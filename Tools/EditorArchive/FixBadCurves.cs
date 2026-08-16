using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// 修复 mixamo 动画曲线坏帧（连续失真段，如 female_Walk 左大腿 Twist 0.10-0.25s 下沉 -122°）
/// 算法：把速率超阈值的连续坏段，用两侧正常关键帧线性插值替换
/// 输出：生成修复后的独立 .anim 资产到 Assets/Art/Animations/Fixed/（FBX 内 clip 只读，不能原地改）
/// 菜单：Tools/Fix Bad Curves（英文）
/// </summary>
public static class FixBadCurves
{
    const float BadRate = 6f;      // rad/s，超过判坏（正常摆动 < 4）
    const string FixedDir = "Assets/Art/Animations/Fixed";
    const float DevThreshold = 0.14f; // 高斯平滑偏差阈值（rad ≈ 8°）：超过才替换（尖峰），斜坡/正常动作保留

    [MenuItem("Tools/Fix Bad Curves")]
    public static void Fix()
    {
        var sb = new StringBuilder();

        // v3：从 FBX 原版重新修复（v2 的速率迭代把正常快速动作也修平了，先恢复干净源）
        // 原版 clip 只读 → 修复后覆盖写回现有 Fixed 资产（guid 不变 → 控制器引用自动生效，绝不 DeleteAsset 重建）
        if (!Directory.Exists(FixedDir)) { sb.AppendLine("Fixed 目录不存在: " + FixedDir); }
        else
        {
            int totalFrames = 0, totalClips = 0;
            foreach (var fixedFile in Directory.GetFiles(FixedDir, "*.anim"))
            {
                var fixedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fixedFile);
                if (fixedClip == null) { sb.AppendLine($"{Path.GetFileName(fixedFile)}: 加载失败"); continue; }
                // 找到对应的 FBX 原版（_fixed.anim 的文件名去掉 _fixed 就是原版名）
                string fbxPath = FixedDir.Substring(0, FixedDir.Length - "Fixed".Length) +
                                 Path.GetFileNameWithoutExtension(fixedFile).Replace("_fixed", "") + ".fbx";
                var src = AssetDatabase.LoadAssetAtPath<AnimationClip>(fbxPath);
                if (src == null) { sb.AppendLine($"{Path.GetFileName(fixedFile)}: 原版 FBX 未找到（跳过）"); continue; }

                // 备份循环设置
                var settings = AnimationUtility.GetAnimationClipSettings(fixedClip);
                bool any = false;
                int clipFrames = 0;
                foreach (var b in AnimationUtility.GetCurveBindings(src))
                {
                    var curve = AnimationUtility.GetEditorCurve(src, b);
                    if (curve == null) continue;
                    var fixedCurve = FixCurve(curve, ref clipFrames);
                    if (fixedCurve != null && fixedCurve != curve)
                    {
                        AnimationUtility.SetEditorCurve(fixedClip, b, fixedCurve);
                        any = true;
                    }
                }
                AnimationUtility.SetAnimationClipSettings(fixedClip, settings);
                EditorUtility.SetDirty(fixedClip);
                if (any) totalClips++;
                totalFrames += clipFrames;
                sb.AppendLine($"{Path.GetFileName(fixedFile)}: 修复 {clipFrames} 帧尖峰");
            }
            AssetDatabase.SaveAssets();
            sb.AppendLine($"\n共修复 {totalClips} 个动画（{totalFrames} 帧尖峰），guid 保持不变（控制器引用无需重建）");
        }

        var outPath = "Assets/Screenshots/fix_bad_curves.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[FixBadCurves] 完成，结果: " + outPath);
    }

    /// <summary>统计 clip 中速率超阈值的坏帧段数量（每条曲线独立计数）</summary>
    private static int CountBadSegments(AnimationClip clip)
    {
        int total = 0;
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null) continue;
            bool prevBad = false;
            for (int i = 1; i < curve.length; i++)
            {
                float dt = curve.keys[i].time - curve.keys[i - 1].time;
                if (dt <= 0.0001f) continue;
                bool bad = Mathf.Abs(curve.keys[i].value - curve.keys[i - 1].value) / dt > BadRate;
                if (bad && !prevBad) total++; // 新坏段起点
                prevBad = bad;
            }
        }
        return total;
    }

    /// <summary>复制 clip 并修复坏段，返回新 clip</summary>
    private static AnimationClip BuildFixedClip(AnimationClip src, StringBuilder sb, out int fixedSegments)
    {
        fixedSegments = 0;
        var dst = new AnimationClip();
        dst.frameRate = src.frameRate;
        dst.wrapMode = src.wrapMode;
        dst.legacy = false;

        // 复制循环/根运动等导入设置（loopTime 等），否则循环动画播完会停住
        var settings = AnimationUtility.GetAnimationClipSettings(src);
        AnimationUtility.SetAnimationClipSettings(dst, settings);

        foreach (var b in AnimationUtility.GetCurveBindings(src))
        {
            var curve = AnimationUtility.GetEditorCurve(src, b);
            if (curve == null) continue;
            var fixedCurve = FixCurve(curve, ref fixedSegments);
            if (fixedCurve != null)
                AnimationUtility.SetEditorCurve(dst, b, fixedCurve);
        }
        return dst;
    }

    /// <summary>
    /// 修复单条曲线（v3）：高斯自适应平滑——只修"相对周围明显偏离"的尖峰帧，保留正常快速动作（斜坡）
    /// 原理：正常动作（斜坡）的高斯平滑偏差小；坏帧（尖峰）偏离大 → 偏差超阈值才替换
    /// 一次通过，无迭代死锁，不误伤正常动作
    /// </summary>
    private static AnimationCurve FixCurve(AnimationCurve curve, ref int fixedFrames)
    {
        if (curve.length < 3) return curve;
        var keys = curve.keys;
        int n = keys.Length;

        // 估计帧间隔（中位数，关键帧时间可能不均匀）
        float dtSum = 0f; int cnt = 0;
        for (int i = 1; i < n; i++)
        {
            float dt = keys[i].time - keys[i - 1].time;
            if (dt > 0.0001f) { dtSum += dt; cnt++; }
        }
        float dtAvg = cnt > 0 ? dtSum / cnt : 0.033f;
        float sigma = dtAvg * 1.8f; // σ ≈ 1.8 帧间隔，窗口覆盖 ±3σ ≈ ±5 帧

        // 时间加权高斯平滑值（全部帧参与，权重重心在中心帧）
        var smooth = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sum = 0f, wsum = 0f;
            for (int j = 0; j < n; j++)
            {
                float d = keys[j].time - keys[i].time;
                float w = Mathf.Exp(-(d * d) / (2f * sigma * sigma));
                sum += w * keys[j].value;
                wsum += w;
            }
            smooth[i] = wsum > 0.0001f ? sum / wsum : keys[i].value;
        }

        // 偏差超阈值才替换（尖峰），切线重置避免折角
        bool any = false;
        for (int i = 0; i < n; i++)
        {
            float dev = Mathf.Abs(smooth[i] - keys[i].value);
            if (dev > DevThreshold)
            {
                var k = keys[i];
                k.value = smooth[i];
                k.inTangent = 0f;
                k.outTangent = 0f;
                keys[i] = k;
                any = true;
                fixedFrames++;
            }
        }
        if (!any) return curve;
        return new AnimationCurve(keys);
    }

    /// <summary>把控制器里引用源 FBX clip 的状态/混合树 motion 替换为 Fixed/*.anim</summary>
    private static void UpdateControllers(List<(string srcPath, string fixedPath)> fixedClips, StringBuilder sb)
    {
        string[] ctrlPaths = {
            "Assets/Art/Animators/FemaleAnimator.controller",
            "Assets/Art/Animators/RangedAnimator.controller",
            "Assets/Art/Animators/MeleeAnimator.controller"
        };
        foreach (var ctrlPath in ctrlPaths)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) { sb.AppendLine($"控制器不存在: {ctrlPath}"); continue; }

            int replaced = 0;
            foreach (var layer in ctrl.layers)
                replaced += ReplaceInStateMachine(layer.stateMachine, fixedClips);
            if (replaced > 0)
            {
                EditorUtility.SetDirty(ctrl);
                sb.AppendLine($"控制器 {Path.GetFileName(ctrlPath)}: 替换 {replaced} 处 clip 引用");
            }
        }
    }

    private static int ReplaceInStateMachine(AnimatorStateMachine sm, List<(string, string)> fixedClips)
    {
        int replaced = 0;
        if (sm == null) return 0;
        foreach (var st in sm.states)
        {
            var m = st.state.motion;
            if (m is AnimationClip clip)
            {
                if (TryReplaceClip(clip, fixedClips, out var fixedClip))
                {
                    st.state.motion = fixedClip;
                    replaced++;
                }
            }
            else if (m is BlendTree bt)
            {
                replaced += ReplaceInBlendTree(bt, fixedClips);
            }
        }
        // 子状态机
        foreach (var child in sm.stateMachines)
            replaced += ReplaceInStateMachine(child.stateMachine, fixedClips);
        return replaced;
    }

    /// <summary>递归替换混合树内的 clip 引用</summary>
    private static int ReplaceInBlendTree(BlendTree bt, List<(string, string)> fixedClips)
    {
        int replaced = 0;
        var children = bt.children;
        for (int i = 0; i < children.Length; i++)
        {
            var m = children[i].motion;
            if (m is AnimationClip clip)
            {
                if (TryReplaceClip(clip, fixedClips, out var fixedClip))
                {
                    children[i].motion = fixedClip;
                    replaced++;
                }
            }
            else if (m is BlendTree child)
            {
                replaced += ReplaceInBlendTree(child, fixedClips);
            }
        }
        bt.children = children;
        return replaced;
    }

    private static bool TryReplaceClip(AnimationClip clip, List<(string, string)> fixedClips, out AnimationClip fixedClip)
    {
        fixedClip = null;
        string srcPath = AssetDatabase.GetAssetPath(clip);
        if (string.IsNullOrEmpty(srcPath)) return false;
        srcPath = srcPath.Replace('\\', '/');
        foreach (var (src, fixedPath) in fixedClips)
        {
            if (srcPath == src.Replace('\\', '/'))
            {
                fixedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fixedPath.Replace('\\', '/'));
                return fixedClip != null;
            }
        }
        return false;
    }
}

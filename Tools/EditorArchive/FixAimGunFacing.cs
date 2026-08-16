using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Text;

/// <summary>
/// 修复瞄准持枪朝向：aimWalk/aimIdle 动画的根骨骼带恒定绕 Y 偏转（RootQ.y≈0.29~0.47），
/// 手部曲线位于该偏转坐标系内 → 运行时根不转、手不动，枪口看起来偏左前方。
/// 修法：按根偏航角 θ 把 左右手 的位置/旋转曲线反向旋转 +θ（使枪口对正前方），RootQ 置 identity。
/// 生成：aimIdle 覆盖 female_aimIdle_fixed.anim；aimWalk 四方向生成 female_aimWalk*2_fixed.anim，
/// 并把两个控制器的 AimWalk 混合树子动画重新指向修正版。
/// 菜单：工具/修复瞄准持枪朝向（英文别名 Tools/FixAimGunFacing）
/// </summary>
public static class FixAimGunFacing
{
    private static readonly (string src, string dst, string dir)[] WalkClips =
    {
        ("Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim", "Assets/Art/Animations/Fixed/female_aimWalk2_fixed.anim", "F"),
        ("Assets/Art/Animations/Fixed/female_aimWalkRight_fixed.anim", "Assets/Art/Animations/Fixed/female_aimWalkRight2_fixed.anim", "R"),
        ("Assets/Art/Animations/Fixed/female_aimWalkBack_fixed.anim", "Assets/Art/Animations/Fixed/female_aimWalkBack2_fixed.anim", "B"),
        ("Assets/Art/Animations/Fixed/female_aimWalkLeft_fixed.anim", "Assets/Art/Animations/Fixed/female_aimWalkLeft2_fixed.anim", "L"),
    };
    private static readonly string IdleSrc = "Assets/Art/Animations/female_aimIdle.fbx";
    private static readonly string IdleDst = "Assets/Art/Animations/Fixed/female_aimIdle_fixed.anim";
    private static readonly string[] Controllers =
    {
        "Assets/Art/Animators/FemaleAnimator.controller",
        "Assets/Art/Animators/RangedAnimator.controller",
    };

    /// <summary>回退实验改动：混合树恢复原始 clip，待机恢复为仅清 RootQ 的版本，删除生成的 2_fixed</summary>
    [MenuItem("工具/回退瞄准朝向实验", false, 1121)]
    [MenuItem("Tools/RevertAimGunFacing", false, 1121)]
    public static void Revert()
    {
        var sb = new StringBuilder();

        // 1. 待机恢复：仅清 RootQ（与 FixAimIdleRoot 一致），原地覆盖保留 guid
        var idleSrc = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleSrc);
        var idleDst = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleDst);
        if (idleSrc != null && idleDst != null)
        {
            foreach (var b in AnimationUtility.GetCurveBindings(idleDst))
                AnimationUtility.SetEditorCurve(idleDst, b, null);
            foreach (var b in AnimationUtility.GetCurveBindings(idleSrc))
            {
                var c = AnimationUtility.GetEditorCurve(idleSrc, b);
                if (c == null) continue;
                if (b.path == "" && b.propertyName.StartsWith("RootQ"))
                {
                    var keys = c.keys;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        var k = keys[i];
                        k.value = b.propertyName == "RootQ.w" ? 1f : 0f;
                        k.inTangent = 0f; k.outTangent = 0f;
                        keys[i] = k;
                    }
                    AnimationUtility.SetEditorCurve(idleDst, b, new AnimationCurve(keys));
                }
                else
                {
                    AnimationUtility.SetEditorCurve(idleDst, b, c);
                }
            }
            EditorUtility.SetDirty(idleDst);
            AssetDatabase.SaveAssets();
            sb.AppendLine("待机 female_aimIdle_fixed.anim 已恢复（仅清 RootQ，guid 不变）");
        }

        // 2. 两个控制器的混合树恢复原始 clip
        var original = new Dictionary<string, AnimationClip>
        {
            ["F"] = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClips[0].src),
            ["R"] = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClips[1].src),
            ["B"] = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClips[2].src),
            ["L"] = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClips[3].src),
        };
        foreach (var ctrlPath in Controllers)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) { sb.AppendLine($"控制器不存在: {ctrlPath}"); continue; }
            int changed = 0;
            foreach (var layer in ctrl.layers)
                changed += RewireBlendTrees(layer.stateMachine, original, ctrlPath, sb);
            if (changed > 0)
            {
                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                sb.AppendLine($"{ctrlPath}: 混合树恢复原始 clip {changed} 处");
            }
        }

        // 3. 删除生成的 2_fixed 实验 clip
        foreach (var (_, dst, _) in WalkClips)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(dst) != null)
            {
                AssetDatabase.DeleteAsset(dst);
                sb.AppendLine($"删除 {dst}");
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(sb.ToString());
        try { System.IO.Directory.CreateDirectory("Assets/Screenshots"); System.IO.File.WriteAllText("Assets/Screenshots/revert_aim_gun.txt", sb.ToString()); } catch { }
    }

    [MenuItem("工具/修复瞄准持枪朝向", false, 1120)]
    [MenuItem("Tools/FixAimGunFacing", false, 1120)]
    public static void Run()
    {
        var sb = new StringBuilder();

        // 1. 待机：覆盖 female_aimIdle_fixed.anim（原 fbx 根偏航 ≈42°）
        var idleSrc = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleSrc);
        if (idleSrc == null) { sb.AppendLine($"待机源缺失: {IdleSrc}"); }
        else
        {
            float yaw = RootYawDeg(idleSrc);
            sb.AppendLine($"待机 {System.IO.Path.GetFileName(IdleSrc)} 根偏航={yaw:F1}°");
            SaveFixedClip(idleSrc, IdleDst, yaw, sb, true);
        }

        // 2. 四方向瞄准走路：生成修正版
        var fixedClips = new Dictionary<string, AnimationClip>(); // 方向 -> clip
        foreach (var (src, dst, dir) in WalkClips)
        {
            var s = AssetDatabase.LoadAssetAtPath<AnimationClip>(src);
            if (s == null) { sb.AppendLine($"走路源缺失: {src}"); continue; }
            float yaw = RootYawDeg(s);
            sb.AppendLine($"走路{dir} {System.IO.Path.GetFileName(src)} 根偏航={yaw:F1}°");
            var clip = SaveFixedClip(s, dst, yaw, sb, true);
            if (clip != null) fixedClips[dir] = clip;
        }

        // 3. 重新接线两个控制器的 AimWalk 混合树（按位置映射 F/R/B/L）
        foreach (var ctrlPath in Controllers)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) { sb.AppendLine($"控制器不存在: {ctrlPath}"); continue; }
            int changed = 0;
            foreach (var layer in ctrl.layers)
                changed += RewireBlendTrees(layer.stateMachine, fixedClips, ctrlPath, sb);
            if (changed > 0)
            {
                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                sb.AppendLine($"{ctrlPath}: 混合树子动画更新 {changed} 处");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(sb.ToString());
        try { System.IO.Directory.CreateDirectory("Assets/Screenshots"); System.IO.File.WriteAllText("Assets/Screenshots/fix_aim_gun.txt", sb.ToString()); } catch { }
    }

    /// <summary>读取 clip 根骨骼恒定绕 Y 偏航角（度）</summary>
    private static float RootYawDeg(AnimationClip clip)
    {
        float qx = 0, qy = 0, qz = 0, qw = 1;
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.path != "" || !b.propertyName.StartsWith("RootQ")) continue;
            var c = AnimationUtility.GetEditorCurve(clip, b);
            if (c == null || c.keys.Length == 0) continue;
            float v = c.keys[0].value;
            switch (b.propertyName)
            {
                case "RootQ.x": qx = v; break;
                case "RootQ.y": qy = v; break;
                case "RootQ.z": qz = v; break;
                case "RootQ.w": qw = v; break;
            }
        }
        var q = new Quaternion(qx, qy, qz, qw);
        return q.eulerAngles.y;
    }

    /// <summary>生成修正版 clip：手部曲线按 +yaw 绕 Y 旋转，RootQ 置 identity，其余原样复制</summary>
    private static AnimationClip SaveFixedClip(AnimationClip src, string dstPath, float yaw, StringBuilder sb, bool deleteOld)
    {
        // 原地覆盖（保留 guid，避免控制器引用断裂）；不存在才新建
        var dst = AssetDatabase.LoadAssetAtPath<AnimationClip>(dstPath);
        bool isNew = dst == null;
        if (isNew)
        {
            dst = new AnimationClip();
            dst.frameRate = src.frameRate;
            dst.wrapMode = src.wrapMode;
            dst.legacy = false;
            AnimationUtility.SetAnimationClipSettings(dst, AnimationUtility.GetAnimationClipSettings(src));
        }
        // 清掉旧曲线，避免残留
        foreach (var b in AnimationUtility.GetCurveBindings(dst))
            AnimationUtility.SetEditorCurve(dst, b, null);

        int torsoCurves = 0, rootCurves = 0;
        foreach (var b in AnimationUtility.GetCurveBindings(src))
        {
            var curve = AnimationUtility.GetEditorCurve(src, b);
            if (curve == null) continue;
            string p = b.propertyName;

            // RootQ → identity
            if (b.path == "" && p.StartsWith("RootQ"))
            {
                var keys = curve.keys;
                for (int i = 0; i < keys.Length; i++)
                {
                    var k = keys[i];
                    k.value = p == "RootQ.w" ? 1f : 0f;
                    k.inTangent = 0f; k.outTangent = 0f;
                    keys[i] = k;
                }
                AnimationUtility.SetEditorCurve(dst, b, new AnimationCurve(keys));
                rootCurves++;
                continue;
            }

            // 上半身扭转曲线：把根偏航分摊到 Spine/Chest/UpperChest 的 Twist（Y 轴旋转可直接加角，
            // 每个肌肉限位约 ±30°，分摊避免超限），把枪口随整个上半身转回正前方
            if (p == "Spine Twist Left-Right" || p == "Chest Twist Left-Right" ||
                p == "UpperChest Twist Left-Right")
            {
                var keys = curve.keys;
                float add = yaw * Mathf.Deg2Rad / 3f;
                for (int i = 0; i < keys.Length; i++)
                {
                    var k = keys[i];
                    k.value += add;
                    keys[i] = k;
                }
                AnimationUtility.SetEditorCurve(dst, b, new AnimationCurve(keys));
                torsoCurves++;
                continue;
            }

            // 其余原样复制
            AnimationUtility.SetEditorCurve(dst, b, curve);
        }

        if (isNew)
            AssetDatabase.CreateAsset(dst, dstPath);
        EditorUtility.SetDirty(dst);
        AssetDatabase.SaveAssets();
        var verify = AssetDatabase.LoadAssetAtPath<AnimationClip>(dstPath);
        sb.AppendLine($"生成 {dstPath}：时长={verify.length:F3}s 帧率={verify.frameRate} 循环={verify.isLooping} | 上半身扭转 {torsoCurves} 条+{yaw:F1}° | RootQ {rootCurves} 条置 identity");
        return verify;
    }

    /// <summary>递归把状态机里名为 AimWalk/AimMoveBlend 的混合树子动画按位置换成修正版</summary>
    private static int RewireBlendTrees(AnimatorStateMachine sm, Dictionary<string, AnimationClip> fixedClips,
        string ctrlPath, StringBuilder sb)
    {
        int n = 0;
        if (sm == null) return 0;
        foreach (var st in sm.states)
        {
            if (st.state.motion is BlendTree bt)
            {
                var children = bt.children;
                for (int i = 0; i < children.Length; i++)
                {
                    var pos = children[i].position;
                    string dir = null;
                    if (pos.x == 0f && pos.y == 1f) dir = "F";
                    else if (pos.x == 1f && pos.y == 0f) dir = "R";
                    else if (pos.x == 0f && pos.y == -1f) dir = "B";
                    else if (pos.x == -1f && pos.y == 0f) dir = "L";
                    if (dir != null && fixedClips.TryGetValue(dir, out var clip) && children[i].motion != clip)
                    {
                        children[i].motion = clip;
                        n++;
                    }
                }
                if (n > 0) bt.children = children;
            }
        }
        foreach (var child in sm.stateMachines)
            n += RewireBlendTrees(child.stateMachine, fixedClips, ctrlPath, sb);
        return n;
    }
}

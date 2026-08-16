using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;

/// <summary>
/// 膝盖链定向修复（v4）：女角色动画 Upper/Lower Leg Twist 曲线存在"驼峰式"异常段
/// 原版 Left Upper Leg Twist 下探 -122°（男参照仅 -24°）、Left Lower Leg Twist 上冲 82.5°（男参照 19°）
/// 驼峰段速率 1565-2353°/s，男参照膝盖链正常上限 ~300°/s
/// v3 高斯平滑对宽驼峰失效（周围帧也是异常值，平滑后仍偏离基线）→ v4 改为线性插值到两侧正常基线
/// 只处理膝盖链曲线（从原版取曲线修复），其他曲线保持 v3 状态；男角色动画已回退不受影响
/// 菜单：Tools/Fix Knee Twists（英文）
/// </summary>
public static class FixKneeTwists
{
    const float BadRate = 8.7f; // rad/s ≈ 500°/s：膝盖链正常速率上限 ~300°/s，500°/s 零误伤
    const string FixedDir = "Assets/Art/Animations/Fixed";

    [MenuItem("Tools/Fix Knee Twists")]
    public static void Run()
    {
        var sb = new StringBuilder();
        int totalClips = 0, totalFrames = 0;
        foreach (var fixedFile in Directory.GetFiles(FixedDir, "female_*.anim"))
        {
            var fixedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fixedFile);
            // 原版 FBX（文件名去掉 _fixed 就是原版名）
            string fbxPath = FixedDir.Substring(0, FixedDir.Length - "Fixed".Length) +
                             Path.GetFileNameWithoutExtension(fixedFile).Replace("_fixed", "") + ".fbx";
            var src = AssetDatabase.LoadAssetAtPath<AnimationClip>(fbxPath);
            if (fixedClip == null || src == null) continue;

            var settings = AnimationUtility.GetAnimationClipSettings(fixedClip);
            int clipFrames = 0;
            foreach (var b in AnimationUtility.GetCurveBindings(src))
            {
                // 只处理膝盖链曲线（Upper Leg / Lower Leg）
                if (!b.propertyName.Contains("Upper Leg") && !b.propertyName.Contains("Lower Leg")) continue;
                var curve = AnimationUtility.GetEditorCurve(src, b);
                if (curve == null) continue;
                var fixedCurve = FixCurve(curve, ref clipFrames);
                AnimationUtility.SetEditorCurve(fixedClip, b, fixedCurve);
            }
            AnimationUtility.SetAnimationClipSettings(fixedClip, settings);
            EditorUtility.SetDirty(fixedClip);
            if (clipFrames > 0)
            {
                totalClips++;
                totalFrames += clipFrames;
                sb.AppendLine($"{Path.GetFileName(fixedFile)}: 修复膝盖链驼峰 {clipFrames} 帧");
            }
        }
        AssetDatabase.SaveAssets();
        sb.AppendLine($"\n共修复 {totalClips} 个动画（膝盖链 {totalFrames} 帧驼峰），guid 不变");

        var outPath = "Assets/Screenshots/fix_knee_twists.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[FixKneeTwists] 完成，结果: " + outPath);
    }

    /// <summary>
    /// 修复单条膝盖链曲线：找"速率连续超阈值的帧对"组成驼峰段，
    /// 段内全部帧线性插值到段外两侧最近正常帧之间（v1 线性插值思路，但只对驼峰生效）
    /// </summary>
    private static AnimationCurve FixCurve(AnimationCurve curve, ref int fixedFrames)
    {
        if (curve.length < 4) return curve;
        var keys = curve.keys;
        int n = keys.Length;

        // 标记坏帧对：i-1 → i 速率超阈值
        var badPair = new bool[n];
        for (int i = 1; i < n; i++)
        {
            float dt = keys[i].time - keys[i - 1].time;
            if (dt <= 0.0001f) continue;
            badPair[i] = Mathf.Abs(keys[i].value - keys[i - 1].value) / dt > BadRate;
        }

        bool any = false;
        int idx = 1;
        while (idx < n)
        {
            if (!badPair[idx]) { idx++; continue; }
            // 驼峰段：[start, end] 覆盖所有连续坏帧对涉及的帧
            int start = idx - 1;
            int end = idx;
            while (end + 1 < n && badPair[end + 1]) end++;
            // 两侧正常基线帧
            int left = Mathf.Max(0, start - 1);
            int right = Mathf.Min(n - 1, end + 1);
            float t0 = keys[left].time, t1 = keys[right].time;
            float v0 = keys[left].value, v1 = keys[right].value;
            for (int k = start; k <= end; k++)
            {
                float frac = t1 > t0 ? (keys[k].time - t0) / (t1 - t0) : 0f;
                var kv = keys[k];
                kv.value = v0 + (v1 - v0) * frac;
                kv.inTangent = 0f;
                kv.outTangent = 0f;
                keys[k] = kv;
                fixedFrames++;
                any = true;
            }
            idx = end + 1;
        }
        if (!any) return curve;
        return new AnimationCurve(keys);
    }
}

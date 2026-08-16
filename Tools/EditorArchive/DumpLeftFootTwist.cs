using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// Dump female_aimWalk_fixed 的 Left Foot Twist In-Out 完整关键帧，
/// 确认坏曲线是"整体偏移"还是"区间坏段"，用于决定修复方案。
/// 菜单：工具/打印左脚Twist曲线（英文别名 Tools/DumpLeftFootTwist）
/// </summary>
public static class DumpLeftFootTwist
{
    [MenuItem("工具/打印左脚Twist曲线", false, 1082)]
    [MenuItem("Tools/DumpLeftFootTwist", false, 1082)]
    public static void Run()
    {
        var sb = new StringBuilder();
        Dump("female_aimWalk_fixed", "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim", sb);
        Dump("female_aimWalkBack_fixed", "Assets/Art/Animations/Fixed/female_aimWalkBack_fixed.anim", sb);
        Dump("female_aimWalkLeft_fixed", "Assets/Art/Animations/Fixed/female_aimWalkLeft_fixed.anim", sb);
        Dump("female_aimWalkRight_fixed", "Assets/Art/Animations/Fixed/female_aimWalkRight_fixed.anim", sb);

        var outPath = "Assets/Screenshots/left_foot_twist.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[DumpTwist] 完成，结果: " + outPath);
    }

    private static void Dump(string label, string path, StringBuilder sb)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { sb.AppendLine($"\n== {label} == 加载失败"); return; }
        sb.AppendLine($"\n========== {label} (时长={clip.length:F3}s) ==========");
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.propertyName != "Left Foot Twist In-Out") continue;
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null) { sb.AppendLine("  无曲线"); continue; }
            sb.AppendLine($"  Left Foot Twist In-Out: {curve.length} 帧");
            for (int i = 0; i < curve.length; i++)
            {
                var k = curve.keys[i];
                sb.AppendLine($"    t={k.time:F3}s 值={k.value * Mathf.Rad2Deg:F1}°");
            }
        }
    }
}

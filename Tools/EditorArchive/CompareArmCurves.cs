using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 对比 female_Walk_fixed vs female_aimWalk_fixed 的左手/左臂肌肉曲线，
/// 定位 aimWalk 持枪左手左偏 43° 的曲线来源（Left Arm / Left Forearm / Left Hand）。
/// 菜单：工具/对比左臂曲线（英文别名 Tools/CompareArmCurves）
/// </summary>
public static class CompareArmCurves
{
    [MenuItem("工具/对比左臂曲线", false, 1100)]
    [MenuItem("Tools/CompareArmCurves", false, 1100)]
    public static void Run()
    {
        var sb = new StringBuilder();
        Dump("female_Walk_fixed(普通)", "Assets/Art/Animations/Fixed/female_Walk_fixed.anim", sb);
        Dump("female_aimWalk_fixed(瞄准)", "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim", sb);

        var outPath = "Assets/Screenshots/compare_arm_curves.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[ArmCurves] 完成，结果: " + outPath);
    }

    private static void Dump(string label, string path, StringBuilder sb)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { sb.AppendLine($"\n== {label} == 加载失败"); return; }
        sb.AppendLine($"\n== {label} (时长={clip.length:F3}s) ==");
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (!b.propertyName.Contains("Arm") && !b.propertyName.Contains("Hand") && !b.propertyName.Contains("Shoulder")) continue;
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null) continue;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var k in curve.keys)
            {
                if (k.value < min) min = k.value;
                if (k.value > max) max = k.value;
            }
            sb.AppendLine($"  {b.propertyName}: [{min * Mathf.Rad2Deg:F1}°, {max * Mathf.Rad2Deg:F1}°]");
        }
    }
}

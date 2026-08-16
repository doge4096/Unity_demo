using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 对比 female_Walk_fixed vs female_aimWalk_fixed 的 Left Lower Leg Twist In-Out 曲线，
/// 确认瞄准走路左小腿内旋 68~84° 是否异常，输出完整关键帧。
/// 菜单：工具/对比左小腿扭曲（英文别名 Tools/CompareLegTwist）
/// </summary>
public static class CompareLegTwist
{
    [MenuItem("工具/对比左小腿扭曲", false, 1092)]
    [MenuItem("Tools/CompareLegTwist", false, 1092)]
    public static void Run()
    {
        var sb = new StringBuilder();
        Dump("female_Walk_fixed(普通)", "Assets/Art/Animations/Fixed/female_Walk_fixed.anim", sb);
        Dump("female_aimWalk_fixed(瞄准)", "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim", sb);

        var outPath = "Assets/Screenshots/compare_leg_twist.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[LegTwist] 完成，结果: " + outPath);
    }

    private static void Dump(string label, string path, StringBuilder sb)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { sb.AppendLine($"\n== {label} == 加载失败"); return; }
        sb.AppendLine($"\n== {label} ==");
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.propertyName != "Left Lower Leg Twist In-Out") continue;
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null) { sb.AppendLine("  无曲线"); continue; }
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < curve.length; i++)
            {
                var k = curve.keys[i];
                if (k.value < min) min = k.value;
                if (k.value > max) max = k.value;
                if (curve.length <= 25) sb.AppendLine($"    t={k.time:F3}s 值={k.value * Mathf.Rad2Deg:F1}°");
            }
            sb.AppendLine($"  共 {curve.length} 帧 值域[{min * Mathf.Rad2Deg:F1}°, {max * Mathf.Rad2Deg:F1}°]");
        }
    }
}

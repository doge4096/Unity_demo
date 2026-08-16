using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 检查所有瞄准移动动画（aimWalk 系列 + aimRun）的左脚 Twist 曲线值域，
/// 找出所有"整体偏移坏曲线"（正常 ±30°，坏曲线 >60°）。
/// 菜单：工具/扫描瞄准动画脚踝坏曲线（英文别名 Tools/ScanAimFootTwist）
/// </summary>
public static class ScanAimFootTwist
{
    private static readonly string[] Clips = {
        "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim",
        "Assets/Art/Animations/Fixed/female_aimWalkBack_fixed.anim",
        "Assets/Art/Animations/Fixed/female_aimWalkLeft_fixed.anim",
        "Assets/Art/Animations/Fixed/female_aimWalkRight_fixed.anim",
        "Assets/Art/Animations/Fixed/female_aimRun_fixed.anim",
        "Assets/Art/Animations/Fixed/female_aimIdle_fixed.anim",
    };

    [MenuItem("工具/扫描瞄准动画脚踝坏曲线", false, 1091)]
    [MenuItem("Tools/ScanAimFootTwist", false, 1091)]
    public static void Run()
    {
        var sb = new StringBuilder();
        foreach (var path in Clips)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { sb.AppendLine($"{path}: 加载失败"); continue; }
            sb.AppendLine($"\n== {System.IO.Path.GetFileName(path)} ==");
            bool anyBad = false;
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (!b.propertyName.Contains("Twist") && !b.propertyName.Contains("Up-Down")) continue;
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                if (curve == null) continue;
                float min = float.MaxValue, max = float.MinValue;
                foreach (var k in curve.keys)
                {
                    if (k.value < min) min = k.value;
                    if (k.value > max) max = k.value;
                }
                float dmin = min * Mathf.Rad2Deg, dmax = max * Mathf.Rad2Deg;
                string flag = (dmax > 60f || dmin < -60f) ? " ← 异常" : "";
                if (flag != "") anyBad = true;
                sb.AppendLine($"  {b.propertyName}: [{dmin:F1}°, {dmax:F1}°]{flag}");
            }
            if (!anyBad) sb.AppendLine("  （脚踝曲线全部正常）");
        }

        var outPath = "Assets/Screenshots/scan_aim_foot.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[ScanFoot] 完成，结果: " + outPath);
    }
}

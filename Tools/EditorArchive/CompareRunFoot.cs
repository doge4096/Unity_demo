using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 对比 female_Run_fixed vs female_aimRun_fixed 的脚部/腿部 Twist、Up-Down 曲线值域，
/// 确认瞄准跑步是否有同类坏曲线（aimWalk 已发现左脚踝+左小腿 Twist 坏）。
/// 菜单：工具/对比跑步脚踝曲线（英文别名 Tools/CompareRunFoot）
/// </summary>
public static class CompareRunFoot
{
    [MenuItem("工具/对比跑步脚踝曲线", false, 1093)]
    [MenuItem("Tools/CompareRunFoot", false, 1093)]
    public static void Run()
    {
        var sb = new StringBuilder();
        Dump("female_Run_fixed(普通)", "Assets/Art/Animations/Fixed/female_Run_fixed.anim", sb);
        Dump("female_aimRun_fixed(瞄准)", "Assets/Art/Animations/Fixed/female_aimRun_fixed.anim", sb);
        Dump("man_Run_fixed(男参照)", "Assets/Art/Animations/Fixed/man_Run_fixed.anim", sb);

        var outPath = "Assets/Screenshots/compare_run_foot.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[RunFoot] 完成，结果: " + outPath);
    }

    private static void Dump(string label, string path, StringBuilder sb)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { sb.AppendLine($"\n== {label} == 加载失败"); return; }
        sb.AppendLine($"\n== {label} (时长={clip.length:F3}s) ==");
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (!b.propertyName.Contains("Twist") && !b.propertyName.Contains("Up-Down") && !b.propertyName.Contains("Lower Leg")) continue;
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
            sb.AppendLine($"  {b.propertyName}: [{dmin:F1}°, {dmax:F1}°]{flag}");
        }
    }
}

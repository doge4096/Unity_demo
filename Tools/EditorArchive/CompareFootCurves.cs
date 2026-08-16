using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 对比 female_Walk_fixed（普通走路）与 female_aimWalk_fixed（瞄准走路）的脚部/脚踝肌肉曲线：
/// 输出 Foot/Toe 相关曲线的值域、最大帧间速率，定位"踝关节像断了一样"是否来自瞄准动画的坏曲线。
/// 菜单：工具/对比走路脚踝曲线（英文别名 Tools/CompareFootCurves）
/// </summary>
public static class CompareFootCurves
{
    [MenuItem("工具/对比走路脚踝曲线", false, 1081)]
    [MenuItem("Tools/CompareFootCurves", false, 1081)]
    public static void Run()
    {
        var sb = new StringBuilder();
        DiagClip("female_Walk_fixed(普通)", "Assets/Art/Animations/Fixed/female_Walk_fixed.anim", sb);
        DiagClip("female_aimWalk_fixed(瞄准)", "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim", sb);
        DiagClip("man_Walking_fixed(男参照)", "Assets/Art/Animations/Fixed/man_Walking_fixed.anim", sb);

        var outPath = "Assets/Screenshots/compare_foot_curves.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[CompareFoot] 完成，结果: " + outPath);
    }

    private static void DiagClip(string label, string path, StringBuilder sb)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { sb.AppendLine("\n== " + label + " == 加载失败"); return; }
        sb.AppendLine("\n========== " + label + " (时长=" + clip.length.ToString("F3") + "s 帧率=" + clip.frameRate + ") ==========");

        int found = 0;
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (!b.propertyName.Contains("Foot") && !b.propertyName.Contains("Toe")) continue;
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null) continue;
            found++;

            float min = float.MaxValue, max = float.MinValue, worstRate = 0f, worstRateT = 0f;
            for (int i = 0; i < curve.length; i++)
            {
                if (curve.keys[i].value < min) min = curve.keys[i].value;
                if (curve.keys[i].value > max) max = curve.keys[i].value;
                if (i > 0)
                {
                    float dt = curve.keys[i].time - curve.keys[i - 1].time;
                    float rate = dt > 0.0001f ? Mathf.Abs(curve.keys[i].value - curve.keys[i - 1].value) / dt : 0f;
                    if (rate > worstRate) { worstRate = rate; worstRateT = curve.keys[i].time; }
                }
            }
            string flag = (max * Mathf.Rad2Deg > 60f || min * Mathf.Rad2Deg < -60f) ? " ← 角度异常(>60°)" : "";
            sb.AppendLine($"  {b.propertyName}: {curve.length} 帧 值域[{min * Mathf.Rad2Deg:F1}°, {max * Mathf.Rad2Deg:F1}°] 最大速率={worstRate * Mathf.Rad2Deg:F0}°/s (@{worstRateT:F3}s){flag}");
        }
        if (found == 0) sb.AppendLine("  未找到 Foot/Toe 曲线");
    }
}

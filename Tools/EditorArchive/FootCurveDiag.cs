using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 脚部曲线专项诊断：dump 所有含 Foot/Toe 的肌肉曲线关键帧（原版 vs Fixed）
/// 一次输出完整数据，定位"速率不高但绝对姿态怪异"的区段
/// 菜单：Tools/Diag Foot Curves（英文）
/// </summary>
public static class FootCurveDiag
{
    [MenuItem("Tools/Diag Foot Curves")]
    public static void Run()
    {
        var sb = new StringBuilder();
        DiagClip("female_Walk 原版", "Assets/Art/Animations/female_Walk.fbx", sb);
        DiagClip("female_Walk Fixed", "Assets/Art/Animations/Fixed/female_Walk_fixed.anim", sb);
        DiagClip("man_Walking 原版(参照)", "Assets/Art/Animations/man_Walking.fbx", sb);

        var outPath = "Assets/Screenshots/foot_curves.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[FootDiag] 完成，结果: " + outPath);
    }

    private static void DiagClip(string label, string path, StringBuilder sb)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { sb.AppendLine("\n== " + label + " == 加载失败"); return; }
        sb.AppendLine("\n========== " + label + " (时长=" + clip.length.ToString("F3") + "s) ==========");

        // 找所有含 Foot/Toe 的肌肉曲线
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
            sb.AppendLine($"\n  {b.propertyName}: {curve.length} 帧 值域[{min * Mathf.Rad2Deg:F1}°, {max * Mathf.Rad2Deg:F1}°] 最大速率={worstRate * Mathf.Rad2Deg:F0}°/s (@{worstRateT:F3}s)");
            // 完整关键帧（每隔一帧）
            for (int i = 0; i < curve.length; i += 2)
            {
                var k = curve.keys[i];
                sb.AppendLine($"    t={k.time:F3}s 值={k.value * Mathf.Rad2Deg:F1}°");
            }
        }
        if (found == 0)
        {
            sb.AppendLine("  未找到 Foot/Toe 曲线，全部 binding 列表：");
            int n = 0;
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (n++ < 40) sb.AppendLine("    " + b.propertyName + " type=" + b.type?.Name);
            }
        }
    }
}

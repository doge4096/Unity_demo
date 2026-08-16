using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 膝盖链专项诊断：Left/Right Thigh(大腿抬落)、Shin(膝盖弯) 肌肉曲线
/// 三组对照（原版 / Fixed / 男参照）+ 速率 + 二阶差分（方向突变 = 尖峰甩动）
/// 菜单：Tools/Diag Knee Curves（英文）
/// </summary>
public static class KneeCurveDiag
{
    [MenuItem("Tools/Diag Knee Curves")]
    public static void Run()
    {
        var sb = new StringBuilder();
        DiagClip("female_Walk 原版", "Assets/Art/Animations/female_Walk.fbx", sb);
        DiagClip("female_Walk Fixed(v3)", "Assets/Art/Animations/Fixed/female_Walk_fixed.anim", sb);
        DiagClip("man_Walking 原版(参照)", "Assets/Art/Animations/man_Walking.fbx", sb);

        var outPath = "Assets/Screenshots/knee_curves.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[KneeDiag] 完成，结果: " + outPath);
    }

    private static void DiagClip(string label, string path, StringBuilder sb)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { sb.AppendLine("\n== " + label + " == 加载失败"); return; }
        sb.AppendLine("\n========== " + label + " (时长=" + clip.length.ToString("F3") + "s) ==========");

        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            // 膝盖链：Upper Leg(大腿摆/收/旋)、Lower Leg(膝盖弯/小腿旋)
            if (!b.propertyName.Contains("Upper Leg") && !b.propertyName.Contains("Lower Leg")) continue;
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null) continue;

            float min = float.MaxValue, max = float.MinValue;
            float worstRate = 0f, worstRateT = 0f;
            float worstA2 = 0f, worstA2T = 0f;
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
                if (i > 1)
                {
                    // 二阶差分：两段相邻斜率突变（rad/s²，直接看弧度差），尖峰 = 方向突变
                    float d1 = curve.keys[i].value - curve.keys[i - 1].value;
                    float d0 = curve.keys[i - 1].value - curve.keys[i - 2].value;
                    float a2 = Mathf.Abs(d1 - d0);
                    if (a2 > worstA2) { worstA2 = a2; worstA2T = curve.keys[i].time; }
                }
            }
            sb.AppendLine($"\n  {b.propertyName}: {curve.length} 帧 值域[{min * Mathf.Rad2Deg:F1}°, {max * Mathf.Rad2Deg:F1}°] 最大速率={worstRate * Mathf.Rad2Deg:F0}°/s(@{worstRateT:F3}s) 最大二阶差={worstA2 * Mathf.Rad2Deg:F1}°(@{worstA2T:F3}s)");
            // 完整关键帧
            for (int i = 0; i < curve.length; i++)
            {
                var k = curve.keys[i];
                sb.AppendLine($"    t={k.time:F3}s 值={k.value * Mathf.Rad2Deg:F1}°");
            }
        }
    }
}

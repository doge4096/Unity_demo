using UnityEditor;
using UnityEngine;
using System.Text;
using System.IO;

/// <summary>
/// 批量扫描所有 FBX 动画的肌肉曲线坏帧（按"速率"判定：弧度差 / 时间差）
/// 走路摆腿的正常瞬时速率 ≈ 2-8 rad/s；>12 rad/s 判定为抖动坏帧
/// 菜单：Tools/Scan Bad Curves（英文）
/// </summary>
public static class BadCurveScanner
{
    const float BadRate = 6f; // rad/s，超过判坏（与 FixBadCurves 修复阈值一致，修复后应扫出 0）

    [MenuItem("Tools/Scan Bad Curves")]
    public static void Scan()
    {
        var sb = new StringBuilder();
        var files = new System.Collections.Generic.List<string>(Directory.GetFiles("Assets/Art/Animations", "*.fbx"));
        if (Directory.Exists("Assets/Art/Animations/Fixed"))
            files.AddRange(Directory.GetFiles("Assets/Art/Animations/Fixed", "*.anim"));
        foreach (var f in files)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(f);
            if (clip == null) continue;
            int totalBad = 0, totalFrames = 0;
            sb.AppendLine($"\n=== {Path.GetFileName(f)} (时长={clip.length:F2}s) ===");
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                if (curve == null) continue;
                for (int i = 1; i < curve.length; i++)
                {
                    float dt = curve.keys[i].time - curve.keys[i - 1].time;
                    if (dt <= 0.0001f) continue;
                    float rate = Mathf.Abs(curve.keys[i].value - curve.keys[i - 1].value) / dt;
                    if (rate > BadRate)
                    {
                        totalBad++;
                        totalFrames++;
                        if (totalFrames <= 3) // 每条曲线只打印前 3 个坏段
                        {
                            float t0 = curve.keys[i - 1].time, t1 = curve.keys[i].time;
                            float v0 = curve.keys[i - 1].value, v1 = curve.keys[i].value;
                            sb.AppendLine($"  [坏] {b.propertyName}: {t0:F3}s 值{v0:F3} → {t1:F3}s 值{v1:F3}（速率={rate:F0} rad/s，间隔{dt * 1000:F0}ms）");
                        }
                    }
                }
            }
            sb.AppendLine($"  坏段总数: {totalBad}");
        }

        var outPath = "Assets/Screenshots/bad_curves.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[BadCurves] 完成，结果: " + outPath);
    }
}

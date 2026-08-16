using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;

/// <summary>
/// 诊断女性动画 clip 的有效性：曲线绑定类型（humanoid muscle 曲线 vs 原始骨骼路径）、关键帧数、值域
/// 判断标准：有效动画的曲线绑定是 humanoid muscle 名（如 "Left Arm Down-Up"）；绑定为空或全零值 = 无效动画
/// 菜单：Tools/Diagnose Animation Clips（中文：工具/诊断动画曲线）
/// </summary>
public static class AnimationClipDiagnose
{
    // 待诊断动画：fbx 原版 + Fixed 修复版 + 基准（man_Walking_fixed 已知有效）
    private static readonly string[] Clips =
    {
        "Assets/Art/Animations/female_Idle.fbx",
        "Assets/Art/Animations/female_aimIdle.fbx",
        "Assets/Art/Animations/female_aimShoot.fbx",
        "Assets/Art/Animations/female_shoot.fbx",
        "Assets/Art/Animations/female_aimWalk.fbx",
        "Assets/Art/Animations/female_aimWalkBack.fbx",
        "Assets/Art/Animations/female_aimWalkLeft.fbx",
        "Assets/Art/Animations/female_aimWalkRight.fbx",
        "Assets/Art/Animations/female_aimRun.fbx",
        "Assets/Art/Animations/female_aimJump.fbx",
        "Assets/Art/Animations/female_aimHit.fbx",
        "Assets/Art/Animations/female_reload.fbx",
        "Assets/Art/Animations/female_Walk.fbx",
        "Assets/Art/Animations/female_Run.fbx",
        "Assets/Art/Animations/female_HitReaction.fbx",
        "Assets/Art/Animations/female_jumpstart.fbx",
        "Assets/Art/Animations/female_floating.fbx",
        "Assets/Art/Animations/female_landing.fbx",
        "Assets/Art/Animations/female_death.fbx",
        "Assets/Art/Animations/Fixed/man_Walking_fixed.anim",
        "Assets/Art/Animations/Fixed/female_shoot_fixed.anim",
        "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim",
        "Assets/Art/Animations/Fixed/female_aimRun_fixed.anim",
        "Assets/Art/Animations/Fixed/female_aimHit_fixed.anim",
        "Assets/Art/Animations/Fixed/female_reload_fixed.anim",
        "Assets/Art/Animations/Fixed/female_jumpstart_fixed.anim",
        "Assets/Art/Animations/Fixed/female_landing_fixed.anim",
    };

    [MenuItem("Tools/Diagnose Animation Clips")]
    [MenuItem("工具/诊断动画曲线")]
    public static void Run()
    {
        var sb = new StringBuilder();
        foreach (var path in Clips)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            sb.AppendLine($"===== {path} =====");
            if (clip == null)
            {
                sb.AppendLine("  [加载失败] clip 为 null");
                continue;
            }
            var bindings = AnimationUtility.GetCurveBindings(clip);
            sb.AppendLine($"  时长={clip.length:F3}s 曲线数={bindings.Length}");

            int muscleCount = 0, pathCount = 0, sampleCount = 0;
            float minVal = float.MaxValue, maxVal = float.MinValue;
            var samplePaths = new System.Collections.Generic.List<string>();
            foreach (var b in bindings)
            {
                if (b.propertyName.Contains("Down-Up") || b.propertyName.Contains("In-Out") ||
                    b.propertyName.Contains("Tilt") || b.propertyName.Contains("m_LocalRotation"))
                    muscleCount++;
                else
                    pathCount++;
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                if (curve != null && curve.keys.Length > 0)
                {
                    sampleCount += curve.keys.Length;
                    foreach (var k in curve.keys)
                    {
                        if (k.value < minVal) minVal = k.value;
                        if (k.value > maxVal) maxVal = k.value;
                    }
                }
                if (samplePaths.Count < 5) samplePaths.Add($"{b.path}/{b.propertyName}");
            }
            sb.AppendLine($"  humanoid肌肉曲线={muscleCount} 骨骼路径曲线={pathCount} 关键帧总数={sampleCount}");
            sb.AppendLine($"  值域=[{minVal:F3}, {maxVal:F3}]" + (sampleCount == 0 ? " [无关键帧=无效]" : ""));
            foreach (var p in samplePaths) sb.AppendLine($"    样例: {p}");
        }
        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/clip_diagnose.txt", sb.ToString());
        Debug.Log("[动画诊断] 完成 → Assets/Screenshots/clip_diagnose.txt");
    }
}

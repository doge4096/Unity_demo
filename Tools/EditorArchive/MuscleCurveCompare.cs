using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;

/// <summary>
/// 对比两个动画 clip 的手臂 muscle 曲线值，确认瞄准动画是否真的包含"抬枪"姿势数据
/// 菜单：Tools/Compare Muscle Curves（中文：工具/对比肌肉曲线）
/// </summary>
public static class MuscleCurveCompare
{
    [MenuItem("Tools/Compare Muscle Curves")]
    [MenuItem("工具/对比肌肉曲线")]
    public static void Run()
    {
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/female_Idle.fbx");
        var aimIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/female_aimIdle.fbx");
        var aimShoot = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/female_aimShoot.fbx");
        var shoot = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/female_shoot.fbx");
        var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Fixed/man_Walking_fixed.anim");
        var shootFixed = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Fixed/female_shoot_fixed.anim");
        var aimWalkFixed = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim");
        var aimRunFixed = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Fixed/female_aimRun_fixed.anim");
        var reloadFixed = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Fixed/female_reload_fixed.anim");
        var aimJumpFixed = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Fixed/female_aimJump_fixed.anim");
        var hitReaction = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/female_HitReaction.fbx");

        var sb = new StringBuilder();
        string[] muscles = {
            "Left Arm Down-Up", "Right Arm Down-Up",
            "Left Arm Front-Back", "Right Arm Front-Back",
            "Left Arm Twist In-Out", "Right Arm Twist In-Out",
            "Left Forearm Stretch", "Right Forearm Stretch",
            "Left Hand Down-Up", "Right Hand Down-Up",
            "Spine Front-Back", "Spine Left-Right",
            "Left Leg Front-Back", "Right Leg Front-Back",
            "Left Leg Down-Up", "Right Leg Down-Up",
            "Left Foot Down-Up", "Right Foot Down-Up",
            "Chest Front-Back", "Chest Left-Right",
        };

        foreach (var m in muscles)
        {
            sb.AppendLine($"===== {m} =====");
            AppendMuscle(sb, idle, m);
            AppendMuscle(sb, aimIdle, m);
            AppendMuscle(sb, aimShoot, m);
            AppendMuscle(sb, shoot, m);
            AppendMuscle(sb, walk, m);
            AppendMuscle(sb, shootFixed, m);
            AppendMuscle(sb, aimWalkFixed, m);
            AppendMuscle(sb, aimRunFixed, m);
            AppendMuscle(sb, reloadFixed, m);
            AppendMuscle(sb, aimJumpFixed, m);
            AppendMuscle(sb, hitReaction, m);
        }
        // 骨骼路径曲线对比：确认手臂姿势数据是否在原始骨骼 localRotation 上（muscle 转换丢失 vs 源动画无姿势）
        sb.AppendLine("\n########## 骨骼路径曲线（手臂 localRotation）##########");
        AppendBonePath(sb, idle, "mixamorig1:LeftArm");
        AppendBonePath(sb, aimIdle, "mixamorig1:LeftArm");
        AppendBonePath(sb, shoot, "mixamorig1:LeftArm");
        AppendBonePath(sb, aimIdle, "mixamorig1:RightArm");
        AppendBonePath(sb, aimIdle, "mixamorig1:LeftForeArm");
        AppendBonePath(sb, aimIdle, "mixamorig1:LeftHand");
        AppendBonePath(sb, aimIdle, "mixamorig1:RightHand");

        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/muscle_compare.txt", sb.ToString());
        Debug.Log("[肌肉曲线对比] 完成 → Assets/Screenshots/muscle_compare.txt");
    }

    /// <summary>输出指定骨骼路径的 localRotation 曲线值域</summary>
    private static void AppendBonePath(StringBuilder sb, AnimationClip clip, string bonePath)
    {
        if (clip == null) { sb.AppendLine("  [clip null]"); return; }
        bool found = false;
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.path == bonePath && b.propertyName.StartsWith("m_LocalRotation"))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                float min = float.MaxValue, max = float.MinValue;
                foreach (var k in curve.keys) { min = Mathf.Min(min, k.value); max = Mathf.Max(max, k.value); }
                sb.AppendLine($"  {clip.name} {bonePath}/{b.propertyName}: 值域=[{min:F3},{max:F3}] 帧数={curve.keys.Length}");
                found = true;
            }
        }
        if (!found) sb.AppendLine($"  {clip.name} {bonePath}: [无骨骼路径曲线]");
    }

    private static void AppendMuscle(StringBuilder sb, AnimationClip clip, string muscle)
    {
        if (clip == null) { sb.AppendLine("  [clip null]"); return; }
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.propertyName == muscle)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                float v0 = curve.keys.Length > 0 ? curve.Evaluate(0f) : float.NaN;
                float vMid = curve.keys.Length > 0 ? curve.Evaluate(clip.length * 0.5f) : float.NaN;
                float min = float.MaxValue, max = float.MinValue;
                foreach (var k in curve.keys) { min = Mathf.Min(min, k.value); max = Mathf.Max(max, k.value); }
                sb.AppendLine($"  {clip.name}: t0={v0:F2} tMid={vMid:F2} 值域=[{min:F2},{max:F2}] 帧数={curve.keys.Length}");
                return;
            }
        }
        sb.AppendLine($"  {clip.name}: [无此曲线]");
    }
}

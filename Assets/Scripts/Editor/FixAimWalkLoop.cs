using UnityEditor;
using UnityEngine;

/// <summary>
/// 修复瞄准走路方向动画不循环问题（m_LoopTime=0 → 1）
/// 根因：female_aimWalkLeft/Right/Back_fixed.anim 从 fbx 提取后未开循环，
/// 瞄准转向时动画播完 1 秒冻结在最后一帧，看起来"没有对应方向的动画"
/// 菜单：工具/修复瞄准走路动画循环（英文别名 Tools/FixAimWalkLoop）
/// </summary>
public static class FixAimWalkLoop
{
    [MenuItem("工具/修复瞄准走路动画循环", false, 1001)]
    [MenuItem("Tools/FixAimWalkLoop", false, 1001)]
    public static void Fix()
    {
        string[] paths =
        {
            "Assets/Art/Animations/Fixed/female_aimWalkLeft_fixed.anim",
            "Assets/Art/Animations/Fixed/female_aimWalkRight_fixed.anim",
            "Assets/Art/Animations/Fixed/female_aimWalkBack_fixed.anim",
        };
        int fixedCount = 0;
        foreach (var p in paths)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
            if (clip == null) { Debug.LogError($"[循环] 加载失败: {p}"); continue; }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            bool wasLoop = settings.loopTime;
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            fixedCount++;
            Debug.Log($"[循环] {clip.name}: loopTime {wasLoop} → {settings.loopTime} | 时长={clip.length:F2}s");
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[循环] 完成：共修复 {fixedCount} 个动画");
    }

    /// <summary>
    /// 检查所有 Fixed 动画的循环状态（辅助确认哪些一次性动画保持不循环）
    /// 菜单：工具/检查动画循环状态（英文别名 Tools/DumpLoopState）
    /// </summary>
    [MenuItem("工具/检查动画循环状态", false, 1001)]
    [MenuItem("Tools/DumpLoopState", false, 1001)]
    public static void DumpLoop()
    {
        string[] paths =
        {
            "Assets/Art/Animations/Fixed/female_aimWalkLeft_fixed.anim",
            "Assets/Art/Animations/Fixed/female_aimWalkRight_fixed.anim",
            "Assets/Art/Animations/Fixed/female_aimWalkBack_fixed.anim",
            "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim",
            "Assets/Art/Animations/Fixed/female_aimRun_fixed.anim",
            "Assets/Art/Animations/Fixed/female_Walk_fixed.anim",
            "Assets/Art/Animations/Fixed/female_Run_fixed.anim",
            "Assets/Art/Animations/Fixed/man_Walking_fixed.anim",
            "Assets/Art/Animations/Fixed/man_Run_fixed.anim",
            "Assets/Art/Animations/Fixed/female_jumpstart_fixed.anim",
            "Assets/Art/Animations/Fixed/female_landing_fixed.anim",
            "Assets/Art/Animations/Fixed/female_HitReaction_fixed.anim",
            "Assets/Art/Animations/Fixed/female_death_fixed.anim",
            "Assets/Art/Animations/Fixed/female_shoot_fixed.anim",
            "Assets/Art/Animations/Fixed/female_reload_fixed.anim",
        };
        foreach (var p in paths)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
            if (clip == null) continue;
            var s = AnimationUtility.GetAnimationClipSettings(clip);
            Debug.Log($"[循环] {clip.name}: loopTime={s.loopTime} loopPose={s.loopBlend} 时长={clip.length:F2}s");
        }
    }
}

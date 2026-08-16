using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 把走路动画从 AimWalk 2D 混合树改成单动画 female_aimWalk_fixed（前向持枪走路），
/// 与跑步（female_aimRun_fixed 单动画）一致——彻底消除 2D 方向混合导致的"斜向走"观感。
/// Walk 状态 motion → female_aimWalk_fixed.anim（递归查找子状态机）。
/// 注意：瞄准时的方向混合（AimWalk 状态）保留不动；只改非瞄准 Walk 状态。
/// 菜单：工具/走路改单动画（英文别名 Tools/WalkToSingleClip）
/// </summary>
public static class WalkToSingleClip
{
    private const string WalkClip = "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim";

    [MenuItem("工具/走路改单动画", false, 1121)]
    [MenuItem("Tools/WalkToSingleClip", false, 1121)]
    public static void Run()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClip);
        if (clip == null) { Debug.LogError("[走路单动画] 找不到动画: " + WalkClip); return; }

        string[] ctrls = {
            "Assets/Art/Animators/FemaleAnimator.controller",
            "Assets/Art/Animators/RangedAnimator.controller",
        };
        foreach (var ctrlPath in ctrls)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) { Debug.LogWarning($"[走路单动画] 控制器不存在: {ctrlPath}"); continue; }
            int changed = 0;
            foreach (var layer in ctrl.layers)
                changed += SwapInSM(layer.stateMachine, clip, ctrlPath);
            if (changed > 0)
            {
                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                Debug.Log($"[走路单动画] {ctrlPath}: {changed} 个 Walk 状态 → {clip.name}");
            }
            else
            {
                Debug.Log($"[走路单动画] {ctrlPath}: 已是单动画（幂等）");
            }
        }
    }

    private static int SwapInSM(AnimatorStateMachine sm, AnimationClip clip, string ctrlPath)
    {
        int n = 0;
        if (sm == null) return 0;
        foreach (var st in sm.states)
        {
            if (st.state.name != "Walk") continue;
            // 混合树 → 单动画
            if (st.state.motion is BlendTree)
            {
                st.state.motion = clip;
                n++;
                Debug.Log($"[走路单动画]   {ctrlPath} 层 {sm.name}: Walk 混合树 → {clip.name}");
            }
        }
        foreach (var child in sm.stateMachines)
            n += SwapInSM(child.stateMachine, clip, ctrlPath);
        return n;
    }
}

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 把待机举枪状态（AimIdle）指向修正版动画 female_aimIdle_fixed.anim
/// （消除了原版 female_aimIdle.fbx 的 RootQ 绕 Y 恒定偏转 ~42°，待机姿势不再歪）。
/// 两个控制器（FemaleAnimator / RangedAnimator）都处理，递归查找子状态机。幂等。
/// 菜单：工具/应用修正待机动画（英文别名 Tools/ApplyAimIdleFix）
/// </summary>
public static class ApplyAimIdleFix
{
    private const string FixedClip = "Assets/Art/Animations/Fixed/female_aimIdle_fixed.anim";
    private static readonly string[] Controllers = {
        "Assets/Art/Animators/FemaleAnimator.controller",
        "Assets/Art/Animators/RangedAnimator.controller",
    };

    [MenuItem("工具/应用修正待机动画", false, 1105)]
    [MenuItem("Tools/ApplyAimIdleFix", false, 1105)]
    public static void Run()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(FixedClip);
        if (clip == null) { Debug.LogError("[待机修正] 找不到修正动画: " + FixedClip); return; }

        foreach (var ctrlPath in Controllers)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) { Debug.LogWarning($"[待机修正] 控制器不存在: {ctrlPath}"); continue; }

            int changed = 0;
            foreach (var layer in ctrl.layers)
                changed += SwapInSM(layer.stateMachine, clip, ctrlPath);

            if (changed > 0)
            {
                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                Debug.Log($"[待机修正] {ctrlPath}: {changed} 个 AimIdle 状态 → {clip.name}（角度已修正）");
            }
            else
            {
                Debug.Log($"[待机修正] {ctrlPath}: 已是修正动画（幂等）");
            }
        }
    }

    private static int SwapInSM(AnimatorStateMachine sm, AnimationClip clip, string ctrlPath)
    {
        int n = 0;
        if (sm == null) return 0;
        foreach (var st in sm.states)
        {
            // AimIdle 与 Idle（普通待机已统一为举枪）都指向修正动画
            if (st.state.name != "AimIdle" && st.state.name != "Idle") continue;
            if (st.state.motion is AnimationClip c && c.name == clip.name) continue;
            st.state.motion = clip;
            n++;
            Debug.Log($"[待机修正]   {ctrlPath} 层 {sm.name}: {st.state.name} → {clip.name}");
        }
        foreach (var child in sm.stateMachines)
            n += SwapInSM(child.stateMachine, clip, ctrlPath);
        return n;
    }
}

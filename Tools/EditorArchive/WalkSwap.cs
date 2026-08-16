using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 把女角色走路动画换成男走路动画（质量诊断结论：female_Walk_fixed 膝盖深弯 +
/// 腰部侧摆过大，2.1 倍速下观感"摇动"；用户已确认换男动画）：
/// FemaleAnimator.controller / RangedAnimator.controller 中所有名为 Walk 的状态
/// motion: female_Walk_fixed → man_Walking_fixed（递归查找子状态机）
/// 只改控制器引用，不动动画资产（男动画 man_* 一律不修改）
/// 菜单：Tools/Swap Female Walk（英文）
/// </summary>
public static class WalkSwap
{
    private const string WalkClip = "Assets/Art/Animations/Fixed/man_Walking_fixed.anim";
    private static readonly string[] Controllers = {
        "Assets/Art/Animators/FemaleAnimator.controller",
        "Assets/Art/Animators/RangedAnimator.controller",
    };

    [MenuItem("Tools/Swap Female Walk")]
    public static void Run()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClip);
        if (clip == null) { Debug.LogError("[WalkSwap] 男走路动画不存在: " + WalkClip); return; }

        foreach (var ctrlPath in Controllers)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) { Debug.LogWarning($"[WalkSwap] 控制器不存在: {ctrlPath}"); continue; }

            int swapped = 0;
            foreach (var layer in ctrl.layers)
                swapped += SwapInSM(layer.stateMachine, clip, ctrlPath);

            if (swapped > 0)
            {
                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                Debug.Log($"[WalkSwap] {ctrlPath}: 替换 {swapped} 个 Walk 状态 → {clip.name}");
            }
            else
            {
                Debug.Log($"[WalkSwap] {ctrlPath}: 未找到 Walk 状态或已是男动画");
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
            var m = st.state.motion;
            if (m is AnimationClip c)
            {
                if (c.name == clip.name) continue; // 已是男动画
                st.state.motion = clip;
                n++;
                Debug.Log($"[WalkSwap]   {ctrlPath} 层 {sm.name}: Walk 状态 {c.name} → {clip.name}");
            }
            else if (m is BlendTree)
            {
                Debug.Log($"[WalkSwap]   {ctrlPath} 层 {sm.name}: Walk 是 BlendTree，跳过（需手动处理）");
            }
        }
        foreach (var child in sm.stateMachines)
            n += SwapInSM(child.stateMachine, clip, ctrlPath);
        return n;
    }
}

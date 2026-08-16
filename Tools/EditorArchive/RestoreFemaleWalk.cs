using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 把女角色走路动画换回女性自己的 female_Walk_fixed（与 WalkSwap 相反方向）：
/// FemaleAnimator.controller / RangedAnimator.controller 中所有名为 Walk 的状态
/// motion: man_Walking_fixed → female_Walk_fixed（递归查找子状态机）
/// 只改控制器引用，不动动画资产
/// 菜单：工具/恢复女性走路动画（英文别名 Tools/RestoreFemaleWalk）
/// </summary>
public static class RestoreFemaleWalk
{
    private const string FemaleWalkClip = "Assets/Art/Animations/Fixed/female_Walk_fixed.anim";
    private static readonly string[] Controllers = {
        "Assets/Art/Animators/FemaleAnimator.controller",
        "Assets/Art/Animators/RangedAnimator.controller",
    };

    [MenuItem("工具/恢复女性走路动画", false, 1030)]
    [MenuItem("Tools/RestoreFemaleWalk", false, 1030)]
    public static void Run()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(FemaleWalkClip);
        if (clip == null) { Debug.LogError("[RestoreFemaleWalk] 女性走路动画不存在: " + FemaleWalkClip); return; }

        foreach (var ctrlPath in Controllers)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) { Debug.LogWarning($"[RestoreFemaleWalk] 控制器不存在: {ctrlPath}"); continue; }

            int swapped = 0;
            foreach (var layer in ctrl.layers)
                swapped += SwapInSM(layer.stateMachine, clip, ctrlPath);

            if (swapped > 0)
            {
                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                Debug.Log($"[RestoreFemaleWalk] {ctrlPath}: 替换 {swapped} 个 Walk 状态 → {clip.name}");
            }
            else
            {
                Debug.Log($"[RestoreFemaleWalk] {ctrlPath}: 未找到 Walk 状态或已是女性走路动画");
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
                if (c.name == clip.name) continue; // 已是女性走路动画
                st.state.motion = clip;
                n++;
                Debug.Log($"[RestoreFemaleWalk]   {ctrlPath} 层 {sm.name}: Walk 状态 {c.name} → {clip.name}");
            }
            else if (m is BlendTree)
            {
                Debug.Log($"[RestoreFemaleWalk]   {ctrlPath} 层 {sm.name}: Walk 是 BlendTree，跳过（需手动处理）");
            }
        }
        foreach (var child in sm.stateMachines)
            n += SwapInSM(child.stateMachine, clip, ctrlPath);
        return n;
    }
}

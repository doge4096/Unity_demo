using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 把女角色跑步动画换回女性自己的 female_Run_fixed（撤销历史实验 SwapFemaleRunToMale 的遗留）：
/// FemaleAnimator.controller / RangedAnimator.controller 中所有名为 Run 的状态
/// motion: man_Run_fixed → female_Run_fixed（递归查找子状态机）
/// 只改控制器引用，不动动画资产
/// 菜单：工具/恢复女性跑步动画（英文别名 Tools/RestoreFemaleRun）
/// </summary>
public static class RestoreFemaleRun
{
    private const string FemaleRunClip = "Assets/Art/Animations/Fixed/female_Run_fixed.anim";
    private static readonly string[] Controllers = {
        "Assets/Art/Animators/FemaleAnimator.controller",
        "Assets/Art/Animators/RangedAnimator.controller",
    };

    [MenuItem("工具/恢复女性跑步动画", false, 1031)]
    [MenuItem("Tools/RestoreFemaleRun", false, 1031)]
    public static void Run()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(FemaleRunClip);
        if (clip == null) { Debug.LogError("[RestoreFemaleRun] 女性跑步动画不存在: " + FemaleRunClip); return; }

        foreach (var ctrlPath in Controllers)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) { Debug.LogWarning($"[RestoreFemaleRun] 控制器不存在: {ctrlPath}"); continue; }

            int swapped = 0;
            foreach (var layer in ctrl.layers)
                swapped += SwapInSM(layer.stateMachine, clip, ctrlPath);

            if (swapped > 0)
            {
                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                Debug.Log($"[RestoreFemaleRun] {ctrlPath}: 替换 {swapped} 个 Run 状态 → {clip.name}");
            }
            else
            {
                Debug.Log($"[RestoreFemaleRun] {ctrlPath}: 未找到 Run 状态或已是女性跑步动画");
            }
        }
    }

    private static int SwapInSM(AnimatorStateMachine sm, AnimationClip clip, string ctrlPath)
    {
        int n = 0;
        if (sm == null) return 0;
        foreach (var st in sm.states)
        {
            if (st.state.name != "Run") continue;
            var m = st.state.motion;
            if (m is AnimationClip c)
            {
                if (c.name == clip.name) continue; // 已是女性跑步动画
                st.state.motion = clip;
                n++;
                Debug.Log($"[RestoreFemaleRun]   {ctrlPath} 层 {sm.name}: Run 状态 {c.name} → {clip.name}");
            }
            else if (m is BlendTree)
            {
                Debug.Log($"[RestoreFemaleRun]   {ctrlPath} 层 {sm.name}: Run 是 BlendTree，跳过（需手动处理）");
            }
        }
        foreach (var child in sm.stateMachines)
            n += SwapInSM(child.stateMachine, clip, ctrlPath);
        return n;
    }
}

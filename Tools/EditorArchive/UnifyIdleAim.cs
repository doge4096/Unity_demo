using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 把站立待机也改成举枪待机（用户需求：角色全程举枪，不需要放下枪）：
/// FemaleAnimator / RangedAnimator 中所有名为 Idle 的状态 motion 换成 AimIdle 的动画
/// （female_aimIdle.fbx，与 AimIdle 状态相同引用），递归查找子状态机。
/// 只改控制器引用，不动动画资产。幂等。
/// 菜单：工具/待机统一为举枪（英文别名 Tools/UnifyIdleAim）
/// </summary>
public static class UnifyIdleAim
{
    private static readonly string[] Controllers = {
        "Assets/Art/Animators/FemaleAnimator.controller",
        "Assets/Art/Animators/RangedAnimator.controller",
    };

    [MenuItem("工具/待机统一为举枪", false, 1033)]
    [MenuItem("Tools/UnifyIdleAim", false, 1033)]
    public static void Run()
    {
        foreach (var ctrlPath in Controllers)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) { Debug.LogWarning($"[待机举枪] 控制器不存在: {ctrlPath}"); continue; }

            // 找到 AimIdle 状态的 motion 作为目标动画
            var aimIdleMotion = FindStateMotion(ctrl, "AimIdle");
            if (aimIdleMotion == null)
            {
                Debug.LogWarning($"[待机举枪] {ctrlPath} 找不到 AimIdle 状态");
                continue;
            }

            int changed = 0;
            foreach (var layer in ctrl.layers)
                changed += SwapInSM(layer.stateMachine, aimIdleMotion, ctrlPath);

            if (changed > 0)
            {
                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                Debug.Log($"[待机举枪] {ctrlPath}: 替换 {changed} 个 Idle 状态 → {aimIdleMotion.name}（举枪待机）");
            }
            else
            {
                Debug.Log($"[待机举枪] {ctrlPath}: 已是举枪待机（幂等）");
            }
        }
    }

    /// <summary>查找状态（含子状态机）的 motion</summary>
    private static Motion FindStateMotion(AnimatorController ctrl, string stateName)
    {
        foreach (var layer in ctrl.layers)
        {
            var m = FindInSM(layer.stateMachine, stateName);
            if (m != null) return m;
        }
        return null;
    }

    private static Motion FindInSM(AnimatorStateMachine sm, string name)
    {
        if (sm == null) return null;
        foreach (var st in sm.states)
            if (st.state.name == name) return st.state.motion;
        foreach (var child in sm.stateMachines)
        {
            var m = FindInSM(child.stateMachine, name);
            if (m != null) return m;
        }
        return null;
    }

    private static int SwapInSM(AnimatorStateMachine sm, Motion aimMotion, string ctrlPath)
    {
        int n = 0;
        if (sm == null) return 0;
        foreach (var st in sm.states)
        {
            if (st.state.name != "Idle") continue;
            if (st.state.motion == aimMotion) continue; // 已是举枪待机
            st.state.motion = aimMotion;
            n++;
            Debug.Log($"[待机举枪]   {ctrlPath} 层 {sm.name}: Idle → {aimMotion.name}");
        }
        foreach (var child in sm.stateMachines)
            n += SwapInSM(child.stateMachine, aimMotion, ctrlPath);
        return n;
    }
}

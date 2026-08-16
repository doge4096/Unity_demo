using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 统一移动动画为瞄准姿态（用户需求：走路/跑步一直举枪，不再区分瞄准/不瞄准两套动画）：
/// - FemaleAnimator: Walk 状态 motion → AimWalk 混合树（同对象引用），Run 状态 motion → AimRun（female_aimRun_fixed）
/// - RangedAnimator: Walk → AimMoveBlend 混合树，Run → AimMoveBlend（无独立 AimRun，统一用移动混合树）
/// 只改状态 motion 引用，不动过渡结构（过渡仍按 IsAiming 切换，但两状态播相同动画 → 视觉无差别）
/// 菜单：工具/统一移动为瞄准姿态（英文别名 Tools/UnifyMoveToAimPose）
/// </summary>
public static class UnifyMoveToAimPose
{
    [MenuItem("工具/统一移动为瞄准姿态", false, 1032)]
    [MenuItem("Tools/UnifyMoveToAimPose", false, 1032)]
    public static void Run()
    {
        UnifyFemale();
        UnifyRanged();
        Debug.Log("[统一瞄准] 完成：走路/跑步已统一为瞄准姿态动画");
    }

    /// <summary>FemaleAnimator：Walk → AimWalk 混合树；Run → AimRun</summary>
    private static void UnifyFemale()
    {
        const string ctrlPath = "Assets/Art/Animators/FemaleAnimator.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
        if (ctrl == null) { Debug.LogError("[统一瞄准] 找不到 FemaleAnimator.controller"); return; }

        var aimWalk = FindState(ctrl, "AimWalk");
        var walk = FindState(ctrl, "Walk");
        var aimRun = FindState(ctrl, "AimRun");
        var run = FindState(ctrl, "Run");

        int changed = 0;
        if (aimWalk != null && walk != null && walk.motion != aimWalk.motion)
        {
            walk.motion = aimWalk.motion;
            changed++;
            Debug.Log($"[统一瞄准] Walk → AimWalk 混合树 ({aimWalk.motion.name})");
        }
        if (aimRun != null && run != null && run.motion != aimRun.motion)
        {
            run.motion = aimRun.motion;
            changed++;
            Debug.Log($"[统一瞄准] Run → AimRun ({aimRun.motion.name})");
        }
        if (changed > 0)
        {
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
        }
        else
        {
            Debug.Log("[统一瞄准] FemaleAnimator: Walk/Run 已是瞄准姿态（幂等）");
        }
    }

    /// <summary>RangedAnimator：Walk → AimMove（持 AimMoveBlend 混合树的状态）；Run → AimMove</summary>
    private static void UnifyRanged()
    {
        const string ctrlPath = "Assets/Art/Animators/RangedAnimator.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
        if (ctrl == null) { Debug.LogWarning("[统一瞄准] 找不到 RangedAnimator.controller"); return; }

        var aimMove = FindState(ctrl, "AimMove");
        var walk = FindState(ctrl, "Walk");
        var run = FindState(ctrl, "Run");

        int changed = 0;
        if (aimMove != null && walk != null && walk.motion != aimMove.motion)
        {
            walk.motion = aimMove.motion;
            changed++;
            Debug.Log($"[统一瞄准] Ranged Walk → AimMove ({aimMove.motion.name})");
        }
        if (aimMove != null && run != null && run.motion != aimMove.motion)
        {
            run.motion = aimMove.motion;
            changed++;
            Debug.Log($"[统一瞄准] Ranged Run → AimMove ({aimMove.motion.name})");
        }
        if (changed > 0)
        {
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
        }
        else
        {
            Debug.Log("[统一瞄准] RangedAnimator: Walk/Run 已是瞄准姿态（幂等）");
        }
    }

    /// <summary>递归查找指定名称的状态（遍历所有层/子状态机）</summary>
    private static AnimatorState FindState(AnimatorController ctrl, string name)
    {
        foreach (var layer in ctrl.layers)
        {
            var s = FindInSM(layer.stateMachine, name);
            if (s != null) return s;
        }
        return null;
    }

    private static AnimatorState FindInSM(AnimatorStateMachine sm, string name)
    {
        if (sm == null) return null;
        foreach (var st in sm.states)
            if (st.state.name == name) return st.state;
        foreach (var child in sm.stateMachines)
        {
            var s = FindInSM(child.stateMachine, name);
            if (s != null) return s;
        }
        return null;
    }
}

using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 修复格挡中移动的滑步问题：Base Layer 的 Block 状态缺过渡，
/// 先按格挡再按前进时腿部一直播原地格挡动画、人却满速位移 → 飘移滑行。
/// 补两条过渡：
/// 1) Block → Walk：Speed>0.1 且 IsBlocking（格挡中移动 → 下半身切走路动画）
/// 2) Walk → Block：Speed<0.1 且 IsBlocking（格挡中停下 → 回原地格挡）
/// 菜单「工具/修复格挡移动」（幂等，重复执行安全）
/// </summary>
public static class BlockMoveFix
{
    [MenuItem("工具/修复格挡移动")]
    public static void Run()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Art/Animators/MeleeAnimator.controller");
        if (controller == null)
        {
            Debug.LogError("[格挡修复] 找不到 MeleeAnimator.controller");
            return;
        }

        // Base Layer 状态机
        var sm = controller.layers[0].stateMachine;
        var blockState = FindState(sm, "Block");
        var walkState = FindState(sm, "Walk");
        if (blockState == null || walkState == null)
        {
            Debug.LogError("[格挡修复] Base Layer 找不到 Block 或 Walk 状态");
            return;
        }

        // 1) Block → Walk：Speed>0.1 且 IsBlocking
        bool hasBlockToWalk = blockState.transitions.Any(t => t.destinationState == walkState);
        if (!hasBlockToWalk)
        {
            var t = blockState.AddTransition(walkState);
            t.hasExitTime = false;
            t.duration = 0.1f;
            t.hasFixedDuration = true;
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t.AddCondition(AnimatorConditionMode.If, 0f, "IsBlocking");
            Debug.Log("[格挡修复] 已添加 Block → Walk 过渡");
        }

        // 2) Walk → Block：Speed<0.1 且 IsBlocking（格挡中停住回原地格挡）
        bool hasWalkToBlock = walkState.transitions.Any(t => t.destinationState == blockState);
        if (!hasWalkToBlock)
        {
            var t = walkState.AddTransition(blockState);
            t.hasExitTime = false;
            t.duration = 0.1f;
            t.hasFixedDuration = true;
            t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            t.AddCondition(AnimatorConditionMode.If, 0f, "IsBlocking");
            Debug.Log("[格挡修复] 已添加 Walk → Block 过渡");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[格挡修复] 完成（幂等，重复执行不会重复添加）");
    }

    /// <summary>在状态机的直接子状态里按名字查找（不进入子状态机）</summary>
    static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        return sm.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
    }
}

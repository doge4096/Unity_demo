using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 修复攻击过渡丢失：Base Layer 的 anyStateTransitions 缺进入 Attack1/2/3 的过渡
/// （疑似清缓存重导入时过渡对象断链，3 条过渡变成孤儿，动画控制里没有任何状态能切入攻击段）。
/// 重新挂 Any State → Attack1/2/3：条件 Attack(If) + Combo==N(Equals) + Speed<0.1(Less)，duration 0.05。
/// 菜单「工具/修复攻击过渡」（幂等，重复执行安全）
/// </summary>
public static class AttackTransitionFix
{
    private static readonly (string State, int Combo)[] Attacks =
        { ("Attack1", 1), ("Attack2", 2), ("Attack3", 3) };

    [MenuItem("工具/修复攻击过渡")]
    public static void Run()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Art/Animators/MeleeAnimator.controller");
        if (controller == null)
        {
            Debug.LogError("[修复攻击] 找不到 MeleeAnimator.controller");
            return;
        }

        var sm = controller.layers[0].stateMachine; // Base Layer
        int added = 0;
        foreach (var (stateName, combo) in Attacks)
        {
            var state = FindState(sm, stateName);
            if (state == null)
            {
                Debug.LogError($"[修复攻击] Base Layer 找不到状态 {stateName}");
                continue;
            }

            // 已存在到该状态的 Any State 过渡则跳过（幂等）
            if (sm.anyStateTransitions.Any(t => t.destinationState == state))
            {
                Debug.Log($"[修复攻击] {stateName} 的 Any State 过渡已存在，跳过");
                continue;
            }

            var t = sm.AddAnyStateTransition(state);
            t.hasExitTime = false;
            t.duration = 0.05f;
            t.hasFixedDuration = true;
            t.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            t.AddCondition(AnimatorConditionMode.Equals, combo, "Combo");
            t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            added++;
            Debug.Log($"[修复攻击] 已添加 Any State → {stateName} (Combo=={combo})");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[修复攻击] 完成，共添加 {added} 条过渡");
    }

    /// <summary>在状态机的直接子状态里按名字查找</summary>
    private static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        return sm.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
    }
}

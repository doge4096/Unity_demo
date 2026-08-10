using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 修复攻击动画切换太快：攻击段（Attack1/2/3、UAttack1/2/3）的 exit time 原本是 0.9，
/// 动画播到 90% 就开始过渡回 Idle/Empty，最后 10% 的动画被跳过（点按和连击都「没播完就切」）。
/// 把所有攻击段的 hasExitTime 过渡改为 exitTime = 1.0（播完整段再切）。
/// 注意：只改攻击段自身发出的过渡，不改打断类过渡（Hit 等，保持被打断能力）。
/// 菜单「工具/攻击动画播完整段」（幂等，重复执行安全）
/// </summary>
public static class AttackExitFix
{
    private static readonly string[] AttackStateNames =
        { "Attack1", "Attack2", "Attack3", "UAttack1", "UAttack2", "UAttack3" };

    [MenuItem("工具/攻击动画播完整段")]
    public static void Run()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Art/Animators/MeleeAnimator.controller");
        if (controller == null)
        {
            Debug.LogError("[攻击播完] 找不到 MeleeAnimator.controller");
            return;
        }

        int changed = 0;
        foreach (var layer in controller.layers)
        {
            foreach (var state in layer.stateMachine.states.Select(s => s.state))
            {
                if (!AttackStateNames.Contains(state.name)) continue;

                foreach (var t in state.transitions)
                {
                    if (!t.hasExitTime) continue;
                    if (t.exitTime >= 1f) continue;

                    t.exitTime = 1f;
                    changed++;
                    Debug.Log($"[攻击播完] {state.name} 的过渡 exitTime -> 1.0");
                }
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[攻击播完] 完成，共修改 {changed} 条过渡");
    }
}

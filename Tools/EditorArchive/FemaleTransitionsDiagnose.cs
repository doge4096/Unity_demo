using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 诊断 FemaleAnimator 过渡在内存中的实际数据（对比 MeleeAnimator）
/// 检查每个状态过渡的条件数量、参数名、目标状态，找出"过渡不生效"的根因
/// </summary>
public static class FemaleTransitionsDiagnose
{
    [MenuItem("工具/诊断女性控制器过渡", false, 1001)]
    [MenuItem("Tools/FemaleTransitionsDiagnose", false, 1001)]
    public static void Run()
    {
        Diagnose("Assets/Art/Animators/FemaleAnimator.controller", "女性");
        Diagnose("Assets/Art/Animators/MeleeAnimator.controller", "男性(对照)");
    }

    private static void Diagnose(string path, string label)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            Debug.LogError($"[诊断] {label} {path} 加载失败！");
            return;
        }

        Debug.Log($"===== {label} {path} =====");
        Debug.Log($"[诊断] 参数数: {controller.parameters.Length}, 层数: {controller.layers.Length}");

        for (int li = 0; li < controller.layers.Length; li++)
        {
            var layer = controller.layers[li];
            var sm = layer.stateMachine;

            // 检查 AnyState 过渡
            Debug.Log($"[诊断] 层[{li}] {layer.name}: AnyState过渡 {sm.anyStateTransitions.Length} 条, 默认状态 {(sm.defaultState != null ? sm.defaultState.name : "NULL!!!")}");

            foreach (var state in sm.states)
            {
                var s = state.state;
                if (s == null) continue;
                int valid = 0;
                foreach (var t in s.transitions)
                {
                    if (t == null) { Debug.LogWarning($"[诊断] {s.name} 含 null 过渡！"); continue; }
                    valid++;
                    string dst = t.destinationState != null ? t.destinationState.name : (t.isExit ? "Exit" : "NULL!!!");
                    string conds = t.conditions.Length > 0
                        ? string.Join(", ", System.Array.ConvertAll(t.conditions, c => $"{c.parameter}({c.mode})={c.threshold}"))
                        : "无条件!!!";
                    if (t.conditions.Length == 0 || t.destinationState == null)
                        Debug.LogWarning($"[诊断] {s.name} → {dst}: 条件[{conds}] HasExitTime={t.hasExitTime} — 异常!");
                }
                if (s.transitions.Length > 0)
                    Debug.Log($"[诊断] {s.name}: {s.transitions.Length} 条过渡 ({valid} 有效)");
            }
        }
        Debug.Log($"===== {label} 诊断完毕 =====");
    }
}

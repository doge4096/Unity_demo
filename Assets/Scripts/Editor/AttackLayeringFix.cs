using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 攻击全走 UpperBody 层：移除 Base Layer 的全身攻击过渡，
/// UpperBody 的 UAttack 去掉 Speed 条件
/// 菜单「工具/攻击全部分层」
/// </summary>
public static class AttackLayeringFix
{
    [MenuItem("工具/攻击全部分层")]
    [MenuItem("Tools/Attack All Layered")]
    public static void Run()
    {
        var sb = new System.Text.StringBuilder();
        var outPath = "D:/Project/unity/interview/Assets/Screenshots/attack_layering.txt";
        try
        {
            var path = "Assets/Art/Animators/MeleeAnimator.controller";
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl == null) { sb.AppendLine("controller 加载失败!"); }
            else
            {
                // 1. Base Layer：移除到 Attack1/2/3 的 AnyState 过渡
                var baseSM = ctrl.layers[0].stateMachine;
                var keep = new List<AnimatorStateTransition>();
                int removed = 0;
                foreach (var t in baseSM.anyStateTransitions)
                {
                    bool isAttack = t.destinationState != null &&
                        (t.destinationState.name == "Attack1" || t.destinationState.name == "Attack2" || t.destinationState.name == "Attack3");
                    if (isAttack) removed++;
                    else keep.Add(t);
                }
                baseSM.anyStateTransitions = keep.ToArray();
                sb.AppendLine($"Base Layer 移除全身攻击过渡: {removed} 个，剩余 {keep.Count} 个");

                // 2. UpperBody：UAttack 过渡去掉 Speed 条件
                var upperSM = ctrl.layers[1].stateMachine;
                int speedRemoved = 0;
                foreach (var t in upperSM.anyStateTransitions)
                {
                    if (t.destinationState != null && t.destinationState.name.StartsWith("UAttack"))
                    {
                        var conds = t.conditions.Where(c => c.parameter != "Speed").ToArray();
                        if (conds.Length != t.conditions.Length)
                        {
                            t.conditions = conds;
                            speedRemoved++;
                        }
                    }
                }
                sb.AppendLine($"UpperBody UAttack 去掉 Speed 条件: {speedRemoved} 处");

                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                sb.AppendLine("完成");
            }
        }
        catch (System.Exception e)
        {
            sb.AppendLine("异常: " + e);
        }
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[攻击分层] " + sb.ToString());
    }
}

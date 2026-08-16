using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 全部动作统一为瞄准姿态（用户需求：待机/移动/跳跃/受击/射击/换弹全程举枪）：
/// - 跳跃：JumpStart 触发恒走 AimJump 单状态（删除非瞄准 JumpStart 分支）
/// - 受击：Hit 触发恒走上层 AimHit（删除 Base 层非瞄准 Hit 分支）
/// - 射击：Shoot 触发恒走上层 AimShoot（删除非瞄准 Shoot 分支）
/// - 换弹：Reload 过渡去掉 IsAiming 条件
/// 死亡无瞄准版动画，保持原样。
/// 不动 PlayerController 的 IsAiming（相机缩放行为不变）。
/// 菜单：工具/全部动作统一为瞄准姿态（英文别名 Tools/UnifyAllAim）
/// </summary>
public static class UnifyAllAim
{
    [MenuItem("工具/全部动作统一为瞄准姿态", false, 1040)]
    [MenuItem("Tools/UnifyAllAim", false, 1040)]
    public static void Run()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Art/Animators/FemaleAnimator.controller");
        if (ctrl == null)
        {
            Debug.LogError("[全部瞄准] 找不到 FemaleAnimator.controller");
            return;
        }

        var sb = new StringBuilder();
        foreach (var layer in ctrl.layers)
        {
            bool isBase = layer.name == "Base Layer";
            var sm = layer.stateMachine;
            if (sm == null) continue;

            var keep = new List<AnimatorStateTransition>();
            foreach (var t in sm.anyStateTransitions)
            {
                string dst = t.destinationState != null ? t.destinationState.name : "";
                var conds = t.conditions;
                bool hasJump = HasCond(conds, "JumpStart");
                bool hasHit = HasCond(conds, "Hit");
                bool hasShoot = HasCond(conds, "Shoot");
                bool hasReload = HasCond(conds, "Reload");

                if (isBase)
                {
                    // 跳跃恒走 AimJump：去 IsAiming；非瞄准分支删除
                    if (dst == "AimJump" && hasJump)
                    {
                        t.conditions = RemoveCond(conds, "IsAiming");
                        sb.AppendLine($"[{layer.name}] JumpStart → AimJump 去掉 IsAiming 条件");
                        keep.Add(t);
                        continue;
                    }
                    if (dst == "JumpStart" && hasJump)
                    {
                        sb.AppendLine($"[{layer.name}] 删除非瞄准跳跃分支 → JumpStart");
                        continue;
                    }
                    // 受击：Base 层非瞄准 Hit 分支删除（由上层 AimHit 接管）
                    if (dst == "Hit" && hasHit)
                    {
                        sb.AppendLine($"[{layer.name}] 删除非瞄准受击分支 → Hit");
                        continue;
                    }
                }
                else
                {
                    // 射击恒走 AimShoot
                    if (dst == "AimShoot" && hasShoot)
                    {
                        t.conditions = RemoveCond(conds, "IsAiming");
                        sb.AppendLine($"[{layer.name}] Shoot → AimShoot 去掉 IsAiming 条件");
                        keep.Add(t);
                        continue;
                    }
                    if (dst == "Shoot" && hasShoot)
                    {
                        sb.AppendLine($"[{layer.name}] 删除非瞄准射击分支 → Shoot");
                        continue;
                    }
                    // 受击恒走上层 AimHit
                    if (dst == "AimHit" && hasHit)
                    {
                        t.conditions = RemoveCond(conds, "IsAiming");
                        sb.AppendLine($"[{layer.name}] Hit → AimHit 去掉 IsAiming 条件");
                        keep.Add(t);
                        continue;
                    }
                    // 换弹恒走 Reload
                    if (dst == "Reload" && hasReload)
                    {
                        t.conditions = RemoveCond(conds, "IsAiming");
                        sb.AppendLine($"[{layer.name}] Reload → Reload 去掉 IsAiming 条件");
                        keep.Add(t);
                        continue;
                    }
                }
                keep.Add(t);
            }
            sm.anyStateTransitions = keep.ToArray();
        }

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        sb.AppendLine("完成：所有动作统一为瞄准姿态（跳跃→AimJump、受击/射击/换弹→瞄准版；死亡无瞄准版保持原样）");
        Debug.Log(sb.ToString());
        try
        {
            System.IO.Directory.CreateDirectory("Assets/Screenshots");
            System.IO.File.WriteAllText("Assets/Screenshots/unify_all_aim.txt", sb.ToString());
        }
        catch { }
    }

    private static bool HasCond(AnimatorCondition[] conds, string param)
    {
        foreach (var c in conds)
            if (c.parameter == param) return true;
        return false;
    }

    private static AnimatorCondition[] RemoveCond(AnimatorCondition[] conds, string param)
    {
        var list = new List<AnimatorCondition>();
        foreach (var c in conds)
            if (c.parameter != param) list.Add(c);
        return list.ToArray();
    }
}

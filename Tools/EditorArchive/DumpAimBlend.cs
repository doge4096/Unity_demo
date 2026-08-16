using UnityEditor;
using UnityEngine;

/// <summary>
/// 运行时诊断：打印 Female Animator 每层当前状态 + 混合树实际输出的子动画（clip 名 + 权重）+ 关键参数。
/// 菜单：工具/打印瞄准混合方向（英文别名 Tools/DumpAimBlend）
/// 用途：验证瞄准走路时 AimX/AimZ 是否真正驱动 2D 混合树切换方向动画。
/// </summary>
public static class DumpAimBlend
{
    /// <summary>只读诊断：打印 FemaleAnimator 各层 AnyState/状态过渡与条件，用于“全部动作统一为瞄准姿态”改造</summary>
    [MenuItem("工具/诊断动画过渡", false, 1012)]
    [MenuItem("Tools/DumpTransitions", false, 1012)]
    public static void DumpTransitions()
    {
        var ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
            "Assets/Art/Animators/FemaleAnimator.controller");
        if (ctrl == null) { Debug.LogError("[过渡] 找不到控制器"); return; }
        var sb = new System.Text.StringBuilder();
        foreach (var layer in ctrl.layers)
        {
            sb.AppendLine($"===== 层 '{layer.name}' =====");
            sb.AppendLine("-- AnyState 过渡 --");
            foreach (var t in layer.stateMachine.anyStateTransitions)
            {
                var conds = new System.Collections.Generic.List<string>();
                foreach (var c in t.conditions)
                    conds.Add($"{c.parameter}(m{(int)c.mode})");
                string dst = t.destinationState != null ? t.destinationState.name
                    : (t.destinationStateMachine != null ? "SM:" + t.destinationStateMachine.name : "?");
                sb.AppendLine($"  [{string.Join(",", conds)}] -> {dst}");
            }
            sb.AppendLine("-- 状态内过渡 --");
            foreach (var cs in layer.stateMachine.states)
            {
                foreach (var t in cs.state.transitions)
                {
                    var conds = new System.Collections.Generic.List<string>();
                    foreach (var c in t.conditions)
                        conds.Add($"{c.parameter}(m{(int)c.mode})");
                    string dst = t.destinationState != null ? t.destinationState.name : "?";
                    sb.AppendLine($"  {cs.state.name} [{string.Join(",", conds)}] -> {dst}");
                }
            }
        }
        Debug.Log(sb.ToString());
        try { System.IO.File.WriteAllText("D:/tmp/transitions.txt", sb.ToString()); } catch { }
    }

    /// <summary>朝向诊断：打印角色 transform / Hips / Spine2 / WeaponSlot 的世界朝向，定位走路待机朝向偏左问题</summary>
    [MenuItem("工具/诊断角色朝向", false, 1011)]
    [MenuItem("Tools/FacingDiag", false, 1011)]
    public static void FacingDiag()
    {
        var female = GameObject.Find("Female");
        if (female == null)
        {
            Debug.LogError("[朝向] 找不到 Female（可能被 GameManager 隐藏）");
            return;
        }
        var anim = female.GetComponent<Animator>();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[朝向] 帧{Time.frameCount} 控制器={anim?.runtimeAnimatorController?.name} " +
                      $"角色yaw={female.transform.eulerAngles.y:F1} 角色forward={female.transform.forward.ToString("F2")}");

        if (anim != null)
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            sb.AppendLine($"  状态={StateName(anim, 0)} Speed={anim.GetFloat("Speed"):F2} IsAiming={anim.GetBool("IsAiming")} " +
                          $"AimX={anim.GetFloat("AimX"):F2} AimZ={anim.GetFloat("AimZ"):F2}");
            // 当前实际播放 clip 的根偏航（RootQ.y @ t=0），验证“根偏航导致枪口偏左”
            var clips = anim.GetCurrentAnimatorClipInfo(0);
            if (clips.Length > 0)
            {
                var clip = clips[0].clip;
                float rootQy0 = float.NaN, rootQyEnd = float.NaN;
                foreach (var b in UnityEditor.AnimationUtility.GetCurveBindings(clip))
                {
                    if (b.propertyName == "RootQ.y")
                    {
                        var c = UnityEditor.AnimationUtility.GetEditorCurve(clip, b);
                        rootQy0 = c.Evaluate(0f);
                        rootQyEnd = c.Evaluate(c.keys.Length > 1 ? c.keys[c.keys.Length - 1].time : 0f);
                        break;
                    }
                }
                sb.AppendLine($"  播放clip={clip.name} RootQ.y@0={rootQy0:F3} @末={rootQyEnd:F3}");
            }
            // 运行时控制器各状态挂的实际 motion（排查状态挂错 clip）
            var ctrl = anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            if (ctrl != null && ctrl.layers.Length > 0)
            {
                foreach (var cs in ctrl.layers[0].stateMachine.states)
                {
                    var m = cs.state.motion;
                    string mName = m == null ? "(null)" : m.name;
                    if (cs.state.name == "Idle" || cs.state.name == "Walk" || cs.state.name == "Run" ||
                        cs.state.name == "AimIdle" || cs.state.name == "AimWalk" || cs.state.name == "AimRun")
                        sb.AppendLine($"  状态[{cs.state.name}] motion={mName}");
                }
            }
            // 磁盘 AssetDatabase 加载的控制器各状态 motion（对比是否与运行时一致）
            var diskCtrl = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                "Assets/Art/Animators/FemaleAnimator.controller");
            if (diskCtrl != null && diskCtrl.layers.Length > 0)
            {
                foreach (var cs in diskCtrl.layers[0].stateMachine.states)
                {
                    var m = cs.state.motion;
                    string mName = m == null ? "(null)" : m.name;
                    if (cs.state.name == "Idle" || cs.state.name == "Walk" || cs.state.name == "Run" ||
                        cs.state.name == "AimIdle" || cs.state.name == "AimWalk" || cs.state.name == "AimRun")
                        sb.AppendLine($"  磁盘[{cs.state.name}] motion={mName}");
                }
            }
        }

        // 骨骼朝向（上半身）
        var hips = female.transform.Find("mixamorig1:Hips");
        var spine2 = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2");
        if (hips != null)
            sb.AppendLine($"  Hips yaw={hips.eulerAngles.y:F1} forward={hips.forward.ToString("F2")}");
        if (spine2 != null)
            sb.AppendLine($"  Spine2 yaw={spine2.eulerAngles.y:F1} forward={spine2.forward.ToString("F2")}");

        // 武器挂点/枪口方向
        var slot = GameObject.Find("WeaponSlot");
        if (slot != null)
            sb.AppendLine($"  WeaponSlot yaw={slot.transform.eulerAngles.y:F1} forward={slot.transform.forward.ToString("F2")}");
        else
            sb.AppendLine("  WeaponSlot 未找到");

        // 右手骨骼（枪通常挂在右手）
        var rHand = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:RightShoulder/mixamorig1:RightArm/mixamorig1:RightForeArm/mixamorig1:RightHand");
        if (rHand != null)
            sb.AppendLine($"  RightHand yaw={rHand.eulerAngles.y:F1} forward={rHand.forward.ToString("F2")}");
        var lHand = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:LeftShoulder/mixamorig1:LeftArm/mixamorig1:LeftForeArm/mixamorig1:LeftHand");
        if (lHand != null && rHand != null)
        {
            // 双手位置与“枪口”方向（右手→左手水平方向角，0=角色前方，负=左）
            Vector3 lh = lHand.position - female.transform.position;
            Vector3 rh = rHand.position - female.transform.position;
            float gunYaw = Mathf.Atan2(rHand.position.x - lHand.position.x, rHand.position.z - lHand.position.z) * Mathf.Rad2Deg;
            sb.AppendLine($"  左手本地={lh.ToString("F2")} 右手本地={rh.ToString("F2")} " +
                          $"双手连线水平角={gunYaw:F1}°（0=朝前,正=右,负=左）");
        }

        // 模型下所有带 Renderer 的物体（找枪 / 观察整体）
        foreach (var r in female.GetComponentsInChildren<Renderer>(true))
        {
            sb.AppendLine($"  渲染器: {r.transform.name} 父={r.transform.parent?.name} " +
                          $"类型={(r is SkinnedMeshRenderer ? "Skinned" : "Mesh")} 启用={r.enabled}");
        }

        Debug.Log(sb.ToString());
        try { System.IO.File.AppendAllText("D:/tmp/facing_diag.txt", sb.ToString() + "\n"); } catch { }
    }

    [MenuItem("工具/打印瞄准混合方向", false, 1008)]
    [MenuItem("Tools/DumpAimBlend", false, 1008)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null)
        {
            Debug.LogError("[混合] 找不到 Female（可能被 GameManager 隐藏，先激活 RangedPlayer）");
            return;
        }
        var anim = female.GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogError("[混合] Female 上没有 Animator");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[混合] 帧{Time.frameCount} 控制器={anim.runtimeAnimatorController?.name}");
        for (int li = 0; li < anim.layerCount; li++)
        {
            var st = anim.GetCurrentAnimatorStateInfo(li);
            var clips = anim.GetCurrentAnimatorClipInfo(li);
            sb.AppendLine($"  层{li} '{anim.GetLayerName(li)}' 状态='{StateName(anim, li)}' normalized={st.normalizedTime:F2}");
            if (clips.Length > 0)
            {
                foreach (var ci in clips)
                    sb.AppendLine($"    clip='{ci.clip.name}' weight={ci.weight:F3}");
            }
            else
            {
                sb.AppendLine("    无 clip（motion 为空？）");
            }
        }
        sb.AppendLine($"  IsAiming={anim.GetBool("IsAiming")} Speed={anim.GetFloat("Speed"):F2} " +
                      $"AimX={anim.GetFloat("AimX"):F2} AimZ={anim.GetFloat("AimZ"):F2}");
        Debug.Log(sb.ToString());
        try { System.IO.File.AppendAllText("D:/tmp/aim_blend.txt", sb.ToString() + "\n"); } catch { }
    }

    private static string StateName(Animator a, int layer)
    {
        if (a == null || a.runtimeAnimatorController == null) return "?";
        var info = a.GetCurrentAnimatorStateInfo(layer);
        var ctrl = a.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (ctrl == null || ctrl.layers.Length <= layer) return "?";
        foreach (var cs in ctrl.layers[layer].stateMachine.states)
        {
            if (cs.state.nameHash == info.shortNameHash)
                return cs.state.name;
        }
        return $"hash{info.shortNameHash % 10000}";
    }
}

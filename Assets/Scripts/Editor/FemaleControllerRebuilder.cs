using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

/// <summary>
/// 用 Unity API 重建 FemaleAnimator 控制器 —— 上下半身分层结构（参考近战 MeleeAnimator 方案）：
/// - Base Layer：全身。非瞄准（Idle/Walk/Run/跳跃/受击/死亡）+ 瞄准（AimIdle/AimWalk 四方向/AimRun/AimJump）
/// - UpperBody Layer：上半身（射击/受击/换弹），Avatar Mask 限制到 Spine 以上（不含 Hips，腿和髋部由 Base 层驱动）
/// 瞄准时移动直接套用 aimWalk 四方向动画（female_aimWalk*_fixed），由 IsAiming/AimX/AimZ/Speed 驱动；
/// 边走边射时 Base 层继续播瞄准移动，上层播射击动画（mask 只覆盖上身）；
/// 死亡时上层让位，全身死亡动画由 Base 层播
/// 原地重建（不删除资产），控制器 guid 不变，场景引用不断链
/// 菜单「工具/重建女性控制器」
/// </summary>
public static class FemaleControllerRebuilder
{
    private static AnimationClip Clip(string fbx) => AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/" + fbx);
    private static AnimationClip FixedClip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Fixed/" + name + ".anim");

    [MenuItem("工具/重建女性控制器")]
    [MenuItem("Tools/Rebuild Female Controller")]
    public static void Run()
    {
        var sb = new System.Text.StringBuilder();
        string outPath = "Assets/Art/Animators/FemaleAnimator.controller";
        try
        {
            // 禁止 Play Mode 中重建（运行时重建会清空资产导致场景 Animator 引用断链 → T-pose）
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[女性控制器重建] 请先退出 Play Mode 再重建");
                return;
            }

            // ===== 原地重建：加载现有控制器（guid 不变，场景引用不断链）=====
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(outPath);
            if (ctrl == null)
            {
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(outPath);
                sb.AppendLine("控制器不存在，已新建");
            }
            else
            {
                // 清空旧内容：删掉额外层（必须保留 Base 层，Unity 控制器至少要有一层）
                while (ctrl.layers.Length > 1) ctrl.RemoveLayer(ctrl.layers.Length - 1);
                if (ctrl.layers.Length == 0) ctrl.AddLayer("Base Layer"); // 兜底：异常残留 0 层时补回
                // 清空 Base 层状态机（删状态自动清掉关联过渡；AnyState 过渡单独清）
                var oldRoot = ctrl.layers[0].stateMachine;
                while (oldRoot.stateMachines.Length > 0) oldRoot.RemoveStateMachine(oldRoot.stateMachines[0].stateMachine);
                while (oldRoot.anyStateTransitions.Length > 0) oldRoot.RemoveAnyStateTransition(oldRoot.anyStateTransitions[0]);
                while (oldRoot.states.Length > 0) oldRoot.RemoveState(oldRoot.states[0].state);
                // 清空参数
                while (ctrl.parameters.Length > 0) ctrl.RemoveParameter(ctrl.parameters[0]);
                sb.AppendLine("控制器已存在，原地清空重建（guid 不变）");
            }

            // ===== 参数 =====
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("IsAiming", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("AimX", AnimatorControllerParameterType.Float);   // 瞄准移动方向（-1..1）
            ctrl.AddParameter("AimZ", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Reload", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("JumpStart", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("JumpLand", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("AnimSpeed", AnimatorControllerParameterType.Float);

            // ===== 上半身 Avatar Mask（Spine 以上+双臂+头，不含 Hips：腿和髋部由 Base 层驱动）=====
            var mask = CreateUpperBodyMask(outPath);

            // ===== Base Layer =====
            var root = ctrl.layers[0].stateMachine;

            AnimatorState NewState(string name, AnimationClip clip, string speedParam)
            {
                var s = root.AddState(name);
                if (clip != null) s.motion = clip;
                if (!string.IsNullOrEmpty(speedParam))
                {
                    s.speedParameterActive = true;
                    s.speedParameter = speedParam;
                }
                return s;
            }

            // 非瞄准（走路/跑步沿用男动画：female_Walk 质量差 → man_Walking_fixed/man_Run_fixed）
            var idle = NewState("Idle", Clip("female_Idle.fbx"), null);
            var walk = NewState("Walk", FixedClip("man_Walking_fixed"), "AnimSpeed");
            var run = NewState("Run", FixedClip("man_Run_fixed"), "AnimSpeed");
            var js = NewState("JumpStart", Clip("female_jumpstart.fbx"), "AnimSpeed");
            var jl = NewState("JumpLoop", Clip("female_floating.fbx"), "AnimSpeed");
            var jld = NewState("JumpLand", Clip("female_landing.fbx"), "AnimSpeed");
            var hit = NewState("Hit", Clip("female_HitReaction.fbx"), "AnimSpeed");
            var die = NewState("Die", Clip("female_death.fbx"), "AnimSpeed");

            // 瞄准移动：用户要求直接套用 aimWalk 四方向动画
            var aimIdle = NewState("AimIdle", Clip("female_aimIdle.fbx"), null);
            var aimWalkF = NewState("AimWalkF", FixedClip("female_aimWalk_fixed"), "AnimSpeed");
            var aimWalkB = NewState("AimWalkB", FixedClip("female_aimWalkBack_fixed"), "AnimSpeed");
            var aimWalkL = NewState("AimWalkL", FixedClip("female_aimWalkLeft_fixed"), "AnimSpeed");
            var aimWalkR = NewState("AimWalkR", FixedClip("female_aimWalkRight_fixed"), "AnimSpeed");
            var aimRun = NewState("AimRun", FixedClip("female_aimRun_fixed"), "AnimSpeed");
            var aimJump = NewState("AimJump", FixedClip("female_aimJump_fixed"), "AnimSpeed");
            root.defaultState = idle;

            // ---- AnyState 过渡（条件互斥；canTransitionToSelf=false 保证状态内条件持续满足时不会自重启）----
            void Any(AnimatorState dst, params (AnimatorConditionMode m, float t, string p)[] conds)
            {
                var tr = root.AddAnyStateTransition(dst);
                tr.duration = 0.05f;
                foreach (var c in conds) tr.AddCondition(c.m, c.t, c.p);
            }

            // 非瞄准移动（加 IsGrounded：防止空中 Speed 变化打断跳跃链）
            Any(idle, (AnimatorConditionMode.IfNot, 0f, "IsAiming"), (AnimatorConditionMode.Less, 0.1f, "Speed"), (AnimatorConditionMode.If, 0f, "IsGrounded"), (AnimatorConditionMode.IfNot, 0f, "IsDead"));
            Any(walk, (AnimatorConditionMode.IfNot, 0f, "IsAiming"), (AnimatorConditionMode.Greater, 0.1f, "Speed"), (AnimatorConditionMode.Less, 0.5f, "Speed"), (AnimatorConditionMode.If, 0f, "IsGrounded"), (AnimatorConditionMode.IfNot, 0f, "IsDead"));
            Any(run, (AnimatorConditionMode.IfNot, 0f, "IsAiming"), (AnimatorConditionMode.Greater, 0.5f, "Speed"), (AnimatorConditionMode.If, 0f, "IsGrounded"), (AnimatorConditionMode.IfNot, 0f, "IsDead"));

            // 瞄准移动：AimX/AimZ 判定方向（|AimX|或|AimZ|>0.5 且另一轴 <0.5，斜向输入保持原状态不抖动），Speed 区分走/跑
            Any(aimIdle, (AnimatorConditionMode.If, 0f, "IsAiming"), (AnimatorConditionMode.Less, 0.1f, "Speed"), (AnimatorConditionMode.If, 0f, "IsGrounded"), (AnimatorConditionMode.IfNot, 0f, "IsDead"));
            Any(aimWalkF, (AnimatorConditionMode.If, 0f, "IsAiming"), (AnimatorConditionMode.Greater, 0.1f, "Speed"), (AnimatorConditionMode.Less, 0.5f, "Speed"), (AnimatorConditionMode.Greater, 0.5f, "AimZ"), (AnimatorConditionMode.Less, 0.5f, "AimX"), (AnimatorConditionMode.Greater, -0.5f, "AimX"), (AnimatorConditionMode.If, 0f, "IsGrounded"), (AnimatorConditionMode.IfNot, 0f, "IsDead"));
            Any(aimWalkB, (AnimatorConditionMode.If, 0f, "IsAiming"), (AnimatorConditionMode.Greater, 0.1f, "Speed"), (AnimatorConditionMode.Less, 0.5f, "Speed"), (AnimatorConditionMode.Less, -0.5f, "AimZ"), (AnimatorConditionMode.Less, 0.5f, "AimX"), (AnimatorConditionMode.Greater, -0.5f, "AimX"), (AnimatorConditionMode.If, 0f, "IsGrounded"), (AnimatorConditionMode.IfNot, 0f, "IsDead"));
            Any(aimWalkL, (AnimatorConditionMode.If, 0f, "IsAiming"), (AnimatorConditionMode.Greater, 0.1f, "Speed"), (AnimatorConditionMode.Less, 0.5f, "Speed"), (AnimatorConditionMode.Less, -0.5f, "AimX"), (AnimatorConditionMode.Less, 0.5f, "AimZ"), (AnimatorConditionMode.Greater, -0.5f, "AimZ"), (AnimatorConditionMode.If, 0f, "IsGrounded"), (AnimatorConditionMode.IfNot, 0f, "IsDead"));
            Any(aimWalkR, (AnimatorConditionMode.If, 0f, "IsAiming"), (AnimatorConditionMode.Greater, 0.1f, "Speed"), (AnimatorConditionMode.Less, 0.5f, "Speed"), (AnimatorConditionMode.Greater, 0.5f, "AimX"), (AnimatorConditionMode.Less, 0.5f, "AimZ"), (AnimatorConditionMode.Greater, -0.5f, "AimZ"), (AnimatorConditionMode.If, 0f, "IsGrounded"), (AnimatorConditionMode.IfNot, 0f, "IsDead"));
            Any(aimRun, (AnimatorConditionMode.If, 0f, "IsAiming"), (AnimatorConditionMode.Greater, 0.5f, "Speed"), (AnimatorConditionMode.If, 0f, "IsGrounded"), (AnimatorConditionMode.IfNot, 0f, "IsDead"));

            // 跳跃：瞄准走 AimJump（瞄准跳跃动画），非瞄准走 JumpStart
            Any(aimJump, (AnimatorConditionMode.If, 0f, "JumpStart"), (AnimatorConditionMode.If, 0f, "IsAiming"), (AnimatorConditionMode.IfNot, 0f, "IsDead"));
            Any(js, (AnimatorConditionMode.If, 0f, "JumpStart"), (AnimatorConditionMode.IfNot, 0f, "IsAiming"), (AnimatorConditionMode.IfNot, 0f, "IsDead"));

            // 受击 / 死亡（瞄准受击走上层 AimHit，Base 层继续播瞄准移动，腿不受影响）
            Any(hit, (AnimatorConditionMode.If, 0f, "Hit"), (AnimatorConditionMode.IfNot, 0f, "IsAiming"), (AnimatorConditionMode.IfNot, 0f, "IsDead"));
            Any(die, (AnimatorConditionMode.If, 0f, "Die"));

            // 跳跃链显式过渡（exit time 自动推进）
            void ExitTo(AnimatorState from, AnimatorState to, float exitTime)
            {
                var t = from.AddTransition(to);
                t.hasExitTime = true;
                t.exitTime = exitTime;
                t.duration = 0.1f;
            }
            ExitTo(js, jl, 0.6f);
            jl.AddTransition(jld).AddCondition(AnimatorConditionMode.If, 0, "JumpLand");
            ExitTo(jld, idle, 0.95f);
            jld.AddTransition(walk).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            jld.AddTransition(run).AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
            // 瞄准跳跃落地：按是否还瞄准分别回 AimIdle/Idle；动画播完兜底回 AimIdle
            aimJump.AddTransition(aimIdle).AddCondition(AnimatorConditionMode.If, 0, "JumpLand");
            var tJumpToIdle = aimJump.AddTransition(idle);
            tJumpToIdle.AddCondition(AnimatorConditionMode.If, 0, "JumpLand");
            tJumpToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAiming");
            ExitTo(aimJump, aimIdle, 0.95f);

            // ===== UpperBody Layer：上半身（Avatar Mask 限制）=====
            ctrl.AddLayer("UpperBody");
            var upper = ctrl.layers[1];
            upper.avatarMask = mask;
            upper.blendingMode = AnimatorLayerBlendingMode.Override;
            upper.defaultWeight = 1f;
            ctrl.layers[1] = upper;
            // 兜底：上面 API 写回在实测中未生效（日志验证 mask=NULL/weight=0）→ 用 SerializedObject 直写序列化字段（m_DefaultWeight/m_Mask）
            var so = new SerializedObject(ctrl);
            var layersProp = so.FindProperty("m_AnimatorLayers");
            var upperProp = layersProp.GetArrayElementAtIndex(1);
            upperProp.FindPropertyRelative("m_DefaultWeight").floatValue = 1f;
            upperProp.FindPropertyRelative("m_Mask").objectReferenceValue = mask;
            so.ApplyModifiedProperties();
            var sm = ctrl.layers[1].stateMachine;

            AnimatorState NewUpper(string name, AnimationClip clip)
            {
                var s = sm.AddState(name);
                if (clip != null) s.motion = clip;
                return s;
            }

            var empty = NewUpper("Empty", null);                          // 无动画：完全透传 Base 层（瞄准持枪姿势由 Base 层 aimIdle/aimWalk 提供）
            var aimShoot = NewUpper("AimShoot", Clip("female_aimShoot.fbx"));   // 瞄准射击：姿势与 aimIdle 连续（Front-Back 同为 -0.23），有微小后坐
            var shoot = NewUpper("Shoot", FixedClip("female_shoot_fixed"));     // 非瞄准射击（腰射）
            var aimHit = NewUpper("AimHit", FixedClip("female_aimHit_fixed"));
            var reload = NewUpper("Reload", FixedClip("female_reload_fixed"));
            sm.defaultState = empty;

            // 上层全部用 AnyState 进入（trigger 一次性触发，不会自重启）；播完 exit 回 Empty；死亡让位
            void UpperAny(AnimatorState dst, params (AnimatorConditionMode m, float t, string p)[] conds)
            {
                var tr = sm.AddAnyStateTransition(dst);
                tr.duration = 0.05f;
                foreach (var c in conds) tr.AddCondition(c.m, c.t, c.p);
            }
            UpperAny(aimShoot, (AnimatorConditionMode.If, 0f, "Shoot"), (AnimatorConditionMode.If, 0f, "IsAiming"), (AnimatorConditionMode.IfNot, 0f, "IsDead"));
            UpperAny(shoot, (AnimatorConditionMode.If, 0f, "Shoot"), (AnimatorConditionMode.IfNot, 0f, "IsAiming"), (AnimatorConditionMode.IfNot, 0f, "IsDead"));
            UpperAny(aimHit, (AnimatorConditionMode.If, 0f, "Hit"), (AnimatorConditionMode.If, 0f, "IsAiming"), (AnimatorConditionMode.IfNot, 0f, "IsDead"));
            UpperAny(reload, (AnimatorConditionMode.If, 0f, "Reload"), (AnimatorConditionMode.If, 0f, "IsAiming"), (AnimatorConditionMode.IfNot, 0f, "IsDead"));
            ExitTo(aimShoot, empty, 0.95f);
            ExitTo(shoot, empty, 0.95f);
            ExitTo(aimHit, empty, 0.95f);
            ExitTo(reload, empty, 0.95f);
            // 死亡让位：上半身动作立即让给 Base 层死亡动画
            aimShoot.AddTransition(empty).AddCondition(AnimatorConditionMode.If, 0, "IsDead");
            shoot.AddTransition(empty).AddCondition(AnimatorConditionMode.If, 0, "IsDead");
            aimHit.AddTransition(empty).AddCondition(AnimatorConditionMode.If, 0, "IsDead");
            reload.AddTransition(empty).AddCondition(AnimatorConditionMode.If, 0, "IsDead");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            sb.AppendLine("重建完成: " + outPath);
            sb.AppendLine($"参数数={ctrl.parameters.Length} 层数={ctrl.layers.Length}");
            sb.AppendLine($"Base 层状态={root.states.Length} 上层状态={sm.states.Length}");
            sb.AppendLine($"上层 Mask={(ctrl.layers[1].avatarMask != null ? ctrl.layers[1].avatarMask.name : "NULL")} 权重={ctrl.layers[1].defaultWeight}");
            sb.AppendLine("== Base 层 ==");
            foreach (var s in root.states)
                sb.AppendLine($"  {s.state.name} -> {(s.state.motion != null ? s.state.motion.name : "NULL")}");
            sb.AppendLine("== UpperBody 层 ==");
            foreach (var s in sm.states)
                sb.AppendLine($"  {s.state.name} -> {(s.state.motion != null ? s.state.motion.name : "NULL")}");
            if (mask != null)
            {
                sb.AppendLine("== Mask 骨骼 ==");
                for (int i = 0; i < mask.transformCount; i++)
                    sb.AppendLine($"  {mask.GetTransformPath(i)} = {mask.GetTransformActive(i)}");
            }
            // 诊断四方向瞄准走路动画的腿部曲线值域（确认 fixed 版腿部有效，避免瞄准走路腿不动）
            sb.AppendLine("== 瞄准动画腿部值域诊断 ==");
            AppendMuscleRange(sb, FixedClip("female_aimWalk_fixed"), "Left Upper Leg Front-Back");
            AppendMuscleRange(sb, FixedClip("female_aimWalk_fixed"), "Left Lower Leg Stretch");
            AppendMuscleRange(sb, FixedClip("female_aimWalkBack_fixed"), "Left Upper Leg Front-Back");
            AppendMuscleRange(sb, FixedClip("female_aimWalkLeft_fixed"), "Left Upper Leg Front-Back");
            AppendMuscleRange(sb, FixedClip("female_aimWalkRight_fixed"), "Left Upper Leg Front-Back");
            AppendMuscleRange(sb, FixedClip("female_aimRun_fixed"), "Left Upper Leg Front-Back");
            AppendMuscleRange(sb, Clip("female_aimIdle.fbx"), "Left Upper Leg Front-Back");
        }
        catch (System.Exception e)
        {
            sb.AppendLine("异常: " + e);
        }
        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/female_controller_rebuild.txt", sb.ToString());
        Debug.Log("[女性控制器重建] " + sb.ToString());
    }

    /// <summary>输出指定动画指定 muscle 曲线的值域（判断动画是否有效）</summary>
    private static void AppendMuscleRange(System.Text.StringBuilder sb, AnimationClip clip, string muscle)
    {
        if (clip == null) { sb.AppendLine($"  [clip null] {muscle}"); return; }
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.propertyName == muscle)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                float min = float.MaxValue, max = float.MinValue;
                foreach (var k in curve.keys) { min = Mathf.Min(min, k.value); max = Mathf.Max(max, k.value); }
                sb.AppendLine($"  {clip.name} {muscle}: 值域=[{min:F3},{max:F3}] 帧数={curve.keys.Length}");
                return;
            }
        }
        sb.AppendLine($"  {clip.name} {muscle}: [无此曲线]");
    }

    /// <summary>创建上半身 Avatar Mask（Spine 以上+双臂+头，不含 Hips，腿脚留给 Base 层）</summary>
    private static AvatarMask CreateUpperBodyMask(string controllerPath)
    {
        // 从 Female.fbx 加载骨骼层级（与场景 Female 模型同一套 mixamorig1: 骨架）
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Female.fbx");
        if (fbx == null)
        {
            Debug.LogError("[女性控制器重建] 找不到 Female.fbx，无法创建 Mask");
            return null;
        }

        var mask = new AvatarMask();
        // 与近战 UpperBody.mask 一致：不含 Hips（Hips 留给 Base 层驱动腿），从 Spine 开始覆盖上身
        string[] upperBones =
        {
            "mixamorig1:Spine", "mixamorig1:Spine1", "mixamorig1:Spine2",
            "mixamorig1:Neck", "mixamorig1:Head",
            "mixamorig1:LeftShoulder", "mixamorig1:LeftArm", "mixamorig1:LeftForeArm", "mixamorig1:LeftHand",
            "mixamorig1:RightShoulder", "mixamorig1:RightArm", "mixamorig1:RightForeArm", "mixamorig1:RightHand",
        };
        foreach (var bone in upperBones)
        {
            var t = FindBone(fbx.transform, bone);
            if (t != null)
                mask.AddTransformPath(t, false); // AddTransformPath 默认激活该骨骼；腿脚未加入 → 下层驱动
            else
                Debug.LogWarning("[女性控制器重建] Mask 找不到骨骼: " + bone);
        }

        string maskPath = "Assets/Art/Masks/FemaleUpperBody.mask";
        var existing = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mask, existing);
            EditorUtility.SetDirty(existing);
        }
        else
        {
            AssetDatabase.CreateAsset(mask, maskPath);
        }
        var savedMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if (savedMask != null) savedMask.name = "FemaleUpperBody"; // new AvatarMask() 的 name 默认空，补上资产名
        AssetDatabase.SaveAssets();
        return savedMask;
    }

    /// <summary>按名字递归查找骨骼 Transform</summary>
    private static Transform FindBone(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindBone(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}

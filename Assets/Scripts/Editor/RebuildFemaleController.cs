using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 全量重建 FemaleAnimator 控制器（保持文件路径与 guid，场景引用不失效）
///
/// 背景：上一次修复（FixFemaleBaseTransitions）对已自动注册的过渡对象重复手动
/// AddObjectToAsset，触发 "Adding asset to object failed"，过渡对象处于"半注册"状态，
/// Unity 重新加载时不加载这些对象 → 状态机运行时没有过渡 → 状态永远停 Idle → 动画不切换。
///
/// 正确模式（参考 ControllerRebuilder 成功重建 MeleeAnimatorRebuilt）：
/// - AddState / AddTransition / AddAnyStateTransition / AddParameter 都会自动注册子对象，绝不手动 AddObjectToAsset
/// - 只有 new 出来的 AnimatorStateMachine（用于新层）需要手动 AddObjectToAsset 注册
/// </summary>
public static class RebuildFemaleController
{
    private const string ControllerPath = "Assets/Art/Animators/FemaleAnimator.controller";

    [MenuItem("工具/全量重建女性控制器", false, 1003)]
    [MenuItem("Tools/RebuildFemaleController", false, 1003)] // 英文别名给 MCP 调用
    public static void Run()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[重建女性] 找不到控制器: {ControllerPath}");
            return;
        }

        // ========== 1. 从旧对象读取数据（动画引用 + 速度参数绑定） ==========
        var clips = new Dictionary<string, AnimationClip>();   // 状态名 -> 动画
        var speedParams = new Dictionary<string, string>();    // 状态名 -> 速度参数
        foreach (var layer in controller.layers)
        {
            foreach (var cs in layer.stateMachine.states)
            {
                if (cs.state == null) continue;
                clips[cs.state.name] = cs.state.motion as AnimationClip;
                if (cs.state.speedParameterActive)
                    speedParams[cs.state.name] = cs.state.speedParameter;
            }
        }
        var upperMask = controller.layers.Length > 1 ? controller.layers[1].avatarMask : null;

        // ========== 2. 删除全部旧子对象（含损坏的半注册过渡） ==========
        var subs = AssetDatabase.LoadAllAssetsAtPath(ControllerPath);
        int removed = 0;
        foreach (var o in subs)
        {
            if (o == null || o == controller) continue;
            AssetDatabase.RemoveObjectFromAsset(o);
            Object.DestroyImmediate(o, true);
            removed++;
        }

        // ========== 3. 清空参数与层 ==========
        controller.parameters = new AnimatorControllerParameter[0];
        var so = new SerializedObject(controller);
        so.FindProperty("m_AnimatorLayers").ClearArray();
        so.ApplyModifiedProperties();

        // ========== 4. 添加 13 个参数 ==========
        void AddParam(string name, AnimatorControllerParameterType type)
            => controller.AddParameter(name, type);
        AddParam("Speed", AnimatorControllerParameterType.Float);
        AddParam("IsAiming", AnimatorControllerParameterType.Bool);
        AddParam("AimX", AnimatorControllerParameterType.Float);
        AddParam("AimZ", AnimatorControllerParameterType.Float);
        AddParam("Shoot", AnimatorControllerParameterType.Trigger);
        AddParam("Reload", AnimatorControllerParameterType.Trigger);
        AddParam("Hit", AnimatorControllerParameterType.Trigger);
        AddParam("Die", AnimatorControllerParameterType.Trigger);
        AddParam("IsDead", AnimatorControllerParameterType.Bool);
        AddParam("JumpStart", AnimatorControllerParameterType.Trigger);
        AddParam("JumpLand", AnimatorControllerParameterType.Trigger);
        AddParam("IsGrounded", AnimatorControllerParameterType.Bool);
        AddParam("AnimSpeed", AnimatorControllerParameterType.Float);

        // AnimSpeed 默认值设为 1：13 个状态绑定它作速度参数，
        // 默认 0 会让动画播放速度=0 冻结在首帧（PlayerController 移动时才赋值）
        {
            var ps = controller.parameters;
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].name == "AnimSpeed") ps[i].defaultFloat = 1f;
            controller.parameters = ps;
        }

        // ========== 5. Base Layer：状态机 + 15 状态 ==========
        var rootSm = new AnimatorStateMachine { name = "Base Layer" };
        AssetDatabase.AddObjectToAsset(rootSm, controller); // 新状态机必须手动注册
        controller.AddLayer(new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f, stateMachine = rootSm });

        // 新建状态：动画引用旧数据，速度参数绑定旧数据（无则 null）
        AnimatorState NewState(AnimatorStateMachine sm, string name)
        {
            var s = sm.AddState(name);
            if (clips.TryGetValue(name, out var clip) && clip != null) s.motion = clip;
            if (speedParams.TryGetValue(name, out var sp)) { s.speedParameterActive = true; s.speedParameter = sp; }
            return s;
        }

        var idle = NewState(rootSm, "Idle");
        var walk = NewState(rootSm, "Walk");
        var run = NewState(rootSm, "Run");
        var aimIdle = NewState(rootSm, "AimIdle");
        var aimWalkF = NewState(rootSm, "AimWalkF");
        var aimWalkB = NewState(rootSm, "AimWalkB");
        var aimWalkL = NewState(rootSm, "AimWalkL");
        var aimWalkR = NewState(rootSm, "AimWalkR");
        var aimRun = NewState(rootSm, "AimRun");
        var aimJump = NewState(rootSm, "AimJump");
        var jumpStart = NewState(rootSm, "JumpStart");
        var jumpLoop = NewState(rootSm, "JumpLoop");
        var jumpLand = NewState(rootSm, "JumpLand");
        var hit = NewState(rootSm, "Hit");
        var die = NewState(rootSm, "Die");
        rootSm.defaultState = idle;

        var states = new Dictionary<string, AnimatorState>
        {
            ["Idle"] = idle, ["Walk"] = walk, ["Run"] = run,
            ["AimIdle"] = aimIdle, ["AimWalkF"] = aimWalkF, ["AimWalkB"] = aimWalkB,
            ["AimWalkL"] = aimWalkL, ["AimWalkR"] = aimWalkR, ["AimRun"] = aimRun,
            ["AimJump"] = aimJump, ["JumpStart"] = jumpStart, ["JumpLoop"] = jumpLoop,
            ["JumpLand"] = jumpLand, ["Hit"] = hit, ["Die"] = die,
        };

        // ========== 6. Base Layer 状态间过渡（74 条） ==========
        // 条件速记：nAim=未瞄准, Aim=瞄准, SIdle=Speed<0.1, SWalk=0.1<=Speed<0.5, SRun=Speed>=0.5
        // F/B/L/R = AimZ/AimX 方向区间（与旧 AnyState 条件一致）
        AnimatorStateTransition Trans(AnimatorState from, AnimatorState to, float duration = 0.05f)
        {
            var t = from.AddTransition(to); // AddTransition 自动注册子对象
            t.duration = duration;
            return t;
        }
        // 通用：带条件列表的过渡
        void Spec(AnimatorState from, AnimatorState to, System.Action<AnimatorStateTransition> conds, float duration = 0.05f)
        {
            var t = Trans(from, to, duration);
            conds?.Invoke(t);
        }
        // 瞄准+走路+方向
        System.Action<AnimatorStateTransition> AimWalkDir(string axis, float lo, float hi)
        {
            return t =>
            {
                t.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming");
                t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                t.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed");
                t.AddCondition(AnimatorConditionMode.Greater, lo, axis);
                t.AddCondition(AnimatorConditionMode.Less, hi, axis);
                string other = axis == "AimZ" ? "AimX" : "AimZ";
                t.AddCondition(AnimatorConditionMode.Greater, -0.5f, other);
                t.AddCondition(AnimatorConditionMode.Less, 0.5f, other);
            };
        }
        System.Action<AnimatorStateTransition> AimRunCond()
        {
            return t =>
            {
                t.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming");
                t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
            };
        }

        // --- 按规格表重建（与修复脚本 74 条一致） ---
        // Idle
        Spec(idle, walk, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); t.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed"); });
        Spec(idle, run, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed"); });
        Spec(idle, aimIdle, t => { t.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming"); });
        // Walk
        Spec(walk, idle, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(walk, run, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed"); });
        Spec(walk, aimWalkF, AimWalkDir("AimZ", 0.5f, 999f));
        Spec(walk, aimWalkB, AimWalkDir("AimZ", -999f, -0.5f));
        Spec(walk, aimWalkL, AimWalkDir("AimX", -999f, -0.5f));
        Spec(walk, aimWalkR, AimWalkDir("AimX", 0.5f, 999f));
        Spec(walk, aimRun, AimRunCond());
        // Run
        Spec(run, idle, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(run, walk, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); t.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed"); });
        Spec(run, aimRun, AimRunCond());
        Spec(run, aimWalkF, AimWalkDir("AimZ", 0.5f, 999f));
        Spec(run, aimWalkB, AimWalkDir("AimZ", -999f, -0.5f));
        Spec(run, aimWalkL, AimWalkDir("AimX", -999f, -0.5f));
        Spec(run, aimWalkR, AimWalkDir("AimX", 0.5f, 999f));
        // AimIdle
        Spec(aimIdle, idle, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(aimIdle, walk, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); t.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed"); });
        Spec(aimIdle, run, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed"); });
        Spec(aimIdle, aimWalkF, AimWalkDir("AimZ", 0.5f, 999f));
        Spec(aimIdle, aimWalkB, AimWalkDir("AimZ", -999f, -0.5f));
        Spec(aimIdle, aimWalkL, AimWalkDir("AimX", -999f, -0.5f));
        Spec(aimIdle, aimWalkR, AimWalkDir("AimX", 0.5f, 999f));
        Spec(aimIdle, aimRun, AimRunCond());
        // AimWalkF
        Spec(aimWalkF, aimIdle, t => { t.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(aimWalkF, aimRun, AimRunCond());
        Spec(aimWalkF, aimWalkB, AimWalkDir("AimZ", -999f, -0.5f));
        Spec(aimWalkF, aimWalkL, AimWalkDir("AimX", -999f, -0.5f));
        Spec(aimWalkF, aimWalkR, AimWalkDir("AimX", 0.5f, 999f));
        Spec(aimWalkF, idle, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(aimWalkF, walk, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); t.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed"); });
        Spec(aimWalkF, run, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed"); });
        // AimWalkB
        Spec(aimWalkB, aimIdle, t => { t.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(aimWalkB, aimRun, AimRunCond());
        Spec(aimWalkB, aimWalkF, AimWalkDir("AimZ", 0.5f, 999f));
        Spec(aimWalkB, aimWalkL, AimWalkDir("AimX", -999f, -0.5f));
        Spec(aimWalkB, aimWalkR, AimWalkDir("AimX", 0.5f, 999f));
        Spec(aimWalkB, idle, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(aimWalkB, walk, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); t.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed"); });
        Spec(aimWalkB, run, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed"); });
        // AimWalkL
        Spec(aimWalkL, aimIdle, t => { t.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(aimWalkL, aimRun, AimRunCond());
        Spec(aimWalkL, aimWalkF, AimWalkDir("AimZ", 0.5f, 999f));
        Spec(aimWalkL, aimWalkB, AimWalkDir("AimZ", -999f, -0.5f));
        Spec(aimWalkL, aimWalkR, AimWalkDir("AimX", 0.5f, 999f));
        Spec(aimWalkL, idle, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(aimWalkL, walk, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); t.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed"); });
        Spec(aimWalkL, run, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed"); });
        // AimWalkR
        Spec(aimWalkR, aimIdle, t => { t.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(aimWalkR, aimRun, AimRunCond());
        Spec(aimWalkR, aimWalkF, AimWalkDir("AimZ", 0.5f, 999f));
        Spec(aimWalkR, aimWalkB, AimWalkDir("AimZ", -999f, -0.5f));
        Spec(aimWalkR, aimWalkL, AimWalkDir("AimX", -999f, -0.5f));
        Spec(aimWalkR, idle, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(aimWalkR, walk, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); t.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed"); });
        Spec(aimWalkR, run, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed"); });
        // AimRun
        Spec(aimRun, aimIdle, t => { t.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(aimRun, aimWalkF, AimWalkDir("AimZ", 0.5f, 999f));
        Spec(aimRun, aimWalkB, AimWalkDir("AimZ", -999f, -0.5f));
        Spec(aimRun, aimWalkL, AimWalkDir("AimX", -999f, -0.5f));
        Spec(aimRun, aimWalkR, AimWalkDir("AimX", 0.5f, 999f));
        Spec(aimRun, run, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed"); });
        Spec(aimRun, walk, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); t.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed"); });
        Spec(aimRun, idle, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });

        // 跳跃系列
        {
            var t = Trans(jumpStart, jumpLoop, 0.1f);
            t.hasExitTime = true; t.exitTime = 0.6f;
            var t2 = Trans(jumpLoop, jumpLand, 0.1f);
            t2.AddCondition(AnimatorConditionMode.If, 0f, "JumpLand");
            var t3 = Trans(jumpLand, idle, 0.1f);
            t3.hasExitTime = true; t3.exitTime = 0.95f;
            var t4 = Trans(jumpLand, walk, 0.1f);
            t4.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var t5 = Trans(jumpLand, run, 0.1f);
            t5.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
            var t6 = Trans(aimJump, aimIdle, 0.1f);
            t6.AddCondition(AnimatorConditionMode.If, 0f, "JumpLand");
            var t7 = Trans(aimJump, idle, 0.1f);
            t7.AddCondition(AnimatorConditionMode.If, 0f, "JumpLand");
            t7.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming");
            var t8 = Trans(aimJump, aimIdle, 0.1f);
            t8.hasExitTime = true; t8.exitTime = 0.95f;
        }

        // Hit 退出（播完回 Idle）
        {
            var t = Trans(hit, idle, 0.1f);
            t.hasExitTime = true; t.exitTime = 0.9f;
        }

        // ========== 7. Base Layer AnyState 过渡（4 条 trigger） ==========
        {
            var t = rootSm.AddAnyStateTransition(jumpStart);
            t.duration = 0.05f;
            t.AddCondition(AnimatorConditionMode.If, 0f, "JumpStart");
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming");
            var t2 = rootSm.AddAnyStateTransition(aimJump);
            t2.duration = 0.05f;
            t2.AddCondition(AnimatorConditionMode.If, 0f, "JumpStart");
            t2.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming");
            var t3 = rootSm.AddAnyStateTransition(hit);
            t3.duration = 0.05f;
            t3.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            t3.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming");
            var t4 = rootSm.AddAnyStateTransition(die);
            t4.duration = 0.05f;
            t4.AddCondition(AnimatorConditionMode.If, 0f, "Die");
        }

        // ========== 8. UpperBody Layer：状态机 + 5 状态 ==========
        var upperSm = new AnimatorStateMachine { name = "UpperBody" };
        AssetDatabase.AddObjectToAsset(upperSm, controller); // 新状态机必须手动注册
        controller.AddLayer(new AnimatorControllerLayer
        {
            name = "UpperBody",
            defaultWeight = 1f,
            stateMachine = upperSm,
            avatarMask = upperMask,
        });

        var empty = NewState(upperSm, "Empty");
        var aimShoot = NewState(upperSm, "AimShoot");
        var shoot = NewState(upperSm, "Shoot");
        var aimHit = NewState(upperSm, "AimHit");
        var reload = NewState(upperSm, "Reload");
        upperSm.defaultState = empty;

        // UpperBody AnyState（4 条，条件与原文件一致，含 IsDead 让位）
        {
            var t = upperSm.AddAnyStateTransition(aimShoot);
            t.duration = 0.05f;
            t.AddCondition(AnimatorConditionMode.If, 0f, "Shoot");
            t.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming");
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
            var t2 = upperSm.AddAnyStateTransition(shoot);
            t2.duration = 0.05f;
            t2.AddCondition(AnimatorConditionMode.If, 0f, "Shoot");
            t2.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming");
            t2.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
            var t3 = upperSm.AddAnyStateTransition(aimHit);
            t3.duration = 0.05f;
            t3.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            t3.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming");
            t3.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
            var t4 = upperSm.AddAnyStateTransition(reload);
            t4.duration = 0.05f;
            t4.AddCondition(AnimatorConditionMode.If, 0f, "Reload");
            t4.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming");
            t4.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
        }

        // UpperBody 状态间过渡（播完回 Empty / 死亡让位）
        void ToEmpty(AnimatorState from)
        {
            var t1 = Trans(from, empty, 0.1f);
            t1.hasExitTime = true; t1.exitTime = 0.95f;
            var t2 = Trans(from, empty, 0.1f);
            t2.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
        }
        ToEmpty(aimShoot);
        ToEmpty(shoot);
        ToEmpty(aimHit);
        ToEmpty(reload);

        // ========== 9. 保存 ==========
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // ========== 10. 验证并输出统计 ==========
        int totalTrans = 0;
        int totalAny = 0;
        foreach (var layer in controller.layers)
        {
            foreach (var cs in layer.stateMachine.states)
                totalTrans += cs.state.transitions.Length;
            totalAny += layer.stateMachine.anyStateTransitions.Length;
        }
        Debug.Log($"[重建女性] 完成！删除旧子对象 {removed} 个 | 参数 {controller.parameters.Length} | 层 {controller.layers.Length} | " +
                  $"Base状态 {controller.layers[0].stateMachine.states.Length} | 过渡 {totalTrans} 条 | AnyState {totalAny} 条。请 Play Mode 验证。");
    }
}

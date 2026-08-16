using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 全量重建 FemaleAnimator 控制器 Base Layer 的过渡结构（幂等，可重复执行）
///
/// 根因：移动切换（Idle/Walk/Run/AimIdle/AimWalkF/B/L/R/AimRun）全部用 AnyState 过渡实现，
/// 而 AnyState 过渡在"条件持续满足 + 目标 = 当前状态 + CanTransitionToSelf=1"时，
/// 每帧都会重新进入当前状态 → 动画永远被重置到开头 → 角色抽搐、动画一直重复播放开头。
/// 且 AnyState 移动过渡会 0.05s 内打断 Hit 等一次性动画（Hit 状态自身又没有退出过渡）。
///
/// 修复：移动切换全部改为状态间过渡（与 MeleeAnimator 已验证的结构一致），
/// AnyState 只保留 trigger 型（Hit/Die/JumpStart/AimJump），Hit 加 HasExitTime 退出过渡。
///
/// 注意：过渡对象必须 AssetDatabase.AddObjectToAsset 注册为控制器子资产，
/// 否则序列化时 fileID=0（悬空引用，加载后为占位对象，无法用 null 判断清理）。
/// </summary>
public static class FixFemaleBaseTransitions
{
    private const string ControllerPath = "Assets/Art/Animators/FemaleAnimator.controller";

    // 移动状态：这 9 个状态的切换从 AnyState 改为状态间过渡
    private static readonly HashSet<string> MoveStates = new HashSet<string>
    {
        "Idle", "Walk", "Run",
        "AimIdle", "AimWalkF", "AimWalkB", "AimWalkL", "AimWalkR", "AimRun",
    };

    [MenuItem("工具/修复女性动画控制器（AnyState→状态间过渡）")]
    [MenuItem("Tools/FixFemaleBaseTransitions")] // 英文别名给自动化工具调用（MCP 服务端不支持中文路径）
    public static void Fix()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[修复女性动画] 找不到控制器: {ControllerPath}");
            return;
        }

        var sm = controller.layers[0].stateMachine; // Base Layer

        // ---------- 1. 清空 Base Layer 全部过渡（状态内 + AnyState），全量重建 ----------
        int removed = 0;
        var allStates = new List<AnimatorState>();
        foreach (var cs in sm.states)
            allStates.Add(cs.state);

        foreach (var st in allStates)
        {
            var trans = new List<AnimatorStateTransition>(st.transitions);
            foreach (var t in trans)
            {
                st.RemoveTransition(t);
                Object.DestroyImmediate(t, true);
                removed++;
            }
        }
        var anyTrans = new List<AnimatorStateTransition>(sm.anyStateTransitions);
        foreach (var t in anyTrans)
        {
            var keep = new List<AnimatorStateTransition>(sm.anyStateTransitions);
            keep.Remove(t);
            sm.anyStateTransitions = keep.ToArray();
            Object.DestroyImmediate(t, true);
            removed++;
        }

        // ---------- 2. 收集状态引用 ----------
        var states = new Dictionary<string, AnimatorState>();
        foreach (var cs in sm.states)
            states[cs.state.name] = cs.state;

        foreach (var name in MoveStates)
            if (!states.ContainsKey(name))
                Debug.LogError($"[修复女性动画] 找不到状态: {name}（请检查控制器）");
        if (!states.ContainsKey("Hit") || !states.ContainsKey("JumpStart") || !states.ContainsKey("JumpLoop")
            || !states.ContainsKey("JumpLand") || !states.ContainsKey("AimJump") || !states.ContainsKey("Die"))
        {
            Debug.LogError("[修复女性动画] 缺少必要状态，已中止");
            return;
        }

        // ---------- 3. 重建状态间过渡（移动 + 跳跃 + Hit 退出） ----------
        int added = 0;
        foreach (var spec in TransitionSpecs)
        {
            if (!states.ContainsKey(spec.From) || !states.ContainsKey(spec.To)) continue;
            var t = MakeTransition(states[spec.To], spec.Duration, spec.HasExitTime, spec.ExitTime);
            foreach (var c in spec.Conditions) t.AddCondition(c.Mode, c.Threshold, c.Param);
            // 关键：必须 AddObjectToAsset 注册为 controller 子资产，否则序列化丢失（fileID=0 悬空）
            AssetDatabase.AddObjectToAsset(t, controller);
            states[spec.From].AddTransition(t);
            added++;
        }

        // ---------- 4. 重建 4 条 trigger 型 AnyState 过渡 ----------
        int anyAdded = 0;
        foreach (var spec in AnyStateSpecs)
        {
            if (!states.ContainsKey(spec.To)) continue;
            var t = sm.AddAnyStateTransition(states[spec.To]);
            t.duration = spec.Duration;
            foreach (var c in spec.Conditions) t.AddCondition(c.Mode, c.Threshold, c.Param);
            AssetDatabase.AddObjectToAsset(t, controller);
            anyAdded++;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log($"[修复女性动画] 完成！清空旧过渡 {removed} 条，重建状态间过渡 {added} 条 + AnyState trigger {anyAdded} 条。" +
                  "请在 Play Mode 验证：站立/走路/冲刺/瞄准四方向移动/跳跃/受击均正常，不再抽搐。");
    }

    /// <summary>创建一条过渡（默认：无退出时间、持续 0.05s）</summary>
    private static AnimatorStateTransition MakeTransition(AnimatorState target, float duration, bool hasExitTime, float exitTime)
    {
        return new AnimatorStateTransition
        {
            destinationState = target,
            duration = duration,
            hasExitTime = hasExitTime,
            exitTime = exitTime,
        };
    }

    /// <summary>一条过渡条件</summary>
    private struct Cond
    {
        public AnimatorConditionMode Mode;
        public float Threshold;
        public string Param;
        public Cond(AnimatorConditionMode mode, float threshold, string param)
        {
            Mode = mode;
            Threshold = threshold;
            Param = param;
        }
    }

    /// <summary>一条过渡规格</summary>
    private struct Spec
    {
        public string From;
        public string To;
        public bool HasExitTime;
        public float ExitTime;
        public float Duration;
        public List<Cond> Conditions;
    }

    // ---------- 条件速记 ----------
    // nAim = 未瞄准; Aim = 瞄准中; S_idle = Speed<0.1; S_walk = 0.1<=Speed<0.5; S_run = Speed>=0.5
    // F/B/L/R = AimZ/AimX 方向区间（与旧 AnyState 条件一致）
    private static readonly Cond nAim = new Cond(AnimatorConditionMode.IfNot, 0f, "IsAiming");
    private static readonly Cond Aim = new Cond(AnimatorConditionMode.If, 0f, "IsAiming");
    private static readonly Cond SIdle = new Cond(AnimatorConditionMode.Less, 0.1f, "Speed");
    private static readonly Cond SWalkLo = new Cond(AnimatorConditionMode.Greater, 0.1f, "Speed");
    private static readonly Cond SWalkHi = new Cond(AnimatorConditionMode.Less, 0.5f, "Speed");
    private static readonly Cond SRun = new Cond(AnimatorConditionMode.Greater, 0.5f, "Speed");
    private static readonly List<Cond> SWalk = new List<Cond> { SWalkLo, SWalkHi };

    // 方向：与旧 AnyState 过渡的条件完全一致
    private static readonly List<Cond> F = new List<Cond>
    {
        new Cond(AnimatorConditionMode.Greater, 0.5f, "AimZ"),
        new Cond(AnimatorConditionMode.Less, 0.5f, "AimX"),
        new Cond(AnimatorConditionMode.Greater, -0.5f, "AimX"),
    };
    private static readonly List<Cond> B = new List<Cond>
    {
        new Cond(AnimatorConditionMode.Less, -0.5f, "AimZ"),
        new Cond(AnimatorConditionMode.Less, 0.5f, "AimX"),
        new Cond(AnimatorConditionMode.Greater, -0.5f, "AimX"),
    };
    private static readonly List<Cond> L = new List<Cond>
    {
        new Cond(AnimatorConditionMode.Less, -0.5f, "AimX"),
        new Cond(AnimatorConditionMode.Less, 0.5f, "AimZ"),
        new Cond(AnimatorConditionMode.Greater, -0.5f, "AimZ"),
    };
    private static readonly List<Cond> R = new List<Cond>
    {
        new Cond(AnimatorConditionMode.Greater, 0.5f, "AimX"),
        new Cond(AnimatorConditionMode.Less, 0.5f, "AimZ"),
        new Cond(AnimatorConditionMode.Greater, -0.5f, "AimZ"),
    };

    /// <summary>合并条件列表（第一个是单条条件，后两个是条件列表）</summary>
    private static List<Cond> With(Cond first, List<Cond> b, List<Cond> c)
    {
        var all = new List<Cond> { first };
        all.AddRange(b);
        all.AddRange(c);
        return all;
    }

    // ---------- 过渡规格表（From → To: 条件；无 ExitTime 字段 = HasExitTime false） ----------
    private static readonly List<Spec> TransitionSpecs = new List<Spec>
    {
        // Idle
        New("Idle", "Walk", 0.05f, new List<Cond> { nAim, SWalkLo, SWalkHi }),
        New("Idle", "Run", 0.05f, new List<Cond> { nAim, SRun }),
        New("Idle", "AimIdle", 0.05f, new List<Cond> { Aim }),

        // Walk
        New("Walk", "Idle", 0.05f, new List<Cond> { nAim, SIdle }),
        New("Walk", "Run", 0.05f, new List<Cond> { nAim, SRun }),
        New("Walk", "AimWalkF", 0.05f, With(Aim, SWalk, F)),
        New("Walk", "AimWalkB", 0.05f, With(Aim, SWalk, B)),
        New("Walk", "AimWalkL", 0.05f, With(Aim, SWalk, L)),
        New("Walk", "AimWalkR", 0.05f, With(Aim, SWalk, R)),
        New("Walk", "AimRun", 0.05f, new List<Cond> { Aim, SRun }),

        // Run
        New("Run", "Idle", 0.05f, new List<Cond> { nAim, SIdle }),
        New("Run", "Walk", 0.05f, new List<Cond> { nAim, SWalkLo, SWalkHi }),
        New("Run", "AimRun", 0.05f, new List<Cond> { Aim, SRun }),
        New("Run", "AimWalkF", 0.05f, With(Aim, SWalk, F)),
        New("Run", "AimWalkB", 0.05f, With(Aim, SWalk, B)),
        New("Run", "AimWalkL", 0.05f, With(Aim, SWalk, L)),
        New("Run", "AimWalkR", 0.05f, With(Aim, SWalk, R)),

        // AimIdle
        New("AimIdle", "Idle", 0.05f, new List<Cond> { nAim, SIdle }),
        New("AimIdle", "Walk", 0.05f, new List<Cond> { nAim, SWalkLo, SWalkHi }),
        New("AimIdle", "Run", 0.05f, new List<Cond> { nAim, SRun }),
        New("AimIdle", "AimWalkF", 0.05f, With(Aim, SWalk, F)),
        New("AimIdle", "AimWalkB", 0.05f, With(Aim, SWalk, B)),
        New("AimIdle", "AimWalkL", 0.05f, With(Aim, SWalk, L)),
        New("AimIdle", "AimWalkR", 0.05f, With(Aim, SWalk, R)),
        New("AimIdle", "AimRun", 0.05f, new List<Cond> { Aim, SRun }),

        // AimWalkF
        New("AimWalkF", "AimIdle", 0.05f, new List<Cond> { Aim, SIdle }),
        New("AimWalkF", "AimRun", 0.05f, new List<Cond> { Aim, SRun }),
        New("AimWalkF", "AimWalkB", 0.05f, With(Aim, SWalk, B)),
        New("AimWalkF", "AimWalkL", 0.05f, With(Aim, SWalk, L)),
        New("AimWalkF", "AimWalkR", 0.05f, With(Aim, SWalk, R)),
        New("AimWalkF", "Idle", 0.05f, new List<Cond> { nAim, SIdle }),
        New("AimWalkF", "Walk", 0.05f, new List<Cond> { nAim, SWalkLo, SWalkHi }),
        New("AimWalkF", "Run", 0.05f, new List<Cond> { nAim, SRun }),

        // AimWalkB
        New("AimWalkB", "AimIdle", 0.05f, new List<Cond> { Aim, SIdle }),
        New("AimWalkB", "AimRun", 0.05f, new List<Cond> { Aim, SRun }),
        New("AimWalkB", "AimWalkF", 0.05f, With(Aim, SWalk, F)),
        New("AimWalkB", "AimWalkL", 0.05f, With(Aim, SWalk, L)),
        New("AimWalkB", "AimWalkR", 0.05f, With(Aim, SWalk, R)),
        New("AimWalkB", "Idle", 0.05f, new List<Cond> { nAim, SIdle }),
        New("AimWalkB", "Walk", 0.05f, new List<Cond> { nAim, SWalkLo, SWalkHi }),
        New("AimWalkB", "Run", 0.05f, new List<Cond> { nAim, SRun }),

        // AimWalkL
        New("AimWalkL", "AimIdle", 0.05f, new List<Cond> { Aim, SIdle }),
        New("AimWalkL", "AimRun", 0.05f, new List<Cond> { Aim, SRun }),
        New("AimWalkL", "AimWalkF", 0.05f, With(Aim, SWalk, F)),
        New("AimWalkL", "AimWalkB", 0.05f, With(Aim, SWalk, B)),
        New("AimWalkL", "AimWalkR", 0.05f, With(Aim, SWalk, R)),
        New("AimWalkL", "Idle", 0.05f, new List<Cond> { nAim, SIdle }),
        New("AimWalkL", "Walk", 0.05f, new List<Cond> { nAim, SWalkLo, SWalkHi }),
        New("AimWalkL", "Run", 0.05f, new List<Cond> { nAim, SRun }),

        // AimWalkR
        New("AimWalkR", "AimIdle", 0.05f, new List<Cond> { Aim, SIdle }),
        New("AimWalkR", "AimRun", 0.05f, new List<Cond> { Aim, SRun }),
        New("AimWalkR", "AimWalkF", 0.05f, With(Aim, SWalk, F)),
        New("AimWalkR", "AimWalkB", 0.05f, With(Aim, SWalk, B)),
        New("AimWalkR", "AimWalkL", 0.05f, With(Aim, SWalk, L)),
        New("AimWalkR", "Idle", 0.05f, new List<Cond> { nAim, SIdle }),
        New("AimWalkR", "Walk", 0.05f, new List<Cond> { nAim, SWalkLo, SWalkHi }),
        New("AimWalkR", "Run", 0.05f, new List<Cond> { nAim, SRun }),

        // AimRun
        New("AimRun", "AimIdle", 0.05f, new List<Cond> { Aim, SIdle }),
        New("AimRun", "AimWalkF", 0.05f, With(Aim, SWalk, F)),
        New("AimRun", "AimWalkB", 0.05f, With(Aim, SWalk, B)),
        New("AimRun", "AimWalkL", 0.05f, With(Aim, SWalk, L)),
        New("AimRun", "AimWalkR", 0.05f, With(Aim, SWalk, R)),
        // 瞄准解除：回非瞄准移动状态
        New("AimRun", "Run", 0.05f, new List<Cond> { nAim, SRun }),
        New("AimRun", "Walk", 0.05f, new List<Cond> { nAim, SWalkLo, SWalkHi }),
        New("AimRun", "Idle", 0.05f, new List<Cond> { nAim, SIdle }),

        // ---------- 跳跃系列（沿用原控制器的条件与退出时间） ----------
        New("JumpStart", "JumpLoop", 0.1f, true, 0.6f, null),      // 无条件，播到 60% 进空中循环
        New("JumpLoop", "JumpLand", 0.1f, new List<Cond> { new Cond(AnimatorConditionMode.If, 0f, "JumpLand") }),
        New("JumpLand", "Idle", 0.1f, true, 0.95f, null),           // 无条件，播完回 Idle，再由 Idle 接力到 Walk/Run
        New("JumpLand", "Walk", 0.1f, new List<Cond> { SWalkLo }),
        New("JumpLand", "Run", 0.1f, new List<Cond> { SRun }),
        New("AimJump", "AimIdle", 0.1f, new List<Cond> { new Cond(AnimatorConditionMode.If, 0f, "JumpLand") }),
        New("AimJump", "Idle", 0.1f, new List<Cond>
        {
            new Cond(AnimatorConditionMode.If, 0f, "JumpLand"),
            nAim,
        }),
        New("AimJump", "AimIdle", 0.1f, true, 0.95f, null),

        // ---------- Hit 退出（播完回 Idle，再接力到 Walk/Run） ----------
        New("Hit", "Idle", 0.1f, true, 0.9f, null),
    };

    // ---------- AnyState 规格（仅 trigger 型） ----------
    private static readonly List<Spec> AnyStateSpecs = new List<Spec>
    {
        NewAny("JumpStart", new List<Cond> { new Cond(AnimatorConditionMode.If, 0f, "JumpStart"), nAim }),
        NewAny("AimJump", new List<Cond> { new Cond(AnimatorConditionMode.If, 0f, "JumpStart"), Aim }),
        NewAny("Hit", new List<Cond> { new Cond(AnimatorConditionMode.If, 0f, "Hit"), nAim }),
        NewAny("Die", new List<Cond> { new Cond(AnimatorConditionMode.If, 0f, "Die") }),
    };

    /// <summary>构造状态间过渡规格（无退出时间）</summary>
    private static Spec New(string from, string to, float duration, List<Cond> conditions)
    {
        return new Spec { From = from, To = to, Duration = duration, Conditions = conditions ?? new List<Cond>() };
    }

    /// <summary>构造带退出时间的过渡规格</summary>
    private static Spec New(string from, string to, float duration, bool hasExitTime, float exitTime, List<Cond> conditions)
    {
        return new Spec { From = from, To = to, Duration = duration, HasExitTime = hasExitTime, ExitTime = exitTime, Conditions = conditions ?? new List<Cond>() };
    }

    /// <summary>构造 AnyState 过渡规格</summary>
    private static Spec NewAny(string to, List<Cond> conditions)
    {
        return new Spec { From = null, To = to, Duration = 0.05f, Conditions = conditions };
    }
}

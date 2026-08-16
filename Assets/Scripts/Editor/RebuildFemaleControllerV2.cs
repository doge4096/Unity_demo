using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 全量重建 FemaleAnimator 控制器 V2：瞄准走路四状态（AimWalkF/B/L/R）合并为
/// 单状态 AimWalk + 2D SimpleDirectional Blend Tree（参数 AimX/AimZ）
///
/// 根因（第八轮后半）：四状态的方向条件互相排斥（AimWalkF 要 |AimX|<0.5，
/// AimWalkL 要 AimX<-0.5...），斜向输入（W+A → AimX=0.7+AimZ=0.7）时四个条件
/// 全部不满足 → 停在 AimIdle 站立姿势 → 角色被 cc.Move 带着"飘移移动"，
/// 下半身没有走路动画（用户反馈"边射击边走路下半身不动"）。
///
/// 正确模式（与 V1 相同）：
/// - AddState/AddTransition/AddAnyStateTransition/AddParameter 自动注册子对象，绝不手动 AddObjectToAsset
/// - 只有 new 出来的 AnimatorStateMachine 和 BlendTree 需要手动 AddObjectToAsset 注册
/// </summary>
public static class RebuildFemaleControllerV2
{
    private const string ControllerPath = "Assets/Art/Animators/FemaleAnimator.controller";

    // 瞄准走路四方向动画 guid（Fixed 目录，已修复循环）
    private static readonly string[] AimWalkClipGuids =
    {
        "22dc9ad7136734443a56e658cd2d63a7", // AimWalkF  female_aimWalk_fixed
        "c00099dc98463954d8c1f6e7332e0d16", // AimWalkR  female_aimWalkRight_fixed
        "34be2e1364ff688408d1460b903d1c40", // AimWalkB  female_aimWalkBack_fixed
        "26af47cce4728bf4b880ab1ba965863d", // AimWalkL  female_aimWalkLeft_fixed
    };

    // 全量状态动画 guid 兜底：旧控制器状态 motion 为 null 时使用
    // （bug 版控制器除 Blend Tree 外所有状态无 motion，重跑重建必须靠 guid 找回动画）
    // 空字符串 = 无动画状态（Empty）
    private static readonly Dictionary<string, string> StateClipGuids = new Dictionary<string, string>
    {
        ["Idle"] = "49f3afbf1ba2fbf46b33799c17d22336", // female_Idle
        ["Walk"] = "70921a408e1c32146ae55fa016c13215", // man_Walking_fixed
        ["Run"] = "f7bbcba32d4d5de41aca55f12bac2743",  // man_Run_fixed
        ["AimIdle"] = "9845c5016a21d844f89b5aa90de7752d", // female_aimIdle
        ["AimRun"] = "fb2e43364ecbb7548a7b8563d66bc89b", // female_aimRun_fixed
        ["AimJump"] = "940675911cec0e543a27d997aa6dae25", // female_aimJump_fixed
        ["JumpStart"] = "559bb4b3133871342863a1cc66f89b6b", // female_jumpstart
        ["JumpLoop"] = "7874c4f8efe3bfd4abd72e799daa8e69", // female_floating
        ["JumpLand"] = "714a9535a183db64284cc7c5d1f30415", // female_landing
        ["Hit"] = "80ef88beb27528a41a685327799b4d11",   // female_HitReaction
        ["Die"] = "3ad2f1f5f7d66e34f9f6572e9f4ceace",   // female_death
        ["AimShoot"] = "f770875e6a213cd4d83be432490bbb27", // female_aimShoot
        ["Shoot"] = "1e325c338577448429a9267163095a7b", // female_shoot_fixed
        ["AimHit"] = "388ff684bc57a0640bbe37df7fc85b3e", // female_aimHit_fixed
        ["Reload"] = "c1190ff3813f9ea4ca538a15d94aabc0", // female_reload_fixed
        ["Empty"] = "", // 空状态：无动画
    };

    [MenuItem("工具/重建女性控制器V2-混合树", false, 1002)]
    [MenuItem("Tools/RebuildFemaleControllerV2", false, 1002)] // 英文别名给 MCP 调用
    public static void Run()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[重建V2] 找不到控制器: {ControllerPath}");
            return;
        }

        // ========== 1. 从旧对象读取数据（动画引用 + 速度参数绑定） ==========
        var clips = new Dictionary<string, AnimationClip>(); // 状态名 -> 动画
        var speedParams = new Dictionary<string, string>();  // 状态名 -> 速度参数
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
        // UpperBody 遮罩：优先旧层配置，兜底按路径加载女性专用 mask
        // （旧控制器从未挂过 mask → 射击动画腿部曲线透传 → 走路射击时腿被定住）
        var upperMask = controller.layers.Length > 1 ? controller.layers[1].avatarMask : null;
        if (upperMask == null)
        {
            upperMask = AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/Art/Masks/FemaleUpperBody.mask");
            if (upperMask != null)
                Debug.Log("[重建V2] UpperBody 层旧配置无遮罩，已按路径兜底加载 FemaleUpperBody.mask");
        }

        // 瞄准走路四方向动画（优先旧状态 motion，兜底按 guid 加载）
        var aimWalkClips = new Dictionary<string, AnimationClip>();
        string[] dirNames = { "AimWalkF", "AimWalkR", "AimWalkB", "AimWalkL" };
        for (int i = 0; i < 4; i++)
        {
            var clip = LoadClipFromOld(controller, dirNames[i], AimWalkClipGuids[i]);
            if (clip == null)
            {
                Debug.LogError($"[重建V2] 加载 AimWalk 方向动画失败: {dirNames[i]} (guid {AimWalkClipGuids[i]})");
                return;
            }
            aimWalkClips[dirNames[i]] = clip;
        }

        // ========== 2. 删除全部旧子对象（含 V1 的四方向状态） ==========
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

        // AnimSpeed 默认值 1（否则速度参数=0 动画冻结首帧）
        {
            var ps = controller.parameters;
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].name == "AnimSpeed") ps[i].defaultFloat = 1f;
            controller.parameters = ps;
        }

        // ========== 5. Base Layer：状态机 + 12 状态 ==========
        var rootSm = new AnimatorStateMachine { name = "Base Layer" };
        AssetDatabase.AddObjectToAsset(rootSm, controller); // 新状态机必须手动注册
        controller.AddLayer(new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f, stateMachine = rootSm });

        // 新建状态：动画引用旧数据（null 时按 guid 兜底），速度参数绑定旧数据（无则 null）
        AnimatorState NewState(AnimatorStateMachine sm, string name)
        {
            var s = sm.AddState(name);
            var clip = GetClip(name);
            if (clip != null) s.motion = clip;
            if (speedParams.TryGetValue(name, out var sp)) { s.speedParameterActive = true; s.speedParameter = sp; }
            return s;
        }

        // 取动画：1. 旧控制器状态 motion  2. guid 兜底表
        AnimationClip GetClip(string name)
        {
            if (clips.TryGetValue(name, out var c) && c != null) return c;
            if (StateClipGuids.TryGetValue(name, out var g) && !string.IsNullOrEmpty(g))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!string.IsNullOrEmpty(p)) return AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
            }
            return null;
        }

        // ========== 6. AimWalk Blend Tree（2D SimpleDirectional，AimX/AimZ 混合） ==========
        var aimWalkBt = new BlendTree
        {
            name = "AimWalk",
            blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = "AimX",
            blendParameterY = "AimZ",
        };
        AssetDatabase.AddObjectToAsset(aimWalkBt, controller); // new 的 BlendTree 必须手动注册
        aimWalkBt.AddChild(aimWalkClips["AimWalkF"], new Vector2(0f, 1f));   // 前
        aimWalkBt.AddChild(aimWalkClips["AimWalkR"], new Vector2(1f, 0f));   // 右
        aimWalkBt.AddChild(aimWalkClips["AimWalkB"], new Vector2(0f, -1f));  // 后
        aimWalkBt.AddChild(aimWalkClips["AimWalkL"], new Vector2(-1f, 0f));  // 左

        var idle = NewState(rootSm, "Idle");
        var walk = NewState(rootSm, "Walk");
        var run = NewState(rootSm, "Run");
        var aimIdle = NewState(rootSm, "AimIdle");
        var aimWalk = NewState(rootSm, "AimWalk");
        aimWalk.motion = aimWalkBt; // 状态挂 Blend Tree（不走旧 motion 读取）
        var aimRun = NewState(rootSm, "AimRun");
        var aimJump = NewState(rootSm, "AimJump");
        var jumpStart = NewState(rootSm, "JumpStart");
        var jumpLoop = NewState(rootSm, "JumpLoop");
        var jumpLand = NewState(rootSm, "JumpLand");
        var hit = NewState(rootSm, "Hit");
        var die = NewState(rootSm, "Die");
        rootSm.defaultState = idle;

        // ========== 7. Base Layer 状态间过渡 ==========
        AnimatorStateTransition Trans(AnimatorState from, AnimatorState to, float duration = 0.05f)
        {
            var t = from.AddTransition(to); // AddTransition 自动注册子对象
            t.duration = duration;
            return t;
        }
        void Spec(AnimatorState from, AnimatorState to, System.Action<AnimatorStateTransition> conds, float duration = 0.05f)
        {
            var t = Trans(from, to, duration);
            conds?.Invoke(t);
        }
        // 瞄准+走路（进入 AimWalk：IsAiming + Speed 0.1-0.5，方向由 Blend Tree 自行混合）
        System.Action<AnimatorStateTransition> ToAimWalk()
        {
            return t =>
            {
                t.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming");
                t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                t.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed");
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

        // Idle
        Spec(idle, walk, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); t.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed"); });
        Spec(idle, run, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed"); });
        Spec(idle, aimIdle, t => { t.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming"); });
        Spec(idle, aimWalk, ToAimWalk());
        Spec(idle, aimRun, AimRunCond());
        // Walk
        Spec(walk, idle, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(walk, run, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed"); });
        Spec(walk, aimIdle, t => { t.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming"); });
        Spec(walk, aimWalk, ToAimWalk());
        Spec(walk, aimRun, AimRunCond());
        // Run
        Spec(run, idle, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(run, walk, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); t.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed"); });
        Spec(run, aimIdle, t => { t.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming"); });
        Spec(run, aimWalk, ToAimWalk());
        Spec(run, aimRun, AimRunCond());
        // AimIdle
        Spec(aimIdle, idle, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(aimIdle, walk, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); t.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed"); });
        Spec(aimIdle, run, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed"); });
        Spec(aimIdle, aimWalk, ToAimWalk());
        Spec(aimIdle, aimRun, AimRunCond());
        // AimWalk（离开：任何状态 → 由条件分流）
        Spec(aimWalk, aimIdle, t => { t.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(aimWalk, aimRun, AimRunCond());
        Spec(aimWalk, idle, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(aimWalk, walk, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); t.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed"); });
        Spec(aimWalk, run, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed"); });
        // AimRun
        Spec(aimRun, aimIdle, t => { t.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming"); t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); });
        Spec(aimRun, aimWalk, ToAimWalk());
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

        // ========== 8. Base Layer AnyState 过渡（4 条 trigger） ==========
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

        // ========== 9. UpperBody Layer：状态机 + 5 状态 ==========
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

        // UpperBody AnyState（4 条）
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

        // ========== 10. 保存 ==========
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // ========== 11. 验证并输出统计 ==========
        int totalTrans = 0;
        int totalAny = 0;
        int noMotion = 0;
        foreach (var layer in controller.layers)
        {
            foreach (var cs in layer.stateMachine.states)
            {
                totalTrans += cs.state.transitions.Length;
                if (cs.state.motion == null && cs.state.name != "Empty")
                {
                    noMotion++;
                    Debug.LogWarning($"[重建V2] 状态 {cs.state.name} 无动画（motion=null），需要检查！");
                }
            }
            totalAny += layer.stateMachine.anyStateTransitions.Length;
        }
        Debug.Log($"[重建V2] 完成！删除旧子对象 {removed} 个 | 参数 {controller.parameters.Length} | 层 {controller.layers.Length} | " +
                  $"Base状态 {controller.layers[0].stateMachine.states.Length}（含 AimWalk Blend Tree）| 过渡 {totalTrans} 条 | AnyState {totalAny} 条 | " +
                  $"无动画状态 {noMotion} 个。请 Play Mode 验证。");
    }

    /// <summary>
    /// 加载瞄准走路方向动画：优先旧控制器状态 motion，兜底按 guid 从 Fixed 目录加载
    /// </summary>
    private static AnimationClip LoadClipFromOld(AnimatorController controller, string stateName, string guid)
    {
        // 方式1：旧状态 motion
        foreach (var layer in controller.layers)
        {
            foreach (var cs in layer.stateMachine.states)
            {
                if (cs.state != null && cs.state.name == stateName && cs.state.motion is AnimationClip c)
                    return c;
            }
        }
        // 方式2：guid 直接加载
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    }
}

using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// Play Mode 下验证 FemaleAnimator 参数驱动的状态切换（重建后回归测试）
///
/// 测试序列（每个阶段设置参数 → 采样 30 帧 Hips 摆动 → 记录当前状态名）：
///   0. Idle     Speed=0            → 预期 Idle（摆动小，呼吸级 ±0.003）
///   1. Walk     Speed=0.4          → 预期 Walk（摆动大 ±0.03）
///   2. Run      Speed=1.0          → 预期 Run
///   3. AimWalkF IsAiming=1+AimZ=1  → 预期 AimWalkF
///   4. Hit      SetTrigger("Hit")  → 预期 Hit（触发后 0.9s 回 Idle）
///   5. 结束输出汇总日志
///
/// 菜单：工具/验证女性动画状态切换（英文别名 Tools/ValidateFemaleAnimator 给 MCP）
/// </summary>
public static class ValidateFemaleAnimator
{
    private const string HipsPath = "mixamorig1:Hips";

    private static int phase = -1;      // 当前阶段
    private static int frameInPhase = 0;// 阶段内帧数
    private static float swingMax = 0f; // Hips Y 最大波动
    private static Vector3 prevHips;
    private static StringBuilder sb;
    private static Animator anim;
    private static Transform hips;

    /// <summary>
    /// 读取女性所有运行时参数值
    /// </summary>
    [MenuItem("工具/读取女性全部参数", false, 1005)]
    [MenuItem("Tools/DumpFemaleParams", false, 1005)]
    public static void DumpFemaleParams()
    {
        var female = GameObject.Find("Female");
        if (female == null) { Debug.LogError("[采样] 找不到 Female"); return; }
        var a = female.GetComponent<Animator>();
        var ctrl = (UnityEditor.Animations.AnimatorController)a.runtimeAnimatorController;
        var parts = new System.Collections.Generic.List<string>();
        foreach (var p in ctrl.parameters)
        {
            switch (p.type)
            {
                case AnimatorControllerParameterType.Bool: parts.Add($"{p.name}={a.GetBool(p.name)}"); break;
                case AnimatorControllerParameterType.Float: parts.Add($"{p.name}={a.GetFloat(p.name):F2}"); break;
                case AnimatorControllerParameterType.Int: parts.Add($"{p.name}={a.GetInteger(p.name)}"); break;
                case AnimatorControllerParameterType.Trigger: parts.Add($"{p.name}=trigger"); break;
            }
        }
        Debug.Log("[参数] " + string.Join(" | ", parts));
        Debug.Log($"[层权重] layer0={a.GetLayerWeight(0):F2} layer1={a.GetLayerWeight(1):F2} " +
                  $"| 层1激活={a.IsInTransition(1)} 过渡层0={a.IsInTransition(0)}");
    }

    /// <summary>
    /// 采样男性角色当前状态（对照）
    /// </summary>
    [MenuItem("工具/采样男性动画当前状态", false, 1005)]
    [MenuItem("Tools/SampleMaleNow", false, 1005)]
    public static void SampleMaleNow()
    {
        var man = GameObject.Find("man");
        if (man == null) { Debug.LogError("[采样] 找不到 man"); return; }
        var a = man.GetComponent<Animator>();
        var info0 = a.GetCurrentAnimatorStateInfo(0);
        var info1 = a.GetCurrentAnimatorStateInfo(1);
        string s0 = "?", s1 = "?";
        var ctrl = (UnityEditor.Animations.AnimatorController)a.runtimeAnimatorController;
        if (ctrl != null)
        {
            foreach (var cs in ctrl.layers[0].stateMachine.states)
                if (cs.state.nameHash == info0.shortNameHash) s0 = cs.state.name;
            if (ctrl.layers.Length > 1)
                foreach (var cs in ctrl.layers[1].stateMachine.states)
                    if (cs.state.nameHash == info1.shortNameHash) s1 = cs.state.name;
        }
        Debug.Log($"[采样] 男 状态={s0}/{s1}");
    }

    /// <summary>
    /// 单次采样：立即输出当前状态名 + Hips 位置（配合 MCP 分步驱动验证）
    /// </summary>
    [MenuItem("工具/采样女性动画当前状态", false, 1005)]
    [MenuItem("Tools/SampleFemaleNow", false, 1005)]
    public static void SampleNow()
    {
        var female = GameObject.Find("Female");
        if (female == null) { Debug.LogError("[采样] 找不到 Female"); return; }
        var a = female.GetComponent<Animator>();
        var h = female.transform.Find(HipsPath);
        if (a == null || h == null) { Debug.LogError("[采样] Animator/Hips 缺失"); return; }
        var info0 = a.GetCurrentAnimatorStateInfo(0);
        var info1 = a.GetCurrentAnimatorStateInfo(1);
        Debug.Log($"[采样] 状态={CurrentState(a, 0)}/{CurrentState(a, 1)} | HipsY={h.position.y:F4} " +
                  $"| Speed={a.GetFloat("Speed"):F2} IsAiming={a.GetBool("IsAiming")} AimZ={a.GetFloat("AimZ"):F2}");
    }

    private static string CurrentState(Animator a, int layer)
    {
        var info = a.GetCurrentAnimatorStateInfo(layer);
        var ctrl = (UnityEditor.Animations.AnimatorController)a.runtimeAnimatorController;
        if (ctrl == null || ctrl.layers.Length <= layer) return "?";
        foreach (var cs in ctrl.layers[layer].stateMachine.states)
        {
            if (cs.state.nameHash == info.shortNameHash)
                return cs.state.name;
        }
        return $"hash{info.shortNameHash % 10000}";
    }

    [MenuItem("工具/验证女性动画状态切换", false, 1004)]
    [MenuItem("Tools/ValidateFemaleAnimator", false, 1004)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null) { Debug.LogError("[验证] Play Mode 中找不到 Female 对象！"); return; }
        anim = female.GetComponent<Animator>();
        if (anim == null) { Debug.LogError("[验证] Female 无 Animator！"); return; }
        hips = female.transform.Find(HipsPath);
        if (hips == null) { Debug.LogError("[验证] 找不到骨骼 " + HipsPath); return; }

        sb = new StringBuilder();
        sb.AppendLine("===== 女性动画状态切换验证（重建后） =====");
        sb.AppendLine($"Animator: controller={anim.runtimeAnimatorController.name} layers={anim.layerCount}");

        phase = -1;
        frameInPhase = 0;
        prevHips = hips.position;
        EditorApplication.update += Step;
        Debug.Log("[验证] 开始，共 5 个阶段（Idle/Walk/Run/AimWalkF/Hit），约 6 秒完成");
    }

    private static int totalFrames = 0;

    private static void Step()
    {
        totalFrames++;
        // 阶段内帧计数
        if (phase == -1)
        {
            // 初始化：先等 10 帧让动画就绪
            frameInPhase++;
            if (frameInPhase < 10) return;
            phase = 0;
            frameInPhase = 0;
            StartPhase();
            return;
        }

        // 采样当前帧 Hips 波动
        var p = hips.position;
        float dy = Mathf.Abs(p.y - prevHips.y);
        if (dy > swingMax) swingMax = dy;
        prevHips = p;
        frameInPhase++;

        // 每阶段采样 60 帧（约 1 秒）
        if (frameInPhase < 60) return;

        // 每 20 帧输出一次进度（即时反馈，确认 Step 在跑）
        if (frameInPhase % 20 == 0)
            Debug.Log($"[验证] 运行中 phase={phase} frame={frameInPhase} Hips摆动={swingMax:F4}m");

        // 超时保护：800 帧（约 13 秒）未完成则强制结束
        if (totalFrames > 800)
        {
            EditorApplication.update -= Step;
            sb.AppendLine("\n===== 超时强制结束 =====");
            Debug.Log(sb.ToString());
            return;
        }

        // 阶段结束：记录结果
        RecordResult();
        phase++;
        frameInPhase = 0;
        swingMax = 0f;

        if (phase > 4)
        {
            // 全部完成
            EditorApplication.update -= Step;
            sb.AppendLine("===== 验证完毕 =====");
            Debug.Log(sb.ToString());
        }
        else
        {
            StartPhase();
        }
    }

    private static void StartPhase()
    {
        switch (phase)
        {
            case 0: // Idle
                anim.SetFloat("Speed", 0f);
                anim.SetBool("IsAiming", false);
                sb.AppendLine("\n--- 阶段0 Idle (Speed=0) ---");
                break;
            case 1: // Walk
                anim.SetFloat("Speed", 0.4f);
                sb.AppendLine("\n--- 阶段1 Walk (Speed=0.4) ---");
                break;
            case 2: // Run
                anim.SetFloat("Speed", 1.0f);
                sb.AppendLine("\n--- 阶段2 Run (Speed=1.0) ---");
                break;
            case 3: // AimWalkF
                anim.SetBool("IsAiming", true);
                anim.SetFloat("AimZ", 1f);
                anim.SetFloat("AimX", 0f);
                anim.SetFloat("Speed", 0.4f);
                sb.AppendLine("\n--- 阶段3 AimWalkF (IsAiming+AimZ=1, Speed=0.4) ---");
                break;
            case 4: // Hit
                anim.SetBool("IsAiming", false);
                anim.SetFloat("Speed", 0f);
                anim.SetTrigger("Hit");
                sb.AppendLine("\n--- 阶段4 Hit (SetTrigger) ---");
                break;
        }
        // 记录阶段开始时状态
        sb.AppendLine($"  阶段{phase} 开始状态: {CurrentState(anim, 0)} / {CurrentState(anim, 1)}");
    }

    private static void RecordResult()
    {
        sb.AppendLine($"  阶段{phase} 结束: Hips摆动幅度={swingMax:F4}m | 状态: {CurrentState(anim, 0)} / {CurrentState(anim, 1)}" +
                      (swingMax > 0.008f ? " → 动画在播放" : " → 疑似冻结"));
    }
}

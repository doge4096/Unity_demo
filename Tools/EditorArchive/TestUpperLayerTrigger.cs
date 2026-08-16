using UnityEditor;
using UnityEngine;

/// <summary>
/// Play Mode 测试：直接用 C# SetTrigger 触发上层 AnyState（区分 MCP 触发方式 vs 上层状态机问题）
/// 菜单：工具/测试上层触发（Tools/TestShootTrigger）
/// </summary>
public static class TestUpperLayerTrigger
{
    [MenuItem("工具/测试上层射击触发", false, 1006)]
    [MenuItem("Tools/TestShootTrigger", false, 1006)]
    public static void TestShoot()
    {
        var female = GameObject.Find("Female");
        if (female == null) { Debug.LogError("[测试] 找不到 Female"); return; }
        var anim = female.GetComponent<Animator>();

        anim.SetBool("IsAiming", true);
        anim.SetTrigger("Shoot");
        Debug.Log($"[测试] 已设置 IsAiming=true + SetTrigger(Shoot) | 当前层1={StateName(anim, 1)}");

        // delayCall 链采样（每编辑器帧执行一次）
        EditorApplication.delayCall += () => DelaySample(anim, 1);
        EditorApplication.delayCall += () => DelaySample(anim, 2);
        EditorApplication.delayCall += () => DelaySample(anim, 3);
        EditorApplication.delayCall += () => DelaySample(anim, 6);
        EditorApplication.delayCall += () => DelaySample(anim, 10);
    }

    private static int _gameFrames;   // 已等待的游戏帧
    private static Animator _tAnim;
    private static int _listenFrames;
    private static System.Text.StringBuilder _listenLog;
    private static string _lastLayer1;

    [MenuItem("工具/逐帧监听上层状态", false, 1010)]
    [MenuItem("Tools/ListenUpperLayer", false, 1010)]
    public static void ListenUpper()
    {
        var female = GameObject.Find("Female");
        if (female == null) { Debug.LogError("[测试] 找不到 Female"); return; }
        _tAnim = female.GetComponent<Animator>();
        _tAnim.SetBool("IsAiming", true);
        _tAnim.SetTrigger("Shoot");
        _listenFrames = 0;
        _listenLog = new System.Text.StringBuilder();
        _lastLayer1 = StateName(_tAnim, 1);
        _listenLog.Append($"[监听] 触发后帧序列(层1): {_lastLayer1}");
        EditorApplication.update += ListenStep;
    }

    private static void ListenStep()
    {
        if (Time.deltaTime <= 0f) return; // 只统计游戏帧
        _listenFrames++;
        string s1 = StateName(_tAnim, 1);
        if (s1 != _lastLayer1)
        {
            _listenLog.Append($" → F{_listenFrames}:{s1}");
            _lastLayer1 = s1;
        }
        if (_listenFrames >= 120)
        {
            EditorApplication.update -= ListenStep;
            _listenLog.Append($" | 结束(120帧) 层0={StateName(_tAnim, 0)} IsAiming={_tAnim.GetBool("IsAiming")}");
            Debug.Log(_listenLog.ToString());
        }
    }

    [MenuItem("工具/可靠触发上层射击", false, 1009)]
    [MenuItem("Tools/ReliableShootTest", false, 1009)]
    public static void ReliableShootTest()
    {
        var female = GameObject.Find("Female");
        if (female == null) { Debug.LogError("[测试] 找不到 Female"); return; }
        _tAnim = female.GetComponent<Animator>();
        _tAnim.SetBool("IsAiming", true);
        _tAnim.SetTrigger("Shoot");
        Debug.Log($"[测试] 可靠测试: SetBool(IsAiming,true)+SetTrigger(Shoot) | 初始层1={StateName(_tAnim, 1)}");
        _gameFrames = 0;
        EditorApplication.update += WaitGameFrames;
    }

    private static void WaitGameFrames()
    {
        // 用 animator 的 playableGraph 状态判断游戏帧是否推进
        if (Time.deltaTime > 0f) _gameFrames++;
        if (_gameFrames < 60) return;
        EditorApplication.update -= WaitGameFrames;
        Debug.Log($"[测试] 60游戏帧后: 层1={StateName(_tAnim, 1)} 层0={StateName(_tAnim, 0)} IsAiming={_tAnim.GetBool("IsAiming")}");
    }

    [MenuItem("工具/诊断上层 AnyState 条件", false, 1008)]
    [MenuItem("Tools/DumpUpperAnyState", false, 1008)]
    public static void DumpUpperAnyState()
    {
        var ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
            "Assets/Art/Animators/FemaleAnimator.controller");
        if (ctrl == null) { Debug.LogError("[测试] 控制器加载失败"); return; }

        for (int li = 0; li < ctrl.layers.Length; li++)
        {
            var sm = ctrl.layers[li].stateMachine;
            Debug.Log($"[测试] 层[{li}] {ctrl.layers[li].name}: AnyState {sm.anyStateTransitions.Length} 条");
            foreach (var t in sm.anyStateTransitions)
            {
                string dst = t.destinationState != null ? t.destinationState.name : (t.isExit ? "Exit" : "NULL");
                string conds = string.Join(" | ", System.Array.ConvertAll(t.conditions,
                    c => $"{c.parameter}(mode={c.mode})={c.threshold}"));
                Debug.Log($"[测试]   AnyState→{dst} 条件: [{conds}]");
            }
        }
    }

    [MenuItem("工具/强制播放上层射击", false, 1007)]
    [MenuItem("Tools/ForcePlayUpper", false, 1007)]
    public static void ForcePlayUpper()
    {
        var female = GameObject.Find("Female");
        if (female == null) { Debug.LogError("[测试] 找不到 Female"); return; }
        var anim = female.GetComponent<Animator>();
        anim.Play("AimShoot", 1, 0f);
        Debug.Log("[测试] anim.Play(AimShoot, layer=1) 已调用");
        EditorApplication.delayCall += () => DelaySample(anim, 1);
        EditorApplication.delayCall += () => DelaySample(anim, 5);
        EditorApplication.delayCall += () => DelaySample(anim, 20);
    }

    private static void DelaySample(Animator anim, int step)
    {
        Debug.Log($"[测试] 延迟{step}帧后: 层1状态={StateName(anim, 1)} 层0状态={StateName(anim, 0)}");
    }

    private static string StateName(Animator anim, int layer)
    {
        var info = anim.GetCurrentAnimatorStateInfo(layer);
        var ctrl = (UnityEditor.Animations.AnimatorController)anim.runtimeAnimatorController;
        if (ctrl == null || ctrl.layers.Length <= layer) return "?";
        foreach (var cs in ctrl.layers[layer].stateMachine.states)
        {
            if (cs.state.nameHash == info.shortNameHash)
                return cs.state.name;
        }
        return $"hash{info.shortNameHash % 10000}";
    }
}

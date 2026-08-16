using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 运行时诊断：打印 Female Animator 当前状态实际播放的动画 clip 资产路径 + 控制器路径，
/// 确认走路时到底用的是 female_Walk_fixed 还是 man_Walking_fixed（clip 名都是 mixamo.com，必须看路径）。
/// 菜单：工具/检查走路动画来源（英文别名 Tools/CheckWalkSource）
/// </summary>
public static class WalkSourceCheck
{
    [MenuItem("工具/检查走路动画来源", false, 1050)]
    [MenuItem("Tools/CheckWalkSource", false, 1050)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null) { Debug.LogError("[走路来源] 找不到 Female"); return; }
        var anim = female.GetComponent<Animator>();
        if (anim == null) { Debug.LogError("[走路来源] Female 无 Animator"); return; }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[走路来源] 运行时控制器 = {anim.runtimeAnimatorController?.name} " +
                      $"(路径 {AssetDatabase.GetAssetPath(anim.runtimeAnimatorController)})");
        sb.AppendLine($"[走路来源] ApplyRootMotion = {anim.applyRootMotion}");

        // 各层当前状态实际播放的 clip 资产路径
        for (int li = 0; li < anim.layerCount; li++)
        {
            var st = anim.GetCurrentAnimatorStateInfo(li);
            var clips = anim.GetCurrentAnimatorClipInfo(li);
            sb.AppendLine($"  层{li} '{anim.GetLayerName(li)}' 状态hash={st.shortNameHash} normalized={st.normalizedTime:F2}");
            foreach (var ci in clips)
            {
                string path = AssetDatabase.GetAssetPath(ci.clip);
                sb.AppendLine($"    clip='{ci.clip.name}' weight={ci.weight:F3} 资产={path}");
            }
        }

        // 打印控制器里所有 Walk 状态引用的 motion 资产路径（编辑期）
        var ctrl = anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (ctrl != null)
        {
            sb.AppendLine($"\n[走路来源] 控制器内所有 Walk 状态引用：");
            foreach (var layer in ctrl.layers)
                DumpWalkStates(layer.stateMachine, sb, 1);
        }

        Debug.Log(sb.ToString());
        try { System.IO.File.AppendAllText("D:/tmp/walk_source.txt", sb.ToString() + "\n"); } catch { }
    }

    private static void DumpWalkStates(AnimatorStateMachine sm, System.Text.StringBuilder sb, int depth)
    {
        if (sm == null) return;
        foreach (var st in sm.states)
        {
            if (st.state.name != "Walk") continue;
            string indent = new string(' ', depth * 2);
            var m = st.state.motion;
            if (m is AnimationClip c)
            {
                string path = AssetDatabase.GetAssetPath(c);
                sb.AppendLine($"{indent}层 '{sm.name}' Walk → clip='{c.name}' 资产={path}");
            }
            else if (m is BlendTree bt)
            {
                sb.AppendLine($"{indent}层 '{sm.name}' Walk → BlendTree '{bt.name}'");
            }
            else
            {
                sb.AppendLine($"{indent}层 '{sm.name}' Walk → motion 为空/null");
            }
        }
        foreach (var child in sm.stateMachines)
            DumpWalkStates(child.stateMachine, sb, depth + 1);
    }
}

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;
using System.Text;

/// <summary>
/// 女性跑步动画替换为男性动画（验证动画源问题）
/// 把 FemaleAnimator/RangedAnimator 里所有文件名含 "Run" 的 female 动画
/// （Run/AimRun 及混合树分支）替换为 man_Run_fixed.anim
/// 男动画已验证：女模型播男动画完全正常
/// 菜单：Tools/Swap Female Run To Male（英文）
/// </summary>
public static class SwapFemaleRunToMale
{
    [MenuItem("Tools/Swap Female Run To Male")]
    public static void Run()
    {
        var maleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Art/Animations/Fixed/man_Run_fixed.anim");
        if (maleClip == null) { Debug.LogError("[SwapRun] man_Run_fixed.anim 加载失败"); return; }

        var sb = new StringBuilder();
        string[] ctrls = {
            "Assets/Art/Animators/FemaleAnimator.controller",
            "Assets/Art/Animators/RangedAnimator.controller"
        };
        foreach (var ctrlPath in ctrls)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) { sb.AppendLine($"控制器不存在: {ctrlPath}"); continue; }
            int replaced = 0;
            foreach (var layer in ctrl.layers)
                replaced += SwapInSM(layer.stateMachine, maleClip);
            if (replaced > 0)
            {
                EditorUtility.SetDirty(ctrl);
                sb.AppendLine($"{Path.GetFileName(ctrlPath)}: 替换 {replaced} 处跑步动画 → man_Run_fixed");
            }
            else
                sb.AppendLine($"{Path.GetFileName(ctrlPath)}: 无跑步动画（未替换）");
        }

        var outPath = "Assets/Screenshots/swap_run.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[SwapRun] 完成，结果: " + outPath);
    }

    private static int SwapInSM(AnimatorStateMachine sm, AnimationClip maleClip)
    {
        if (sm == null) return 0;
        int replaced = 0;
        foreach (var st in sm.states)
        {
            var motion = st.state.motion;
            if (motion is AnimationClip clip && IsFemaleRunClip(clip))
            {
                st.state.motion = maleClip;
                replaced++;
            }
            else if (motion is BlendTree bt)
            {
                replaced += SwapInBlendTree(bt, maleClip);
            }
        }
        foreach (var child in sm.stateMachines)
            replaced += SwapInSM(child.stateMachine, maleClip);
        return replaced;
    }

    private static int SwapInBlendTree(BlendTree bt, AnimationClip maleClip)
    {
        int replaced = 0;
        var children = bt.children;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].motion is AnimationClip clip && IsFemaleRunClip(clip))
            {
                children[i].motion = maleClip;
                replaced++;
            }
            else if (children[i].motion is BlendTree child)
            {
                replaced += SwapInBlendTree(child, maleClip);
            }
        }
        bt.children = children;
        return replaced;
    }

    /// <summary>判断是否是女性跑步动画（文件名含 Run 且不是 man_）</summary>
    private static bool IsFemaleRunClip(AnimationClip clip)
    {
        string path = AssetDatabase.GetAssetPath(clip);
        if (string.IsNullOrEmpty(path)) return false;
        string name = Path.GetFileNameWithoutExtension(path);
        return name.Contains("Run") && !name.StartsWith("man_") && !name.Contains("Jump");
    }
}

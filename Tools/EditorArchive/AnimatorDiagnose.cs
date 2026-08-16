using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Text;
using System.IO;

/// <summary>
/// 临时诊断 — 输出 controller 状态到文件（排查 T-pose）
/// 结果写到 Assets/Screenshots/diagnose.txt
/// </summary>
[InitializeOnLoad]
public static class AnimatorDiagnose
{
    static AnimatorDiagnose()
    {
        EditorApplication.delayCall += Run;
    }

    private static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== MeleeAnimator 诊断 ===");

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Art/Animators/MeleeAnimator.controller");
        if (controller == null)
        {
            sb.AppendLine("controller 加载失败!");
        }
        else
        {
            sb.AppendLine($"参数 {controller.parameters.Length} 个");
            foreach (var p in controller.parameters)
                sb.AppendLine($"  参数 {p.name} type={p.type}");

            var sm = controller.layers[0].stateMachine;
            sb.AppendLine($"状态 {sm.states.Length} 个");
            foreach (var s in sm.states)
            {
                var st = s.state;
                string motion = st.motion != null ? st.motion.name : "(null!)";
                sb.AppendLine($"  状态 {st.name} motion='{motion}'");
            }
        }

        // 检查动画 fbx 的 clip 数量（通过 AnimationClip 资产）
        sb.AppendLine("=== 动画 clip 资产 ===");
        var clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Art/Animations" });
        foreach (var g in clips)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            sb.AppendLine($"  clip 资产: {path}");
        }

        string outPath = "D:/Project/unity/interview/Assets/Screenshots/diagnose.txt";
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[诊断] 已写入 " + outPath);
    }
}

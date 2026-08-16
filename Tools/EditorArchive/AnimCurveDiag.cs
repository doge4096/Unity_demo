using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;
using System.Linq;

/// <summary>
/// 临时诊断 — 输出动画的骨骼曲线路径（确认根骨骼基准）
/// 结果写到 Assets/Screenshots/anim_paths.txt
/// </summary>
[InitializeOnLoad]
public static class AnimCurveDiag
{
    static AnimCurveDiag()
    {
        EditorApplication.delayCall += Run;
    }

    private static void Run()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Art/Animations/man_attack1.fbx");
        if (clip == null)
        {
            File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/anim_paths.txt",
                "clip 加载失败");
            return;
        }

        var bindings = AnimationUtility.GetCurveBindings(clip);
        var paths = bindings.Select(b => b.path).Distinct().ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"曲线总数: {bindings.Length}, 骨骼数: {paths.Count}");
        foreach (var p in paths.Take(30))
            sb.AppendLine(p);

        // 检查是否有 root 相关路径
        sb.AppendLine("--- 含 root/Hips 的路径 ---");
        foreach (var p in paths)
        {
            if (p.ToLower().Contains("root") || p.Contains("Hips"))
                sb.AppendLine(p);
        }

        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/anim_paths.txt",
            sb.ToString());
        Debug.Log("[诊断] 动画路径已写入 anim_paths.txt");
    }
}

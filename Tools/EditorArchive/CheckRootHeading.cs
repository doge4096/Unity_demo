using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// Dump 各走路/跑步动画的 Hips 根骨骼旋转曲线（首帧/全程 euler Y），
/// 检查走路动画是否有根朝向偏移（历史 female_Walk.fbx Hips euler y=350° 左偏 10°）。
/// 菜单：工具/检查根骨骼朝向（英文别名 Tools/CheckRootHeading）
/// </summary>
public static class CheckRootHeading
{
    private static readonly string[] Clips = {
        "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim",
        "Assets/Art/Animations/Fixed/female_aimWalkLeft_fixed.anim",
        "Assets/Art/Animations/Fixed/female_aimWalkRight_fixed.anim",
        "Assets/Art/Animations/Fixed/female_aimWalkBack_fixed.anim",
        "Assets/Art/Animations/Fixed/female_aimRun_fixed.anim",
        "Assets/Art/Animations/Fixed/female_aimIdle_fixed.anim",
        "Assets/Art/Animations/Fixed/female_Walk_fixed.anim",
        "Assets/Art/Animations/Fixed/man_Walking_fixed.anim",
    };

    [MenuItem("工具/检查根骨骼朝向", false, 1094)]
    [MenuItem("Tools/CheckRootHeading", false, 1094)]
    public static void Run()
    {
        var sb = new StringBuilder();
        foreach (var path in Clips)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { sb.AppendLine($"\n== {System.IO.Path.GetFileName(path)} == 加载失败"); continue; }
            sb.AppendLine($"\n== {System.IO.Path.GetFileName(path)} (时长={clip.length:F3}s) ==");
            bool found = false;
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                bool isRoot = b.path == "" && (b.propertyName.StartsWith("RootT") || b.propertyName.StartsWith("RootQ") ||
                                                b.propertyName.StartsWith("Root") ||
                                                b.propertyName == "HipsT.x" || b.propertyName == "HipsT.y" || b.propertyName == "HipsT.z" ||
                                                b.propertyName == "HipsQ.x" || b.propertyName == "HipsQ.y" || b.propertyName == "HipsQ.z" || b.propertyName == "HipsQ.w");
                if (!isRoot) continue;
                found = true;
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                if (curve == null) continue;
                float first = curve.keys.Length > 0 ? curve.keys[0].value : 0;
                float last = curve.keys.Length > 0 ? curve.keys[curve.length - 1].value : 0;
                float min = float.MaxValue, max = float.MinValue;
                foreach (var k in curve.keys)
                {
                    if (k.value < min) min = k.value;
                    if (k.value > max) max = k.value;
                }
                // 根旋转四元数 → 粗略转 euler（只列 q 分量原始值）
                if (b.propertyName.StartsWith("RootQ") || b.propertyName.StartsWith("HipsQ"))
                    sb.AppendLine($"  {b.propertyName}: 首={first:F4} 尾={last:F4} 值域[{min:F4}~{max:F4}]");
                else
                    sb.AppendLine($"  {b.propertyName}: 首={first:F3} 尾={last:F3} 值域[{min:F3}~{max:F3}]");
            }
            if (!found) sb.AppendLine("  无根骨骼曲线（可能无根运动）");
        }

        var outPath = "Assets/Screenshots/root_heading.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[RootHeading] 完成，结果: " + outPath);
    }
}

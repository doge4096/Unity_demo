using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// Dump female_aimIdle.fbx（AimIdle 待机动画）的 Root/Hips 曲线，
/// 定位待机姿势角度偏转来源（RootQ 恒定偏转 / Hips 起始偏转）。
/// 菜单：工具/检查待机根曲线（英文别名 Tools/CheckAimIdleRoot）
/// </summary>
public static class CheckAimIdleRoot
{
    [MenuItem("工具/检查待机根曲线", false, 1103)]
    [MenuItem("Tools/CheckAimIdleRoot", false, 1103)]
    public static void Run()
    {
        var sb = new StringBuilder();
        Dump("female_aimIdle.fbx", "Assets/Art/Animations/female_aimIdle.fbx", sb);
        Dump("female_aimShoot.fbx", "Assets/Art/Animations/female_aimShoot.fbx", sb);
        Dump("female_Idle.fbx(普通待机对照)", "Assets/Art/Animations/female_Idle.fbx", sb);

        var outPath = "Assets/Screenshots/aimidle_root.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[AimIdleRoot] 完成，结果: " + outPath);
    }

    private static void Dump(string label, string path, StringBuilder sb)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { sb.AppendLine($"\n== {label} == 加载失败"); return; }
        sb.AppendLine($"\n== {label} (时长={clip.length:F3}s) ==");
        bool found = false;
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            bool isRoot = b.path == "" && (b.propertyName.StartsWith("Root") ||
                                            b.propertyName.StartsWith("Hips"));
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
            sb.AppendLine($"  {b.propertyName}: 首={first:F4} 尾={last:F4} 值域[{min:F4}~{max:F4}]");
        }
        if (!found) sb.AppendLine("  无 Root/Hips 曲线");
    }
}

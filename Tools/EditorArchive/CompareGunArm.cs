using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 对比 female_aimWalk_fixed（走路持枪，枪口偏左43°）与 female_aimShoot.fbx（射击持枪，枪口较正）的
/// 手臂/肩膀肌肉曲线，定位持枪姿态校正量。
/// 菜单：工具/对比持枪手臂曲线（英文别名 Tools/CompareGunArm）
/// </summary>
public static class CompareGunArm
{
    [MenuItem("工具/对比持枪手臂曲线", false, 1111)]
    [MenuItem("Tools/CompareGunArm", false, 1111)]
    public static void Run()
    {
        var sb = new StringBuilder();
        Dump("female_aimWalk_fixed(走路)", "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim", sb);
        Dump("female_aimShoot.fbx(射击)", "Assets/Art/Animations/female_aimShoot.fbx", sb);

        var outPath = "Assets/Screenshots/compare_gun_arm.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[GunArm] 完成，结果: " + outPath);
    }

    private static void Dump(string label, string path, StringBuilder sb)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { sb.AppendLine($"\n== {label} == 加载失败"); return; }
        sb.AppendLine($"\n== {label} (时长={clip.length:F3}s) ==");
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (!b.propertyName.Contains("Shoulder") && !b.propertyName.Contains("Arm") && !b.propertyName.Contains("Hand")) continue;
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null) continue;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var k in curve.keys)
            {
                if (k.value < min) min = k.value;
                if (k.value > max) max = k.value;
            }
            sb.AppendLine($"  {b.propertyName}: [{min * Mathf.Rad2Deg:F1}°, {max * Mathf.Rad2Deg:F1}°]");
        }
    }
}

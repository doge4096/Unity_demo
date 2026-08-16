using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>输出 female_Walk 左腿 Twist 曲线全部关键帧。菜单：Tools/Dump Curve</summary>
public static class CurveDump
{
    [MenuItem("Tools/Dump Curve")]
    public static void Run()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/female_Walk.fbx");
        var sb = new StringBuilder();
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (!b.propertyName.Contains("Twist")) continue;
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            sb.AppendLine($"\n== {b.propertyName} ==");
            for (int i = 0; i < curve.length; i++)
            {
                var k = curve.keys[i];
                float rate = i > 0 ? Mathf.Abs(k.value - curve.keys[i - 1].value) / Mathf.Max(0.0001f, k.time - curve.keys[i - 1].time) : 0f;
                sb.AppendLine($"  t={k.time:F3}s 值={k.value:F4} 速率={rate:F1}");
            }
        }
        var outPath = "Assets/Screenshots/curve_dump.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[CurveDump] 完成");
    }
}

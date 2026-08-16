using UnityEditor;
using UnityEngine;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 读取动画曲线关键帧：对比 female_Walk 左右腿曲线质量（定位 0.40s 附近的甩动）
/// 菜单：Tools/Diag Curve Keyframes（英文）
/// </summary>
public static class CurveKeyframeDiag
{
    [MenuItem("Tools/Diag Curve Keyframes")]
    public static void Run()
    {
        var sb = new StringBuilder();
        DiagClip("female_Walk", "Assets/Art/Animations/female_Walk.fbx", sb);
        DiagClip("man_Walking", "Assets/Art/Animations/man_Walking.fbx", sb);

        var outPath = "Assets/Screenshots/curve_keyframes.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[CurveDiag] 完成，结果: " + outPath);
    }

    private static void DiagClip(string label, string clipPath, StringBuilder sb)
    {
        sb.AppendLine($"\n========== {label} ==========");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) { sb.AppendLine("clip 加载失败"); return; }
        sb.AppendLine($"clip: {clip.name} 时长={clip.length:F3}s 帧率={clip.frameRate}");

        // 打印前 15 条 binding 看路径格式
        var bindings = AnimationUtility.GetCurveBindings(clip);
        sb.AppendLine($"总 binding 数={bindings.Length}");
        for (int i = 0; i < Mathf.Min(15, bindings.Length); i++)
            sb.AppendLine($"  binding[{i}]: path={bindings[i].path} prop={bindings[i].propertyName} type={bindings[i].type?.Name}");

        // 肌肉空间曲线：binding.propertyName 是肌肉名（LeftUpperLegQ.x 等）
        // 统计所有曲线的关键帧数与最大帧间跳变
        var allProps = new Dictionary<string, List<float>>(); // prop名 -> 所有关键帧值
        var propKeyframes = new Dictionary<string, int>();
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null) continue;
            if (!propKeyframes.ContainsKey(b.propertyName)) propKeyframes[b.propertyName] = 0;
            propKeyframes[b.propertyName] += curve.length;
            // 计算相邻关键帧最大差值
            float worst = 0f, worstT = 0f;
            for (int i = 1; i < curve.length; i++)
            {
                float d = Mathf.Abs(curve.keys[i].value - curve.keys[i - 1].value);
                if (d > worst) { worst = d; worstT = curve.keys[i].time; }
            }
            sb.AppendLine($"  {b.propertyName}: 关键帧={curve.length} 最大帧差={worst:F4}(@{worstT:F3}s)");
        }
    }

    private static List<Keyframe> GetCurve(Dictionary<string, List<Keyframe>> map, string key)
    {
        return map.TryGetValue(key, out var v) ? v : null;
    }

    private static float SampleCurve(List<Keyframe> keys, float t)
    {
        if (keys == null || keys.Count == 0) return 0f;
        if (t <= keys[0].time) return keys[0].value;
        for (int i = 0; i < keys.Count - 1; i++)
        {
            if (t <= keys[i + 1].time)
            {
                float tt = (t - keys[i].time) / Mathf.Max(0.0001f, keys[i + 1].time - keys[i].time);
                return Mathf.Lerp(keys[i].value, keys[i + 1].value, tt);
            }
        }
        return keys[keys.Count - 1].value;
    }
}

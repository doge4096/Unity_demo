using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 诊断动画 clip 的曲线绑定是否有效（对比有效人形动画 vs 数字 attribute 动画）
/// 菜单：工具/检查动画曲线绑定（英文别名 Tools/DumpClipBindings）
/// </summary>
public static class ClipBindingsDump
{
    [MenuItem("工具/检查动画曲线绑定", false, 1003)]
    [MenuItem("Tools/DumpClipBindings", false, 1003)]
    public static void Dump()
    {
        string[] paths =
        {
            "Assets/Art/Animations/Fixed/man_Walking_fixed.anim",       // 有效（字符串肌肉名）
            "Assets/Art/Animations/Fixed/man_Run_fixed.anim",           // 有效（字符串肌肉名）
            "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim",    // 数字 attribute？
            "Assets/Art/Animations/Fixed/female_aimWalkLeft_fixed.anim",
            "Assets/Art/Animations/Fixed/female_aimWalkRight_fixed.anim",
            "Assets/Art/Animations/Fixed/female_aimWalkBack_fixed.anim",
            "Assets/Art/Animations/Fixed/female_aimRun_fixed.anim",
            "Assets/Art/Animations/Fixed/female_Walk_fixed.anim",
            "Assets/Art/Animations/Fixed/female_Run_fixed.anim",
        };
        foreach (var p in paths)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
            if (clip == null) { Debug.LogError($"[绑定] 加载失败: {p}"); continue; }
            var sb = new StringBuilder();
            sb.AppendLine($"[绑定] {clip.name} | 时长={clip.length:F2}s 帧率={clip.frameRate} " +
                          $"总曲线={clip.legacy} 人形={clip.isHumanMotion}");
            var bindings = AnimationUtility.GetCurveBindings(clip);
            sb.AppendLine($"  GetEditorCurveBindings 返回 {bindings.Length} 条");
            int n = Mathf.Min(bindings.Length, 8);
            for (int i = 0; i < n; i++)
            {
                var b = bindings[i];
                sb.AppendLine($"    [{i}] path='{b.path}' type={b.type.Name} prop='{b.propertyName}'");
            }
            if (bindings.Length == 0)
                sb.AppendLine("    >>> 0 条有效绑定 —— 动画不驱动任何属性！");
            else if (bindings.Length > n)
                sb.AppendLine($"    ... 其余 {bindings.Length - n} 条省略");
            Debug.Log(sb.ToString());
        }
    }
}

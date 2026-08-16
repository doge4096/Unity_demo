using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Text;

/// <summary>
/// 最终链路验证：3 个控制器每个状态的 motion 引用能否被 Unity 真实加载
/// （重建 RangedAnimator 后确认无空引用、无坏引用）
/// 菜单：Tools/Verify Controllers（英文）
/// </summary>
public static class VerifyControllers
{
    [MenuItem("Tools/Verify Controllers")]
    public static void Run()
    {
        var sb = new StringBuilder();
        string[] ctrlPaths = {
            "Assets/Art/Animators/FemaleAnimator.controller",
            "Assets/Art/Animators/RangedAnimator.controller",
            "Assets/Art/Animators/MeleeAnimator.controller"
        };
        foreach (var ctrlPath in ctrlPaths)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) { sb.AppendLine($"!! 控制器加载失败: {ctrlPath}"); continue; }
            sb.AppendLine($"\n=== {ctrlPath} ===");
            int bad = 0;
            foreach (var layer in ctrl.layers)
                bad += VerifyStateMachine(layer.stateMachine, sb);
            if (bad == 0)
                sb.AppendLine("  ✅ 所有状态引用有效");
            else
                sb.AppendLine($"  ❌ {bad} 个状态引用异常");
        }
        var outPath = "Assets/Screenshots/verify_controllers.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[VerifyControllers] 完成，结果: " + outPath);
    }

    private static int VerifyStateMachine(AnimatorStateMachine sm, StringBuilder sb)
    {
        int bad = 0;
        if (sm == null) return 0;
        foreach (var st in sm.states)
        {
            var m = st.state.motion;
            string name = st.state.name;
            if (m == null)
            {
                // 允许命名的 Empty 状态为空，其余报错
                if (name != "Empty")
                {
                    sb.AppendLine($"  ❌ {name}: motion 为空");
                    bad++;
                }
                else sb.AppendLine($"  - {name}: 空（设计如此）");
            }
            else if (m is AnimationClip clip)
            {
                string path = AssetDatabase.GetAssetPath(clip);
                // 二次确认：路径存在且可加载
                var recheck = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (recheck == null)
                {
                    sb.AppendLine($"  ❌ {name}: 引用 {clip.name} 但重新加载失败 path={path}");
                    bad++;
                }
                else
                {
                    sb.AppendLine($"  ✅ {name}: {clip.name} ({System.IO.Path.GetFileName(path)})");
                }
            }
            else if (m is BlendTree bt)
            {
                sb.AppendLine($"  ✅ {name}: BlendTree {bt.name}（{bt.children.Length} 个方向）");
                bad += VerifyBlendTree(bt, sb);
            }
        }
        foreach (var child in sm.stateMachines)
            bad += VerifyStateMachine(child.stateMachine, sb);
        return bad;
    }

    private static int VerifyBlendTree(BlendTree bt, StringBuilder sb)
    {
        int bad = 0;
        foreach (var c in bt.children)
        {
            if (c.motion is AnimationClip clip)
            {
                string path = AssetDatabase.GetAssetPath(clip);
                var recheck = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (recheck == null)
                {
                    sb.AppendLine($"  ❌ 混合树子项: {clip.name} 重新加载失败");
                    bad++;
                }
            }
            else if (c.motion == null)
            {
                sb.AppendLine($"  ❌ 混合树子项: motion 为空");
                bad++;
            }
        }
        return bad;
    }
}

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;
using System.Text;

/// <summary>
/// 修复控制器 motion 引用：fileID 与 guid 不匹配（重建 Fixed 资产后 Unity 缓存旧对象引用）
/// 做法：强制重导入 Fixed/*.anim 清除缓存 → 重新 LoadAssetAtPath → 重新赋值 motion
/// 菜单：Tools/Fix Motion Refs（英文）
/// </summary>
public static class FixMotionRefs
{
    const string FixedDir = "Assets/Art/Animations/Fixed";

    [MenuItem("Tools/Fix Motion Refs")]
    public static void Fix()
    {
        var sb = new StringBuilder();

        // 1. 强制重导入所有 Fixed 资产，清除 Unity 缓存里的错误 fileID
        if (Directory.Exists(FixedDir))
        {
            foreach (var f in Directory.GetFiles(FixedDir, "*.anim"))
            {
                AssetDatabase.ImportAsset(f.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
                sb.AppendLine($"重导入: {Path.GetFileName(f)}");
            }
        }

        // 2. 遍历控制器重新赋值 motion
        string[] ctrlPaths = {
            "Assets/Art/Animators/FemaleAnimator.controller",
            "Assets/Art/Animators/RangedAnimator.controller",
            "Assets/Art/Animators/MeleeAnimator.controller"
        };
        foreach (var ctrlPath in ctrlPaths)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) { sb.AppendLine($"控制器不存在: {ctrlPath}"); continue; }
            int replaced = 0;
            foreach (var layer in ctrl.layers)
                replaced += FixInStateMachine(layer.stateMachine, ref replaced);
            if (replaced > 0)
            {
                EditorUtility.SetDirty(ctrl);
                sb.AppendLine($"控制器 {Path.GetFileName(ctrlPath)}: 修正 {replaced} 处 motion 引用");
            }
            else
            {
                sb.AppendLine($"控制器 {Path.GetFileName(ctrlPath)}: 无需修正");
            }
        }

        AssetDatabase.SaveAssets();
        var outPath = "Assets/Screenshots/fix_motion_refs.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[FixMotionRefs] 完成，结果: " + outPath);
    }

    private static int FixInStateMachine(AnimatorStateMachine sm, ref int total)
    {
        int replaced = 0;
        if (sm == null) return 0;
        foreach (var st in sm.states)
        {
            if (st.state.motion is AnimationClip clip)
            {
                string path = AssetDatabase.GetAssetPath(clip);
                if (string.IsNullOrEmpty(path)) continue;
                path = path.Replace('\\', '/');
                if (!path.StartsWith(FixedDir)) continue;
                // 重新加载（此时已重导入，fileID 应为 7400000）+ 重新赋值 → 序列化正确引用
                var fresh = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (fresh == null) continue;
                st.state.motion = fresh;
                replaced++;
            }
            else if (st.state.motion is BlendTree bt)
            {
                replaced += FixInBlendTree(bt, ref total);
            }
        }
        foreach (var child in sm.stateMachines)
            replaced += FixInStateMachine(child.stateMachine, ref total);
        total += replaced;
        return replaced;
    }

    private static int FixInBlendTree(BlendTree bt, ref int total)
    {
        int replaced = 0;
        var children = bt.children;
        for (int i = 0; i < children.Length; i++)
        {
            var m = children[i].motion;
            if (m is AnimationClip clip)
            {
                string path = AssetDatabase.GetAssetPath(clip);
                if (string.IsNullOrEmpty(path)) continue;
                path = path.Replace('\\', '/');
                if (!path.StartsWith(FixedDir)) continue;
                var fresh = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (fresh == null) continue;
                children[i].motion = fresh;
                replaced++;
            }
            else if (m is BlendTree child)
            {
                replaced += FixInBlendTree(child, ref total);
            }
        }
        bt.children = children;
        total += replaced;
        return replaced;
    }
}

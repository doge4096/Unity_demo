using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 强制重新加载 FemaleAnimator 控制器（丢弃内存中损坏的对象，从磁盘文件重新解析）
/// 修复：Unity 缓存了修复前的内存对象，场景 Animator 引用它导致运行时状态机为空
/// </summary>
public static class ReloadFemaleController
{
    private const string Path = "Assets/Art/Animators/FemaleAnimator.controller";

    [MenuItem("工具/强制重新加载女性控制器", false, 1002)]
    [MenuItem("Tools/ReloadFemaleController", false, 1002)]
    public static void Reload()
    {
        var c1 = AssetDatabase.LoadAssetAtPath<AnimatorController>(Path);
        if (c1 == null)
        {
            Debug.LogError("[重载] 控制器加载失败！");
            return;
        }
        Log(c1, "加载前");

        // 1. 卸载未使用的资源（释放无引用的缓存对象）
        EditorUtility.UnloadUnusedAssetsImmediate();

        // 2. 强制从磁盘重新导入（同步 + 强制更新，替换内存对象）
        AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        // 3. 重新加载并验证
        var c2 = AssetDatabase.LoadAssetAtPath<AnimatorController>(Path);
        if (c2 == null)
        {
            Debug.LogError("[重载] 重载后控制器为空！");
            return;
        }
        Log(c2, "加载后");
    }

    private static void Log(AnimatorController c, string label)
    {
        var sm = c.layers[0].stateMachine;
        int total = 0;
        foreach (var cs in sm.states)
            total += cs.state.transitions.Length;

        Debug.Log($"[重载] {label}: instanceID={c.GetInstanceID()} 参数={c.parameters.Length} 层={c.layers.Length} " +
                  $"Base层状态={sm.states.Length} AnyState={sm.anyStateTransitions.Length} 状态过渡={total}");

        foreach (var cs in sm.states)
            Debug.Log($"[重载] {label} 状态 {cs.state.name}: {cs.state.transitions.Length} 条过渡, 动画={(cs.state.motion != null ? cs.state.motion.name : "NULL")}");
    }
}

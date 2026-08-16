using UnityEditor;
using UnityEngine;

/// <summary>
/// 扫描场景中所有挂有缺失脚本（missing script）的对象
/// 菜单：工具/扫描缺失脚本（英文别名 Tools/ScanMissingScripts）
/// </summary>
public static class MissingScriptScanner
{
    [MenuItem("工具/扫描缺失脚本", false, 1000)]
    [MenuItem("Tools/ScanMissingScripts", false, 1000)]
    public static void Scan()
    {
        var all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int missingCount = 0;
        foreach (var go in all)
        {
            var components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    missingCount++;
                    Debug.LogWarning($"[缺失脚本] {go.name} (路径: {GetPath(go)}) 第 {i} 个组件引用缺失", go);
                }
            }
        }
        Debug.Log($"[缺失脚本] 扫描完成：场景 {all.Length} 个对象，缺失组件 {missingCount} 个");
    }

    private static string GetPath(GameObject go)
    {
        var t = go.transform;
        var sb = new System.Text.StringBuilder(t.name);
        while (t.parent != null)
        {
            t = t.parent;
            sb.Insert(0, t.name + "/");
        }
        return sb.ToString();
    }
}

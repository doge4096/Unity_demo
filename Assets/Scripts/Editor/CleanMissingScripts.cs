using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 清理场景中所有缺失脚本（missing script）组件
/// 根因：CharacterSelectPanel 上残留一个脚本早已删除的空壳组件（guid bd33023885798df41a2737b6cbdbd8ae，
/// 从项目初始化提交起就存在），点 Play 时 Inspector 序列化报错：
/// SerializedObjectNotCreatableException / ArgumentNullException / NullReferenceException
/// 菜单：工具/清理场景缺失脚本（英文别名 Tools/CleanMissingScripts）
/// </summary>
public static class CleanMissingScripts
{
    [MenuItem("工具/清理场景缺失脚本", false, 1004)]
    [MenuItem("Tools/CleanMissingScripts", false, 1004)] // 英文别名给 MCP 调用
    public static void Clean()
    {
        var all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int removed = 0;
        int total = 0;
        foreach (var go in all)
        {
            total++;
            // 官方 API：移除该对象上所有引用缺失脚本的组件
            int cnt = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (cnt > 0)
            {
                removed += cnt;
                Debug.Log($"[清理] {go.name}（路径: {GetPath(go.transform)}）移除 {cnt} 个缺失组件");
            }
        }
        if (removed > 0)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        // ========== 清理 prefab 资产中的缺失组件 ==========
        // 报错 stack: GameObjectInspector.OnEnable → PrefabUtility.IsPartOfVariantPrefab
        // 说明 Inspector 刷新的是 prefab 资产，prefab 内部也有 missing script
        int prefabRemoved = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".prefab")) continue;
            var contents = PrefabUtility.LoadPrefabContents(path);
            int cnt = 0;
            foreach (var c in contents.GetComponentsInChildren<Component>(true))
            {
                if (c == null) cnt++;
            }
            if (cnt > 0)
            {
                int real = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(contents);
                if (real > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    Debug.Log($"[清理] prefab {path} 移除 {real} 个缺失组件");
                    prefabRemoved += real;
                }
            }
            PrefabUtility.UnloadPrefabContents(contents);
        }
        if (prefabRemoved > 0)
            AssetDatabase.SaveAssets();

        Debug.Log($"[清理] 完成：场景扫描 {total} 个对象移除 {removed} 个，prefab 移除 {prefabRemoved} 个，共 {removed + prefabRemoved} 个。场景已保存");
    }

    /// <summary>
    /// 检查场景是否还有缺失组件（辅助验证，菜单：工具/检查缺失脚本）
    /// </summary>
    [MenuItem("工具/检查缺失脚本", false, 1004)]
    [MenuItem("Tools/CheckMissingScripts", false, 1004)]
    public static void Check()
    {
        var all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int missing = 0;
        foreach (var go in all)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null)
                {
                    missing++;
                    Debug.LogWarning($"[检查] {go.name}（路径: {GetPath(go.transform)}）仍有缺失组件", go);
                }
            }
        }
        Debug.Log($"[检查] 扫描 {all.Length} 个对象，缺失组件 {missing} 个");
    }

    private static string GetPath(Transform t)
    {
        var sb = new System.Text.StringBuilder(t.name);
        while (t.parent != null)
        {
            t = t.parent;
            sb.Insert(0, t.name + "/");
        }
        return sb.ToString();
    }
}

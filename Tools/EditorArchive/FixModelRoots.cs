using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 修复场景中模型根位置并用 EditorSceneManager.SaveScene 保存（v3）：
/// 男 MeleePlayer/man  → localPosition (0, -1.08, 0)（模型回到 GO 正下方，脚贴地）
/// 女 RangedPlayer/Female → localPosition (0, -0.85, 0)
///
/// v3 修复要点（重要，前两版均失败）：
/// - v1 直接 setter + SetDirty：prefab 实例的 Transform 序列化值仍读旧 override，
///   磁盘残留 scene_save 重算的远值（x=-9.589/z=105.185），重新加载场景又甩远
/// - v2 RecordPrefabInstancePropertyModifications：同样基于序列化值，x/z 仍读旧值
/// - v3 用 SerializedObject 直接改 m_LocalPosition（FixColliderCenters 已验证此路径
///   能正确写盘 prefab 实例 override），ApplyModifiedProperties 会正确替换旧条目
///
/// 菜单：Tools/Fix Model Roots & Save（英文）
/// </summary>
public static class FixModelRoots
{
    [MenuItem("Tools/Fix Model Roots & Save")]
    public static void Fix()
    {
        bool changed = false;
        foreach (var go in Object.FindObjectsOfType<GameObject>(true))
        {
            Transform modelRoot = null;
            float y = 0f;
            if (go.name == "MeleePlayer") { modelRoot = go.transform.Find("man"); y = -1.08f; }
            else if (go.name == "RangedPlayer") { modelRoot = go.transform.Find("Female"); y = -0.85f; }
            if (modelRoot == null) continue;

            // 用 SerializedObject 修改 m_LocalPosition（prefab 实例才正确替换 override 写盘）
            var so = new SerializedObject(modelRoot);
            var pos = so.FindProperty("m_LocalPosition");
            if (pos == null) { so.Dispose(); continue; }
            pos.vector3Value = new Vector3(0f, y, 0f);
            so.ApplyModifiedProperties();
            so.Dispose();
            EditorUtility.SetDirty(modelRoot);
            changed = true;
            Debug.Log($"[FixRoots] {modelRoot.name}: m_LocalPosition → (0, {y}, 0)（SerializedObject）");
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FixRoots] 模型根已修复，场景已保存（EditorSceneManager.SaveScene）");
        }
        else
        {
            Debug.Log("[FixRoots] 未找到模型根，未改动");
        }
    }
}

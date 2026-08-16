using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 修复角色碰撞体中心（配合角色 GO 高度调整，让胶囊体保持 0~2m 贴地）：
/// 男 MeleePlayer  GO=1.20 → CC/CapsuleCollider center.y = -0.20
/// 女 RangedPlayer  GO=0.95 → CC/CapsuleCollider center.y = +0.05
/// 直接改组件并 SetDirty + 保存场景（scene_save 不写 inactive 对象的 override，需代码落盘）
/// 菜单：Tools/Fix Collider Centers（英文）
/// </summary>
public static class FixColliderCenters
{
    [MenuItem("Tools/Fix Collider Centers")]
    public static void Fix()
    {
        int fixedCount = 0;
        foreach (var go in Object.FindObjectsOfType<GameObject>(true))
        {
            if (go.name != "MeleePlayer" && go.name != "RangedPlayer") continue;
            float cy = go.name == "MeleePlayer" ? -0.2f : 0.05f;

            // 用 SerializedObject 修改（prefab 实例才能生成 PropertyModification override 写进场景）
            foreach (Component comp in new Component[] { go.GetComponent<CharacterController>(), go.GetComponent<CapsuleCollider>() })
            {
                if (comp == null) continue;
                var so = new SerializedObject(comp);
                var center = so.FindProperty("m_Center");
                if (center == null) { so.Dispose(); continue; }
                var v = center.vector3Value;
                center.vector3Value = new Vector3(v.x, cy, v.z);
                so.ApplyModifiedProperties();
                so.Dispose();
                EditorUtility.SetDirty(comp);
                fixedCount++;
            }
            Debug.Log($"[FixCenters] {go.name}: CC/CapsuleCollider center.y → {cy}");
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[FixCenters] 完成：共设置 {fixedCount} 个组件，场景已保存");
    }
}

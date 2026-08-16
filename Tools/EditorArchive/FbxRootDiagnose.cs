using UnityEditor;
using UnityEngine;

/// <summary>
/// 诊断 FBX 模型根偏移：打印模型内部根/骨骼 localPosition 与实例化后的渲染脚底高度，
/// 用于确定场景中模型实例的正确 Y 偏移（修复角色嵌地/悬空）
/// 菜单：工具/诊断FBX根偏移（Tools/Diagnose Fbx Root）
/// </summary>
public static class FbxRootDiagnose
{
    [MenuItem("Tools/Diagnose Fbx Root")]
    public static void Diagnose()
    {
        // 1. 打印模型 prefab 内部 transform（= fbx Lcl Translation，被场景 override 覆盖前的默认值）
        string[] paths = {
            "Assets/Art/Models/Female.fbx",
            "Assets/Art/Models/man.fbx"
        };
        foreach (var path in paths)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                Debug.LogError($"[FbxRoot] 加载失败: {path}");
                continue;
            }
            Debug.Log($"[FbxRoot] === {path} === 根: {go.name} localPos={go.transform.localPosition} localScale={go.transform.localScale}");
            foreach (Transform child in go.transform)
            {
                Debug.Log($"[FbxRoot]   子物体: {child.name} localPos={child.localPosition}");
            }
        }

        // 2. 实例化（无 override）计算渲染脚底：MeshRenderer bounds.min.y = 网格最低点
        foreach (var path in paths)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(go);
            if (inst == null) { Debug.LogError($"[FbxRoot] 实例化失败: {path}"); continue; }

            // 隐藏场景污染：临时对象放在原点
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;

            float minY = float.MaxValue;
            var renderers = inst.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
            {
                var b = r.bounds;
                if (b.min.y < minY) minY = b.min.y;
                Debug.Log($"[FbxRoot]   {path} 网格 {r.name}: bounds.center={b.center} min.y={b.min.y:F3} max.y={b.max.y:F3}");
            }
            float height = renderers.Length > 0 ? GetHeight(renderers) : 0f;
            Debug.Log($"[FbxRoot]   {path} 渲染脚底(无 override 世界 Y) = {minY:F3}，模型高度 ≈ {height:F3}");
            Debug.Log($"[FbxRoot]   结论建议: 该模型实例的 Y override 应为 {-minY:F3}（使脚底贴地 y=0）");

            Object.DestroyImmediate(inst);
        }
    }

    private static float GetHeight(MeshRenderer[] renderers)
    {
        var allMin = float.MaxValue;
        var allMax = float.MinValue;
        foreach (var r in renderers)
        {
            var b = r.bounds;
            if (b.min.y < allMin) allMin = b.min.y;
            if (b.max.y > allMax) allMax = b.max.y;
        }
        return allMax - allMin;
    }
}

using UnityEngine;

/// <summary>
/// 一次性验证脚本：打印 Female 实例各网格的运行时材质和主贴图（验证贴图链路）
/// </summary>
public class FemaleMatVerify : MonoBehaviour
{
    private void Start()
    {
        var female = GameObject.Find("Female");
        if (female == null)
        {
            Debug.Log("[Verify] Female 未找到");
            return;
        }

        var renderers = female.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var r in renderers)
        {
            var m = r.sharedMaterial;
            string tex = m != null && m.mainTexture != null ? m.mainTexture.name : "null";
            Debug.Log($"[Verify] {r.name} 材质: {(m != null ? m.name : "null")} 主贴图: {tex} 网格: {(r.sharedMesh != null ? r.sharedMesh.name : "null")}");
        }
    }
}

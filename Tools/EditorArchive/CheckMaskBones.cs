using UnityEditor;
using UnityEngine;

/// <summary>
/// 检查 AvatarMask 的实际激活骨骼（诊断 mask 是否与角色骨骼匹配）
/// 菜单：工具/检查遮罩骨骼（英文别名 Tools/CheckMaskBones）
/// </summary>
public static class CheckMaskBones
{
    [MenuItem("工具/检查遮罩骨骼", false, 1005)]
    [MenuItem("Tools/CheckMaskBones", false, 1005)]
    public static void Check()
    {
        Check("Assets/Art/Masks/FemaleUpperBody.mask");
        Check("Assets/Art/Masks/UpperBody.mask");
    }

    private static void Check(string path)
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if (mask == null) { Debug.LogError($"[mask] 加载失败: {path}"); return; }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[mask] {mask.name} | transformCount={mask.transformCount}");
        for (int i = 0; i < mask.transformCount; i++)
        {
            string p = mask.GetTransformPath(i);
            bool active = mask.GetTransformActive(i);
            sb.AppendLine($"  [{i}] {p} active={active}");
        }
        // 人形骨骼组状态
        foreach (AvatarMaskBodyPart part in System.Enum.GetValues(typeof(AvatarMaskBodyPart)))
        {
            sb.AppendLine($"  humanoid {part} = {mask.GetHumanoidBodyPartActive(part)}");
        }
        Debug.Log(sb.ToString());
    }
}

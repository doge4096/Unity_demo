using UnityEditor;
using UnityEngine;

/// <summary>
/// 修复 FemaleUpperBody.mask 的 humanoid 骨骼位：当前 mask 的 humanoid 位全 True
/// （含 LeftLeg/RightLeg），Humanoid Animator 按 humanoid 骨骼组过滤时腿部未被遮挡
/// → 射击动画（全身体）腿部曲线透传 → 走路射击时腿被定成站立姿势
///
/// 修复：关闭下肢/根骨骼 humanoid 位（LeftLeg/RightLeg/LeftFootIK/RightFootIK/Root），
/// 保留上肢（Body/Head/双臂/手指/手 IK）。
/// 菜单：工具/修复女性遮罩人形位（英文别名 Tools/FixFemaleMask）
/// </summary>
public static class FixFemaleMask
{
    private const string MaskPath = "Assets/Art/Masks/FemaleUpperBody.mask";

    [MenuItem("工具/修复女性遮罩人形位", false, 1005)]
    [MenuItem("Tools/FixFemaleMask", false, 1005)] // 英文别名给 MCP 调用
    public static void Fix()
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null)
        {
            Debug.LogError($"[mask修复] 找不到遮罩: {MaskPath}");
            return;
        }

        // 关闭：根 + 下肢（腿部曲线被遮挡，由 Base 层驱动）
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);

        // 开启：躯干 + 头 + 双臂 + 手指 + 手 IK（上半身动画正常透传）
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);

        EditorUtility.SetDirty(mask);
        AssetDatabase.SaveAssets();

        Debug.Log($"[mask修复] {mask.name} humanoid 位已修复：腿/根关闭，躯干/头/臂开启。控制器引用同一资产，自动生效");
    }
}

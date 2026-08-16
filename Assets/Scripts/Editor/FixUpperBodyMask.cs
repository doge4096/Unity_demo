using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 修复射击时下半身被定住问题：给 FemaleAnimator 控制器的 UpperBody 层挂上
/// AvatarMask（FemaleUpperBody.mask，仅包含躯干+头部+双臂，不含腿部）
///
/// 根因：控制器第二层从未挂过 mask，射击动画（female_shoot_fixed 全身体动画）
/// 在 UpperBody 层播放时腿部曲线透传 → 走路时射击，腿被定成站立姿势滑行
/// 菜单：工具/修复射击分层遮罩（英文别名 Tools/FixUpperBodyMask）
/// </summary>
public static class FixUpperBodyMask
{
    private const string ControllerPath = "Assets/Art/Animators/FemaleAnimator.controller";
    private const string MaskPath = "Assets/Art/Masks/FemaleUpperBody.mask";

    [MenuItem("工具/修复射击分层遮罩", false, 1003)]
    [MenuItem("Tools/FixUpperBodyMask", false, 1003)] // 英文别名给 MCP 调用
    public static void Fix()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[遮罩] 找不到控制器: {ControllerPath}");
            return;
        }
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null)
        {
            Debug.LogError($"[遮罩] 找不到遮罩资产: {MaskPath}");
            return;
        }

        if (controller.layers.Length < 2)
        {
            Debug.LogError("[遮罩] 控制器层数不足（需要 Base + UpperBody 两层）");
            return;
        }

        var layers = controller.layers;
        var old = layers[1].avatarMask;
        layers[1].avatarMask = mask;
        controller.layers = layers;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log($"[遮罩] UpperBody 层遮罩: {(old != null ? old.name : "无")} → {mask.name} | " +
                  $"层={controller.layers[1].name} weight={controller.layers[1].defaultWeight}");
    }

    /// <summary>
    /// 检查当前控制器各层遮罩挂载情况（辅助验证）
    /// 菜单：工具/检查分层遮罩（英文别名 Tools/DumpLayerMask）
    /// </summary>
    [MenuItem("工具/检查分层遮罩", false, 1003)]
    [MenuItem("Tools/DumpLayerMask", false, 1003)]
    public static void Dump()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) return;
        for (int i = 0; i < controller.layers.Length; i++)
        {
            var l = controller.layers[i];
            var m = l.avatarMask;
            Debug.Log($"[遮罩] 层{i} '{l.name}': mask={(m != null ? m.name : "无")} weight={l.defaultWeight}");
        }
    }
}

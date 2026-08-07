using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

/// <summary>
/// 用 Unity API 重新绑定 controller 的 mask 引用（绕过手写 YAML 的解析问题）
/// 菜单「工具/重新绑定mask引用」（Tools/Reassign Mask Ref）
/// </summary>
public static class MaskBindFixer
{
    [MenuItem("工具/重新绑定mask引用")]
    [MenuItem("Tools/Reassign Mask Ref")]
    public static void Run()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Art/Animators/MeleeAnimator.controller");
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(
            "Assets/Art/Masks/UpperBody.mask");
        var sb = new System.Text.StringBuilder();
        if (ctrl == null) { sb.AppendLine("controller 加载失败"); }
        else if (mask == null) { sb.AppendLine("mask 加载失败"); }
        else
        {
            sb.AppendLine($"controller 层数={ctrl.layers.Length} mask transform数={mask.transformCount}");
            // 第 1 层（UpperBody）重新绑定 mask
            var layers = ctrl.layers;
            if (layers.Length > 1)
            {
                layers[1].avatarMask = mask;
                ctrl.layers = layers;
                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                sb.AppendLine($"已绑定 mask 到层 '{ctrl.layers[1].name}'，新引用 avatarMask={(ctrl.layers[1].avatarMask != null ? ctrl.layers[1].avatarMask.name : "null")}");
            }
            else
            {
                sb.AppendLine("controller 只有 1 层，无法绑定");
            }
        }
        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/mask_bind.txt", sb.ToString());
        Debug.Log("[MaskBind] " + sb.ToString());
    }
}

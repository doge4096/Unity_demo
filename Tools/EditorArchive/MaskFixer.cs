using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 修复 UpperBody.mask — 旧资产 YAML 手写格式导致 Unity 无法加载（返回 null）
/// 用 Unity API 重建（幂等：只有当前资产不可加载时才重建）
/// 启用上半身部位（躯干/头/双臂/双手/手指），腿部保持关闭
/// 新 guid 写入 Assets/Screenshots/mask_guid.txt 供 controller 引用更新
/// 由 AnimStateDiagnose 菜单手动调用（不自动执行，避免覆盖手动修改）
/// </summary>
public static class MaskFixer
{
    public const string MaskPath = "Assets/Art/Masks/UpperBody.mask";
    private const string AbsPath = "D:/Project/unity/interview/Assets/Art/Masks/UpperBody.mask";
    private const string ResultFile = "D:/Project/unity/interview/Assets/Screenshots/mask_guid.txt";

    public static void Fix(bool force = false)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[Fix] 开始执行 " + System.DateTime.Now.ToString("HH:mm:ss") + " force=" + force);
        try
        {
            // 幂等检查：可加载 且 已有 transform mask 才跳过（Generic 模型必须靠 transform mask 生效）
            var existing = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            sb.AppendLine("[Fix] 现有资产 LoadAssetAtPath: " + (existing != null ? "成功" : "null") + " transform数=" + (existing != null ? existing.transformCount.ToString() : "?"));
            if (existing != null && !force && existing.transformCount > 0)
            {
                File.WriteAllText(ResultFile, "skip-existing");
                sb.AppendLine("[Fix] 已有效，跳过重建");
                Debug.Log("[修复] " + sb.ToString());
                return;
            }

            // 磁盘级删除旧文件 + meta（绕过 AssetDatabase 注册状态）
            if (File.Exists(AbsPath)) { File.Delete(AbsPath); sb.AppendLine("[Fix] 已删旧文件"); }
            if (File.Exists(AbsPath + ".meta")) { File.Delete(AbsPath + ".meta"); sb.AppendLine("[Fix] 已删旧 meta"); }
            AssetDatabase.Refresh();

            var mask = new AvatarMask();   // AvatarMask 继承自 Object，直接 new（不是 ScriptableObject）
            mask.name = "UpperBody";

            // 上半身部位启用（枚举：无 LeftHand/RightHand，手部由 LeftHandIK/RightHandIK 控制）
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
            // 下半身关闭：髋部(Root)/腿/脚全部交给 Base Layer 的移动动画，否则上层会覆盖腿脚
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);

            // TransformMask（mixamorig 骨骼路径）——man.fbx 是 Generic 类型，humanoid 部位不生效，
            // 必须用骨骼路径匹配（Generic 模型只认 transform mask）。
            // 用 AddTransformPath 从场景骨骼对象生成路径（保证格式与动画 clip 曲线路径一致；
            // SetTransformPath 手写字符串不设权重导致 m_Weight=0 无效）
            var manAnim = GameObject.Find("MeleePlayer/man")?.GetComponent<Animator>();
            if (manAnim != null)
            {
                var spine = manAnim.transform.Find("mixamorig:Hips/mixamorig:Spine");
                if (spine != null)
                {
                    // 递归添加 Spine 及所有子骨骼（Spine1/Spine2/Neck/Head/双臂/手/手指 = 完整上半身，不含腿）
                    mask.AddTransformPath(spine, true);
                    sb.AppendLine("[Fix] AddTransformPath 从场景骨骼生成，transformCount=" + mask.transformCount);
                }
                else
                {
                    sb.AppendLine("[Fix] 未找到场景骨骼 mixamorig:Hips/mixamorig:Spine");
                }
            }
            else
            {
                sb.AppendLine("[Fix] 未找到 MeleePlayer/man（场景未打开或对象不存在）");
            }

            AssetDatabase.CreateAsset(mask, MaskPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            sb.AppendLine("[Fix] CreateAsset 成功");

            var guid = AssetDatabase.AssetPathToGUID(MaskPath);
            File.WriteAllText(ResultFile, guid);
            sb.AppendLine("[Fix] 新 guid=" + guid);
        }
        catch (System.Exception e)
        {
            sb.AppendLine("[Fix] 异常: " + e);
            // 异常也写入结果文件（避免控制台查不到）
            try { File.WriteAllText(ResultFile, "ERROR: " + e); } catch { }
        }
        Debug.Log("[修复] " + sb.ToString());
    }
}

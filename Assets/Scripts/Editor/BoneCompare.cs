using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

/// <summary>
/// 对比模型与动画的骨骼位置（排查「骨骼长度不匹配」报错）
/// 菜单「工具/对比骨骼位置」
/// </summary>
public static class BoneCompare
{
    [MenuItem("工具/对比骨骼位置")]
    [MenuItem("Tools/Compare Bones")]
    public static void Run()
    {
        var sb = new StringBuilder();
        // 读模型 Avatar 的骨骼位置（humanoid 骨骼）
        var modelAvatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/Art/Models/man.fbx");
        var animAvatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/Art/Animations/man_attack1.fbx");

        sb.AppendLine("== 模型 vs 动画 骨骼位置对比 ==");
        if (modelAvatar == null) { sb.AppendLine("模型 Avatar 加载失败"); }
        if (animAvatar == null) { sb.AppendLine("动画 Avatar 加载失败"); }

        if (modelAvatar != null && animAvatar != null)
        {
            // 通过 humanDescription 读骨骼位置（Avatar 资产）
            var bones = new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot, HumanBodyBones.Spine, HumanBodyBones.Head };
            foreach (var b in bones)
            {
                var mT = GameObject.Find("MeleePlayer/man")?.transform;
                // 从 meta 读 humanDescription.skeleton（更可靠——不依赖场景对象）
                var mPos = GetSkeletonPos("Assets/Art/Models/man.fbx.meta", b);
                var aPos = GetSkeletonPos("Assets/Art/Animations/man_attack1.fbx.meta", b);
                if (mPos.HasValue && aPos.HasValue)
                {
                    float dx = (mPos.Value - aPos.Value).magnitude * 1000f;  // 转 mm
                    sb.AppendLine($"{b}: 模型{mPos.Value} 动画{aPos.Value} 差异={dx:F1}mm");
                }
                else
                {
                    sb.AppendLine($"{b}: 位置数据缺失（模型{mPos.HasValue} 动画{aPos.HasValue}）");
                }
            }
        }

        // 对比 git 版本的动画骨骼（旧动画是否匹配）
        sb.AppendLine("== git 旧版动画骨骼 ==");
        var oldMeta = "/tmp/anim_backup_0808/man_attack1.fbx.meta";
        if (File.Exists(oldMeta))
        {
            foreach (var b in new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftFoot })
            {
                var oldPos = GetSkeletonPos(oldMeta, b);
                sb.AppendLine($"旧动画 {b}: {oldPos}");
            }
        }
        else { sb.AppendLine("旧动画 meta 备份不存在"); }

        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/bone_compare.txt", sb.ToString());
        Debug.Log("[对比骨骼] 完成");
    }

    /// <summary>从 meta 的 humanDescription.skeleton 读骨骼位置（按 humanName 找 boneName 的骨骼）</summary>
    private static Vector3? GetSkeletonPos(string metaPath, HumanBodyBones bone)
    {
        try
        {
            var c = File.ReadAllText(metaPath);
            // human 映射：找该骨骼的 boneName
            var humanMatch = System.Text.RegularExpressions.Regex.Match(c,
                @"boneName: (\S+)\n\s+humanName: " + bone);
            if (!humanMatch.Success) return null;
            var boneName = humanMatch.Groups[1].Value;
            // skeleton 里找该骨骼的位置
            var skelMatch = System.Text.RegularExpressions.Regex.Match(c,
                @"- name: " + System.Text.RegularExpressions.Regex.Escape(boneName) + @"\n\s+parentName: \S*\n\s+position: \{x: ([\d.eE-]+), y: ([\d.eE-]+), z: ([\d.eE-]+)\}");
            if (!skelMatch.Success) return null;
            return new Vector3(float.Parse(skelMatch.Groups[1].Value),
                float.Parse(skelMatch.Groups[2].Value),
                float.Parse(skelMatch.Groups[3].Value));
        }
        catch { return null; }
    }
}

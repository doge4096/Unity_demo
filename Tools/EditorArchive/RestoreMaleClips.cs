using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;

/// <summary>
/// 回退男角色动画：Fixed 目录下 man_*.anim 全部重置为 FBX 原版曲线（原地写回，guid 不变，控制器引用自动生效）
/// 背景：v3 全局高斯平滑误伤了原本正常的男角色动画（man_Run/man_Walking 等被修 64-481 帧"尖峰"）
/// 男角色动画曲线 = 原版 mixamo 数据，不再走修复管线
/// 菜单：Tools/Restore Male Clips（英文）
/// </summary>
public static class RestoreMaleClips
{
    [MenuItem("Tools/Restore Male Clips")]
    public static void Run()
    {
        var sb = new StringBuilder();
        int restored = 0;
        foreach (var fixedFile in Directory.GetFiles("Assets/Art/Animations/Fixed", "man_*.anim"))
        {
            string fbxPath = "Assets/Art/Animations/" +
                             Path.GetFileNameWithoutExtension(fixedFile).Replace("_fixed", "") + ".fbx";
            var fixedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fixedFile);
            var src = AssetDatabase.LoadAssetAtPath<AnimationClip>(fbxPath);
            if (fixedClip == null || src == null)
            {
                sb.AppendLine($"{Path.GetFileName(fixedFile)}: 加载失败（跳过）");
                continue;
            }

            // 先清空 Fixed 现有全部曲线，再复制原版曲线
            foreach (var b in AnimationUtility.GetCurveBindings(fixedClip))
                AnimationUtility.SetEditorCurve(fixedClip, b, null);
            int curves = 0;
            foreach (var b in AnimationUtility.GetCurveBindings(src))
            {
                var c = AnimationUtility.GetEditorCurve(src, b);
                if (c == null) continue;
                AnimationUtility.SetEditorCurve(fixedClip, b, c);
                curves++;
            }
            // 同步循环/根运动等导入设置
            var settings = AnimationUtility.GetAnimationClipSettings(src);
            AnimationUtility.SetAnimationClipSettings(fixedClip, settings);
            EditorUtility.SetDirty(fixedClip);
            sb.AppendLine($"{Path.GetFileName(fixedFile)}: 已恢复原版曲线（{curves} 条）");
            restored++;
        }
        AssetDatabase.SaveAssets();
        sb.AppendLine($"\n共恢复 {restored} 个男角色动画（guid 不变，控制器引用无需重建）");

        var outPath = "Assets/Screenshots/restore_male.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[RestoreMale] 完成，结果: " + outPath);
    }
}

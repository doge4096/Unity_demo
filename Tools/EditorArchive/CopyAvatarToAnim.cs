using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 把动画 fbx 的 Avatar 改为从角色模型 Female.fbx 复制（Avatar Definition: Copy From Other Avatar），
/// 让动画重定向使用角色骨骼比例，并尝试消除 "Avatar Rig Configuration mis-match" 警告。
/// 菜单：工具/复制角色Avatar到动画（英文别名 Tools/CopyAvatarToAnim）
/// </summary>
public static class CopyAvatarToAnim
{
    private const string AnimPath = "Assets/Art/Animations/female_aimWalk.fbx";
    private const string ModelPath = "Assets/Art/Models/Female.fbx";

    [MenuItem("工具/复制角色Avatar到动画", false, 1130)]
    [MenuItem("Tools/CopyAvatarToAnim", false, 1130)]
    public static void Run()
    {
        var sb = new StringBuilder();
        var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(ModelPath);
        if (avatar == null)
        {
            sb.AppendLine($"角色模型 Avatar 加载失败: {ModelPath}");
            Debug.Log(sb.ToString());
            return;
        }

        var importer = AssetImporter.GetAtPath(AnimPath) as ModelImporter;
        if (importer == null)
        {
            sb.AppendLine($"动画文件加载失败: {AnimPath}");
            Debug.Log(sb.ToString());
            return;
        }

        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = avatar;
        importer.SaveAndReimport();

        sb.AppendLine($"已设置 {AnimPath} Avatar 复制自 {ModelPath}（{avatar.name}）并重新导入");
        Debug.Log(sb.ToString());
        try
        {
            System.IO.Directory.CreateDirectory("Assets/Screenshots");
            System.IO.File.WriteAllText("Assets/Screenshots/copy_avatar.txt", sb.ToString());
        }
        catch { }
    }
}

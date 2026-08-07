using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 把近战动画 fbx 的 Avatar 绑定到 man.fbx 的 Avatar（CopyFromOther）
/// Humanoid 动画必须引用模型的 Avatar 才能正确映射骨骼
/// 菜单「工具/绑定动画Avatar」
/// </summary>
public static class AvatarBindFixer
{
    [MenuItem("工具/绑定动画Avatar")]
    [MenuItem("Tools/Bind Anim Avatar")]
    public static void Run()
    {
        var modelAvatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/Art/Models/man.fbx");
        var sb = new System.Text.StringBuilder();
        if (modelAvatar == null)
        {
            sb.AppendLine("man.fbx 的 Avatar 加载失败!");
        }
        else
        {
            string[] anims = { "man_Idle", "man_Walking", "man_Run", "man_attack1", "man_attack2", "man_attack3",
                "man_hitreaction", "man_death", "man_blockIdle", "man_blockHit", "Jump_start", "Jump_loop", "jump_land" };
            foreach (var name in anims)
            {
                string path = "Assets/Art/Animations/" + name + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) { sb.AppendLine(name + ": 无 importer"); continue; }
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = modelAvatar;
                importer.SaveAndReimport();
                sb.AppendLine(name + ": 已绑定 Avatar");
            }
        }
        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/avatar_bind.txt", sb.ToString());
        Debug.Log("[AvatarBind] " + sb.ToString());
    }
}

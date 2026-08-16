using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 给场景中 man 的 Animator 绑定 man.fbx 的 Avatar（转 Humanoid 后必须绑定，否则 T-pose）
/// 菜单「工具/绑定角色Avatar」
/// </summary>
public static class ManAvatarBinder
{
    [MenuItem("工具/绑定角色Avatar")]
    [MenuItem("Tools/Bind Man Avatar")]
    public static void Bind()
    {
        var sb = new System.Text.StringBuilder();
        // FindObjectsOfTypeAll 能找未激活对象
        var man = default(GameObject);
        foreach (var a in Resources.FindObjectsOfTypeAll<Animator>())
        {
            if (a.gameObject.name == "man" && a.gameObject.scene.IsValid())
            {
                man = a.gameObject;
                break;
            }
        }
        if (man == null) { sb.AppendLine("未找到 MeleePlayer/man"); }
        else
        {
            var anim = man.GetComponent<Animator>();
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/Art/Models/man.fbx");
            if (anim == null) { sb.AppendLine("man 无 Animator"); }
            else if (avatar == null) { sb.AppendLine("man.fbx 的 Avatar 加载失败"); }
            else
            {
                anim.avatar = avatar;
                EditorUtility.SetDirty(anim);
                EditorSceneManager.SaveOpenScenes();
                sb.AppendLine("已绑定 Avatar: " + avatar.name + " (isHuman=" + avatar.isHuman + ") 场景已保存");
            }
        }
        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/avatar_bind_result.txt", sb.ToString());
        Debug.Log("[绑定Avatar] " + sb.ToString());
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 强制重新加载场景（从磁盘读取最新版本——修复 Unity 内存场景与磁盘不一致的问题）
/// 菜单「工具/重载场景」
/// </summary>
public static class SceneReloader
{
    [MenuItem("工具/重载场景")]
    [MenuItem("Tools/Reload Scene")]
    public static void Run()
    {
        var path = "Assets/Scenes/SampleScene.unity";
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/scene_reload.txt",
            "场景已重载: " + scene.name);
    }
}

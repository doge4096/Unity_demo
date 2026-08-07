using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

/// <summary>
/// 用 Unity API 强制重新序列化 controller（重写 motion/mask 引用为 API 格式）
/// 修复「资产引用正确但运行时解析失败」的问题（与 mask 引用同因）
/// 菜单「工具/重写控制器引用」
/// </summary>
public static class ControllerReserializer
{
    [MenuItem("工具/重写控制器引用")]
    [MenuItem("Tools/Reserialize Controller")]
    public static void Run()
    {
        var path = "Assets/Art/Animators/MeleeAnimator.controller";
        // 先卸载缓存再重新加载（文件被外部覆盖后 Unity 可能返回旧缓存对象）
        var old = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (old != null) Resources.UnloadAsset(old);
        AssetDatabase.Refresh();
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        var sb = new System.Text.StringBuilder();
        if (ctrl == null) { sb.AppendLine("controller 加载失败!"); }
        else
        {
            // 统计引用状态
            sb.AppendLine($"层数={ctrl.layers.Length} 参数数={ctrl.parameters.Length}");
            foreach (var layer in ctrl.layers)
            {
                sb.AppendLine($"层 '{layer.name}' mask={(layer.avatarMask != null ? layer.avatarMask.name : "无")}");
                foreach (var cs in layer.stateMachine.states)
                {
                    var m = cs.state.motion;
                    sb.AppendLine($"  {cs.state.name} -> motion={(m != null ? m.name : "NULL!")}");
                }
            }
            // 强制重新序列化
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            sb.AppendLine("已强制重新序列化并保存");
        }
        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/controller_reserialize.txt", sb.ToString());
        Debug.Log("[重写] " + sb.ToString());
    }
}

using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

/// <summary>
/// 用 Unity API 重新绑定 controller 所有状态的 motion/mask 引用
/// （原 YAML 引用的运行时烘焙失败——GetCurrentAnimatorClipInfo 为空）
/// 菜单「工具/重绑运动引用」
/// </summary>
public static class MotionRebinder
{
    [MenuItem("工具/重绑运动引用")]
    [MenuItem("Tools/Rebind Motions")]
    public static void Run()
    {
        var path = "Assets/Art/Animators/MeleeAnimator.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        var sb = new System.Text.StringBuilder();
        if (ctrl == null) { sb.AppendLine("controller 加载失败!"); }
        else
        {
            int rebound = 0, failed = 0;
            foreach (var layer in ctrl.layers)
            {
                foreach (var cs in layer.stateMachine.states)
                {
                    var m = cs.state.motion;
                    if (m == null) continue;
                    // 通过 clip 资产路径重新加载并赋值（API 格式引用）
                    string assetPath = AssetDatabase.GetAssetPath(m);
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                    if (clip != null)
                    {
                        cs.state.motion = clip;
                        rebound++;
                    }
                    else { failed++; sb.AppendLine($"  {cs.state.name}: 重绑失败（{assetPath}）"); }
                }
                sb.AppendLine($"层 '{layer.name}': mask={(layer.avatarMask != null ? layer.avatarMask.name : "无")}");
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            sb.AppendLine($"完成: 重绑 {rebound} 个 motion, 失败 {failed}");
        }
        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/motion_rebind.txt", sb.ToString());
        Debug.Log("[重绑] " + sb.ToString());
    }
}

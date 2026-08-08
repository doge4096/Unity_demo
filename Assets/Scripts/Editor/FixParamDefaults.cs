using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

/// <summary>
/// 修复 controller 参数默认值（Rebuild 时 API 创建参数默认全 0，导致 AnimSpeed=0 动画冻结）
/// 速度类参数默认 1；IsGrounded 默认 true；其余保持 0
/// 菜单「工具/修复参数默认值」
/// </summary>
public static class FixParamDefaults
{
    [MenuItem("工具/修复参数默认值")]
    [MenuItem("Tools/Fix Param Defaults")]
    public static void Run()
    {
        var path = "Assets/Art/Animators/MeleeAnimator.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        var sb = new System.Text.StringBuilder();
        if (ctrl == null) { sb.AppendLine("controller 加载失败!"); }
        else
        {
            var ps = ctrl.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                // 速度类参数默认 1
                if (p.name == "AnimSpeed" || p.name == "AttackSpeed" || p.name == "JumpSpeed" ||
                    p.name == "BlockSpeed" || p.name == "HitSpeed" || p.name == "DieSpeed")
                {
                    if (p.defaultFloat != 1f)
                    {
                        p.defaultFloat = 1f;
                        sb.AppendLine($"{p.name}: 默认值 0 -> 1");
                    }
                }
                // IsGrounded 默认 true
                if (p.name == "IsGrounded")
                {
                    if (p.defaultBool != true)
                    {
                        p.defaultBool = true;
                        sb.AppendLine("IsGrounded: 默认 false -> true");
                    }
                }
            }
            ctrl.parameters = ps;
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            sb.AppendLine("完成");
        }
        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/param_fix.txt", sb.ToString());
        Debug.Log("[参数修复] " + sb.ToString());
    }
}

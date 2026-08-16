using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

/// <summary>
/// 格挡分层：与攻击对称
/// - 原地格挡（Speed<=0.1）：Base Layer 全身格挡（Block 状态）
/// - 移动格挡（Speed>0.1）：UpperBody 层上半身格挡（UBlock 状态），腿保持走路/跑步
/// 菜单「工具/添加格挡分层」
/// </summary>
public static class BlockLayerAdder
{
    [MenuItem("工具/添加格挡分层")]
    [MenuItem("Tools/Add Block Layer")]
    public static void Run()
    {
        var path = "Assets/Art/Animators/MeleeAnimator.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        var sb = new System.Text.StringBuilder();
        if (ctrl == null) { sb.AppendLine("controller 加载失败!"); }
        else
        {
            // ===== 1. Base Layer：IsBlocking 过渡加 Speed<=0.1（原地才全身格挡）=====
            var baseSM = ctrl.layers[0].stateMachine;
            int baseFixed = 0;
            foreach (var t in baseSM.anyStateTransitions)
            {
                bool hasBlock = false;
                foreach (var c in t.conditions)
                    if (c.parameter == "IsBlocking" && c.mode == AnimatorConditionMode.If) hasBlock = true;
                if (hasBlock)
                {
                    // 检查是否已有 Speed 条件，没有则添加 Speed<=0.1
                    bool hasSpeed = false;
                    foreach (var c in t.conditions)
                        if (c.parameter == "Speed") hasSpeed = true;
                    if (!hasSpeed)
                    {
                        t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
                        baseFixed++;
                    }
                }
            }
            sb.AppendLine($"Base Layer 格挡过渡已加原地条件: {baseFixed} 处");

            // ===== 2. UpperBody 层：添加 UBlock 状态与过渡 =====
            var upperSM = ctrl.layers[1].stateMachine;
            // 检查是否已有 UBlock（幂等）
            bool hasUBlock = false;
            foreach (var cs in upperSM.states)
                if (cs.state.name == "UBlock") hasUBlock = true;

            if (!hasUBlock)
            {
                var ublock = upperSM.AddState("UBlock");
                ublock.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/man_blockIdle.fbx");
                ublock.speedParameterActive = true;
                ublock.speedParameter = "BlockSpeed";

                // AnyState -> UBlock（IsBlocking + Speed>0.1）
                var inT = upperSM.AddAnyStateTransition(ublock);
                inT.AddCondition(AnimatorConditionMode.If, 0, "IsBlocking");
                inT.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                inT.duration = 0.08f;

                // UBlock -> Empty（IsBlocking false 松开格挡）
                var empty = upperSM.defaultState;
                var outT = ublock.AddTransition(empty);
                outT.AddCondition(AnimatorConditionMode.IfNot, 0, "IsBlocking");
                outT.duration = 0.08f;

                sb.AppendLine("UpperBody 层已添加 UBlock 状态与过渡");
            }
            else { sb.AppendLine("UBlock 已存在，跳过"); }

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            sb.AppendLine("完成");
        }
        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/block_layer.txt", sb.ToString());
        Debug.Log("[格挡分层] " + sb.ToString());
    }
}

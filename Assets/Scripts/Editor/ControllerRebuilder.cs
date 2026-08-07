using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

/// <summary>
/// 用 Unity API 完全重建 MeleeAnimator 控制器（绕开历史 YAML 的运行时烘焙失败）
/// 保持与原控制器完全相同的参数/状态/过渡/层结构
/// 菜单「工具/重建近战控制器」
/// </summary>
public static class ControllerRebuilder
{
    private static AnimationClip Clip(string fbx) => AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/" + fbx);

    [MenuItem("工具/重建近战控制器")]
    [MenuItem("Tools/Rebuild Melee Controller")]
    public static void Run()
    {
        var sb = new System.Text.StringBuilder();
        string outPath = "Assets/Art/Animators/MeleeAnimatorRebuilt.controller";
        try
        {
            // 删除旧的（如果存在）
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(outPath) != null)
                AssetDatabase.DeleteAsset(outPath);

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(outPath);
            var root = ctrl.layers[0].stateMachine;

            // ===== 参数 =====
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Combo", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("JumpStart", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("JumpLand", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("AnimSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("IsBlocking", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("AttackSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("JumpSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("BlockSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("HitSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("DieSpeed", AnimatorControllerParameterType.Float);

            // ===== Base Layer 状态 =====
            AnimatorState NewState(string name, AnimationClip clip, string speedParam)
            {
                var s = root.AddState(name);
                if (clip != null) s.motion = clip;
                if (!string.IsNullOrEmpty(speedParam))
                {
                    s.speedParameterActive = true;
                    s.speedParameter = speedParam;
                }
                return s;
            }

            var idle = NewState("Idle", Clip("man_Idle.fbx"), null);
            var walk = NewState("Walk", Clip("man_Walking.fbx"), "AnimSpeed");
            var run = NewState("Run", Clip("man_Run.fbx"), "AnimSpeed");
            var atk1 = NewState("Attack1", Clip("man_attack1.fbx"), "AttackSpeed");
            var atk2 = NewState("Attack2", Clip("man_attack2.fbx"), "AttackSpeed");
            var atk3 = NewState("Attack3", Clip("man_attack3.fbx"), "AttackSpeed");
            var hit = NewState("Hit", Clip("man_hitreaction.fbx"), "HitSpeed");
            var die = NewState("Die", Clip("man_death.fbx"), "DieSpeed");
            var js = NewState("JumpStart", Clip("Jump_start.fbx"), "JumpSpeed");
            var jl = NewState("JumpLoop", Clip("Jump_loop.fbx"), "JumpSpeed");
            var jld = NewState("JumpLand", Clip("jump_land.fbx"), "JumpSpeed");
            var block = NewState("Block", Clip("man_blockIdle.fbx"), "BlockSpeed");
            var blockHit = NewState("BlockHit", Clip("man_blockHit.fbx"), "BlockSpeed");
            root.defaultState = idle;

            // ===== 状态间过渡 =====
            idle.AddTransition(walk).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            walk.AddTransition(idle).AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            walk.AddTransition(run).AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
            run.AddTransition(walk).AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed");
            var tA1 = atk1.AddTransition(idle); tA1.hasExitTime = true; tA1.exitTime = 0.9f; tA1.duration = 0.1f;
            var tA2 = atk2.AddTransition(idle); tA2.hasExitTime = true; tA2.exitTime = 0.9f; tA2.duration = 0.1f;
            var tA3 = atk3.AddTransition(idle); tA3.hasExitTime = true; tA3.exitTime = 0.9f; tA3.duration = 0.1f;
            var tHit = hit.AddTransition(idle); tHit.hasExitTime = true; tHit.exitTime = 0.9f; tHit.duration = 0.1f;
            var tDie = die.AddTransition(idle); tDie.hasExitTime = true; tDie.exitTime = 0.9f; tDie.duration = 0.1f;
            idle.AddTransition(js).AddCondition(AnimatorConditionMode.If, 0, "JumpStart");
            walk.AddTransition(js).AddCondition(AnimatorConditionMode.If, 0, "JumpStart");
            run.AddTransition(js).AddCondition(AnimatorConditionMode.If, 0, "JumpStart");
            var tJs = js.AddTransition(jl); tJs.hasExitTime = true; tJs.exitTime = 0.5f; tJs.duration = 0.1f;
            jl.AddTransition(jld).AddCondition(AnimatorConditionMode.If, 0, "JumpLand");
            jld.AddTransition(idle).AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            jld.AddTransition(walk).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var tB = block.AddTransition(idle); tB.AddCondition(AnimatorConditionMode.IfNot, 0, "IsBlocking"); tB.duration = 0.08f;
            var tBH = block.AddTransition(blockHit); tBH.AddCondition(AnimatorConditionMode.If, 0, "Hit"); tBH.duration = 0.05f;
            var tBH2 = blockHit.AddTransition(block); tBH2.hasExitTime = true; tBH2.exitTime = 0.9f; tBH2.duration = 0.08f;

            // ===== AnyState 过渡（Base Layer）=====
            var anyHit = root.AddAnyStateTransition(hit);
            anyHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            anyHit.duration = 0.05f;
            var anyDie = root.AddAnyStateTransition(die);
            anyDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
            anyDie.duration = 0.05f;
            var anyBlock = root.AddAnyStateTransition(block);
            anyBlock.AddCondition(AnimatorConditionMode.If, 0, "IsBlocking");
            anyBlock.duration = 0.08f;
            void AnyAttack(AnimatorState dst, int combo)
            {
                var t = root.AddAnyStateTransition(dst);
                t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
                t.AddCondition(AnimatorConditionMode.Equals, combo, "Combo");
                t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
                t.duration = 0.05f;
            }
            AnyAttack(atk1, 1); AnyAttack(atk2, 2); AnyAttack(atk3, 3);

            // ===== UpperBody 层 =====
            // 关键：状态机必须 AddObjectToAsset 注册为 controller 子资产，否则序列化丢失
            var sm2 = new AnimatorStateMachine { name = "UpperBody Layer" };
            AssetDatabase.AddObjectToAsset(sm2, outPath);
            var layer2 = new AnimatorControllerLayer
            {
                name = "UpperBody",
                defaultWeight = 1f,
                stateMachine = sm2,
                avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/Art/Masks/UpperBody.mask")
            };
            ctrl.AddLayer(layer2);
            var empty = sm2.AddState("Empty");
            sm2.defaultState = empty;
            var ua1 = sm2.AddState("UAttack1"); ua1.motion = Clip("man_attack1.fbx");
            ua1.speedParameterActive = true; ua1.speedParameter = "AttackSpeed";
            var ua2 = sm2.AddState("UAttack2"); ua2.motion = Clip("man_attack2.fbx");
            ua2.speedParameterActive = true; ua2.speedParameter = "AttackSpeed";
            var ua3 = sm2.AddState("UAttack3"); ua3.motion = Clip("man_attack3.fbx");
            ua3.speedParameterActive = true; ua3.speedParameter = "AttackSpeed";
            void AnyUAttack(AnimatorState dst, int combo)
            {
                var t = sm2.AddAnyStateTransition(dst);
                t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
                t.AddCondition(AnimatorConditionMode.Equals, combo, "Combo");
                t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                t.duration = 0.05f;
            }
            AnyUAttack(ua1, 1); AnyUAttack(ua2, 2); AnyUAttack(ua3, 3);
            foreach (var ua in new[] { ua1, ua2, ua3 })
            {
                var t = ua.AddTransition(empty);
                t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0.1f;
            }

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            sb.AppendLine("重建完成: " + outPath);
            sb.AppendLine($"层数={ctrl.layers.Length} 参数数={ctrl.parameters.Length} Base状态={root.states.Length} Upper状态={sm2.states.Length}");
        }
        catch (System.Exception e)
        {
            sb.AppendLine("异常: " + e);
        }
        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/controller_rebuild.txt", sb.ToString());
        Debug.Log("[重建] " + sb.ToString());
    }
}

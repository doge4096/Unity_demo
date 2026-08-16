using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

/// <summary>
/// 用 Unity API 重建 RangedAnimator 控制器（女性远程角色专用，动画全部为 female_*）
/// 修复历史 YAML 问题：Shoot 状态孤儿（未注册到状态机导致 AnyState 报错）、UShoot 层 clip 引用丢失
/// 保持原参数集（RangedCharacter 驱动 Attack）与结构（移动/瞄准 blend tree/上身层）
/// 菜单「工具/重建远程控制器」
/// </summary>
public static class RangedControllerRebuilder
{
    /// <summary>加载动画：优先 Fixed/*.anim（坏帧已修复），没有 Fixed 版本则用原 FBX clip（如 Idle/aimIdle/floating 无坏帧）</summary>
    private static AnimationClip Clip(string fbx)
    {
        string fixedPath = "Assets/Art/Animations/Fixed/" + Path.GetFileNameWithoutExtension(fbx) + "_fixed.anim";
        var fc = AssetDatabase.LoadAssetAtPath<AnimationClip>(fixedPath);
        if (fc != null) return fc;
        return AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/" + fbx);
    }

    [MenuItem("工具/重建远程控制器")]
    [MenuItem("Tools/Rebuild Ranged Controller")]
    public static void Run()
    {
        var sb = new System.Text.StringBuilder();
        string outPath = "Assets/Art/Animators/RangedAnimator.controller";
        try
        {
            // 删除旧的（如果存在）
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(outPath) != null)
                AssetDatabase.DeleteAsset(outPath);

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(outPath);
            var root = ctrl.layers[0].stateMachine;

            // ===== 参数（与历史版本一致，RangedCharacter 驱动 Attack）=====
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
            ctrl.AddParameter("IsAiming", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("AimX", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("AimZ", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("AttackSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("JumpSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("BlockSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("HitSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("DieSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("ShootSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("AimSpeed", AnimatorControllerParameterType.Float);

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

            var idle = NewState("Idle", Clip("female_Idle.fbx"), null);
            var walk = NewState("Walk", Clip("female_Walk.fbx"), "AnimSpeed");
            var run = NewState("Run", Clip("female_Run.fbx"), "AnimSpeed");
            var shoot = NewState("Shoot", Clip("female_shoot.fbx"), "ShootSpeed");
            var hit = NewState("Hit", Clip("female_HitReaction.fbx"), "HitSpeed");
            var die = NewState("Die", Clip("female_death.fbx"), "DieSpeed");
            var js = NewState("JumpStart", Clip("female_jumpstart.fbx"), "JumpSpeed");
            var jl = NewState("JumpLoop", Clip("female_floating.fbx"), "JumpSpeed");
            var jld = NewState("JumpLand", Clip("female_landing.fbx"), "JumpSpeed");
            var aimIdle = NewState("AimIdle", Clip("female_aimIdle.fbx"), "AimSpeed");
            var aimJump = NewState("AimJump", Clip("female_aimJump.fbx"), "JumpSpeed");

            // AimMove 2D 混合树（AimX/AimZ 驱动 4 方向瞄准移动）
            var aimMoveBlend = new BlendTree { name = "AimMoveBlend" };
            aimMoveBlend.blendType = BlendTreeType.SimpleDirectional2D;
            aimMoveBlend.blendParameter = "AimX";
            aimMoveBlend.blendParameterY = "AimZ";
            aimMoveBlend.AddChild(Clip("female_aimWalk.fbx"), new Vector2(0, 1));      // 前
            aimMoveBlend.AddChild(Clip("female_aimWalkBack.fbx"), new Vector2(0, -1)); // 后
            aimMoveBlend.AddChild(Clip("female_aimWalkLeft.fbx"), new Vector2(-1, 0)); // 左
            aimMoveBlend.AddChild(Clip("female_aimWalkRight.fbx"), new Vector2(1, 0)); // 右
            AssetDatabase.AddObjectToAsset(aimMoveBlend, outPath);
            var aimMove = root.AddState("AimMove");
            aimMove.motion = aimMoveBlend;
            aimMove.speedParameterActive = true;
            aimMove.speedParameter = "AimSpeed";

            root.defaultState = idle;

            // ===== 移动过渡（Speed）=====
            idle.AddTransition(walk).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            walk.AddTransition(idle).AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            walk.AddTransition(run).AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
            run.AddTransition(walk).AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed");

            // ===== 瞄准切换（IsAiming）=====
            idle.AddTransition(aimIdle).AddCondition(AnimatorConditionMode.If, 0, "IsAiming");
            walk.AddTransition(aimMove).AddCondition(AnimatorConditionMode.If, 0, "IsAiming");
            run.AddTransition(aimMove).AddCondition(AnimatorConditionMode.If, 0, "IsAiming");
            aimIdle.AddTransition(idle).AddCondition(AnimatorConditionMode.IfNot, 0, "IsAiming");
            aimIdle.AddTransition(aimMove).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            aimMove.AddTransition(aimIdle).AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            aimMove.AddTransition(walk).AddCondition(AnimatorConditionMode.IfNot, 0, "IsAiming");

            // ===== 动作状态退出（hasExitTime）=====
            void ExitTo(AnimatorState from, AnimatorState to, float exitTime)
            {
                var t = from.AddTransition(to);
                t.hasExitTime = true;
                t.exitTime = exitTime;
                t.duration = 0.1f;
            }
            ExitTo(shoot, idle, 0.9f);
            ExitTo(hit, idle, 0.9f);
            ExitTo(die, idle, 0.9f);
            ExitTo(js, jl, 0.5f);
            jl.AddTransition(jld).AddCondition(AnimatorConditionMode.If, 0, "JumpLand");
            ExitTo(jld, idle, 0.9f);
            jld.AddTransition(walk).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            jld.AddTransition(run).AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
            ExitTo(aimJump, aimIdle, 0.9f);

            // ===== AnyState 过渡 =====
            void Any(AnimatorState dst, AnimatorConditionMode m1, float t1, string p1,
                     AnimatorConditionMode m2 = AnimatorConditionMode.If, float t2 = 0, string p2 = null)
            {
                var tr = root.AddAnyStateTransition(dst);
                tr.AddCondition(m1, t1, p1);
                if (p2 != null) tr.AddCondition(m2, t2, p2);
                tr.duration = 0.05f;
            }
            Any(shoot, AnimatorConditionMode.If, 0, "Attack");
            Any(hit, AnimatorConditionMode.If, 0, "Hit");
            Any(die, AnimatorConditionMode.If, 0, "Die");
            Any(js, AnimatorConditionMode.If, 0, "JumpStart", AnimatorConditionMode.IfNot, 0, "IsAiming");
            Any(aimJump, AnimatorConditionMode.If, 0, "JumpStart", AnimatorConditionMode.If, 0, "IsAiming");

            // ===== UpperBody 层（射击上身层）=====
            var sm2 = new AnimatorStateMachine { name = "UpperBody Layer" };
            AssetDatabase.AddObjectToAsset(sm2, outPath);
            var layer2 = new AnimatorControllerLayer
            {
                name = "UpperBody",
                defaultWeight = 1f,
                stateMachine = sm2
            };
            ctrl.AddLayer(layer2);
            var empty = sm2.AddState("Empty");
            sm2.defaultState = empty;
            var uShoot = sm2.AddState("UShoot");
            uShoot.motion = Clip("female_shoot.fbx");
            uShoot.speedParameterActive = true;
            uShoot.speedParameter = "ShootSpeed";
            var anyUShoot = sm2.AddAnyStateTransition(uShoot);
            anyUShoot.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            anyUShoot.duration = 0.05f;
            var tUShoot = uShoot.AddTransition(empty);
            tUShoot.hasExitTime = true;
            tUShoot.exitTime = 0.9f;
            tUShoot.duration = 0.1f;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            sb.AppendLine("重建完成: " + outPath);
            sb.AppendLine($"参数数={ctrl.parameters.Length} 层数={ctrl.layers.Length} Base状态={root.states.Length} Upper状态={sm2.states.Length}");
            foreach (var s in root.states)
                sb.AppendLine($"  {s.state.name} -> {(s.state.motion != null ? s.state.motion.name : "NULL")}");
            sb.AppendLine($"  AimMove -> {aimMoveBlend.name} ({aimMoveBlend.children.Length} 方向)");
        }
        catch (System.Exception e)
        {
            sb.AppendLine("异常: " + e);
        }
        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/ranged_controller_rebuild.txt", sb.ToString());
        Debug.Log("[远程控制器重建] " + sb.ToString());
    }
}

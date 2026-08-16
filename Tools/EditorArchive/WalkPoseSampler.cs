using UnityEditor;
using UnityEngine;

/// <summary>
/// 运行时采样：临时禁用 PlayerController（防止其每帧覆盖 Speed 参数），
/// 强制播放 Walk 状态并逐帧采样膝弯角/骨骼姿态，
/// 客观量化 female_Walk_fixed 走路时的腿部姿态（膝弯角曲线是否平滑、是否深弯）。
/// 结果写入 D:/tmp/walk_knee.txt
/// 菜单：工具/采样走路姿态（英文别名 Tools/SampleWalkPose）
/// </summary>
public static class WalkPoseSampler
{
    [MenuItem("工具/采样走路姿态", false, 1040)]
    [MenuItem("Tools/SampleWalkPose", false, 1040)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[走路姿态] 请先激活 RangedPlayer 并在 Play Mode 下运行");
            return;
        }
        var anim = female.GetComponent<Animator>();
        if (anim == null) return;

        // 临时禁用 PlayerController（防止其每帧把 Speed 写回 0 / 覆盖状态）
        var player = GameObject.Find("Player");
        var pc = player != null ? player.GetComponent<PlayerController>() : null;
        bool pcWasEnabled = pc != null && pc.enabled;
        if (pc != null) pc.enabled = false;

        // 腿部骨骼路径（与 SampleKnee 一致）
        var hips = female.transform.Find("mixamorig1:Hips");
        var lul = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg");
        var ll = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg/mixamorig1:LeftLeg");
        var rl = female.transform.Find("mixamorig1:Hips/mixamorig1:RightUpLeg/mixamorig1:RightLeg");
        if (lul == null || ll == null || hips == null)
        {
            Debug.LogError($"[走路姿态] 骨骼路径不匹配: Hips={hips != null} LUL={lul != null} LL={ll != null}");
            if (pc != null) pc.enabled = pcWasEnabled;
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[走路姿态] 帧{Time.frameCount} 控制器={anim.runtimeAnimatorController?.name} 目标状态=Walk " +
                      $"(PlayerController 已临时禁用:{pcWasEnabled})");
        sb.AppendLine("帧\t膝弯L°\t膝弯R°\tLeftLegX\tRightLegX\tHipsY\tHipsWorld\t状态");
        int frame = 0;
        const int totalFrames = 80; // 1.25s @60fps ≈ 75 帧，采 80 帧覆盖一个完整循环

        // 首帧：写入走路参数并强制进入 Walk
        anim.SetBool("IsAiming", false);
        anim.SetFloat("Speed", 0.4f);
        anim.SetFloat("AnimSpeed", 1f);
        anim.Play("Walk", 0, 0f);

        EditorApplication.update += Step;

        void Step()
        {
            // 保持走路参数（PlayerController 已禁用，不会覆盖）
            anim.SetBool("IsAiming", false);
            anim.SetFloat("Speed", 0.4f);
            var info = anim.GetCurrentAnimatorStateInfo(0);
            string stateName = "?";
            var ctrl = (UnityEditor.Animations.AnimatorController)anim.runtimeAnimatorController;
            if (ctrl != null)
            {
                foreach (var cs in ctrl.layers[0].stateMachine.states)
                {
                    if (cs.state.nameHash == info.shortNameHash) { stateName = cs.state.name; break; }
                }
            }

            // 膝弯角：大腿/小腿骨骼本地向下轴夹角（0°=伸直）
            float kneeL = Vector3.Angle(lul.TransformDirection(Vector3.down), ll.TransformDirection(Vector3.down));
            float kneeR = rl != null ? Vector3.Angle(
                female.transform.Find("mixamorig1:Hips/mixamorig1:RightUpLeg").TransformDirection(Vector3.down),
                rl.TransformDirection(Vector3.down)) : -1f;

            sb.AppendLine($"{Time.frameCount}\t{kneeL:F1}\t{kneeR:F1}\t{ll.localEulerAngles.x:F1}\t{(rl != null ? rl.localEulerAngles.x.ToString("F1") : "?")}\t" +
                          $"{hips.position.y:F3}\t{hips.position.ToString("F2")}\t{stateName}");

            frame++;
            if (frame >= totalFrames)
            {
                EditorApplication.update -= Step;
                // 恢复 PlayerController
                if (pc != null) pc.enabled = pcWasEnabled;
                sb.AppendLine($"\n【完成】已恢复 PlayerController.enabled={pcWasEnabled}");
                Debug.Log(sb.ToString());
                try { System.IO.File.AppendAllText("D:/tmp/walk_knee.txt", sb.ToString() + "\n"); } catch { }
            }
        }
    }
}

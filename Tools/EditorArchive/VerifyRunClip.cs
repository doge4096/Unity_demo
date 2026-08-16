using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 运行时验证 Run（冲刺）状态实际播放的动画 clip（统一瞄准姿态后应为 female_aimRun_fixed）。
/// 非阻塞实现：注册 EditorApplication.update 采样 12 帧后自移除（与 VerifyWalkClip 相同模式）。
/// 结果写入 D:/tmp/walk_source.txt
/// 菜单：工具/验证跑步动画（英文别名 Tools/VerifyRunClip）
/// </summary>
public static class VerifyRunClip
{
    [MenuItem("工具/验证跑步动画", false, 1072)]
    [MenuItem("Tools/VerifyRunClip", false, 1072)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[验证跑步] 请先激活 RangedPlayer 并在 Play Mode 下运行");
            return;
        }
        var anim = female.GetComponent<Animator>();
        if (anim == null) return;

        // 临时禁用 PlayerController（防参数覆盖）
        var player = GameObject.Find("Player");
        var pc = player != null ? player.GetComponent<PlayerController>() : null;
        bool pcWas = pc != null && pc.enabled;
        if (pc != null) pc.enabled = false;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[验证跑步] 帧{Time.frameCount} 控制器={anim.runtimeAnimatorController?.name} " +
                      $"(路径 {AssetDatabase.GetAssetPath(anim.runtimeAnimatorController)})");
        sb.AppendLine($"[验证跑步] PlayerController 已临时禁用={pcWas}");
        sb.AppendLine("帧\t状态\tclip名\t资产路径\t膝弯L°");

        var lul = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg");
        var ll = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg/mixamorig1:LeftLeg");

        anim.SetBool("IsAiming", false);
        anim.SetFloat("Speed", 1f);
        anim.SetFloat("AnimSpeed", 1f);
        anim.Play("Run", 0, 0f);

        int frame = 0;
        const int total = 12;
        EditorApplication.update += Step;

        void Step()
        {
            anim.SetBool("IsAiming", false);
            anim.SetFloat("Speed", 1f);
            var info = anim.GetCurrentAnimatorStateInfo(0);
            string curState = "?";
            var ctrl = anim.runtimeAnimatorController as AnimatorController;
            if (ctrl != null)
            {
                foreach (var cs in ctrl.layers[0].stateMachine.states)
                {
                    if (cs.state.nameHash == info.shortNameHash) { curState = cs.state.name; break; }
                }
            }
            var clips = anim.GetCurrentAnimatorClipInfo(0);
            string clipStr = clips.Length > 0 ? clips[0].clip.name : "(无)";
            string clipPath = clips.Length > 0 ? AssetDatabase.GetAssetPath(clips[0].clip) : "(无)";
            float kneeL = (lul != null && ll != null)
                ? Vector3.Angle(lul.TransformDirection(Vector3.down), ll.TransformDirection(Vector3.down))
                : -1f;

            sb.AppendLine($"{Time.frameCount}\t{curState}\t{clipStr}\t{clipPath}\t{kneeL:F1}");
            frame++;
            if (frame >= total)
            {
                EditorApplication.update -= Step;
                if (pc != null) pc.enabled = pcWas;
                Debug.Log(sb.ToString());
                try { System.IO.File.AppendAllText("D:/tmp/walk_source.txt", sb.ToString() + "\n"); } catch { }
            }
        }
    }
}

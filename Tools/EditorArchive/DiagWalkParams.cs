using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 运行时诊断：不禁用 PlayerController，直接读取当前 Animator 的 AimX/AimZ 实际值 + 状态，
/// 确认走路时混合树的真实输入（怀疑原点 (0,0) 4方向混合导致"斜向走"）。
/// 菜单：工具/诊断走路参数（英文别名 Tools/DiagWalkParams）
/// </summary>
public static class DiagWalkParams
{
    [MenuItem("工具/诊断走路参数", false, 1107)]
    [MenuItem("Tools/DiagWalkParams", false, 1107)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[走路参数] 请先激活 RangedPlayer 并在 Play Mode 下运行");
            return;
        }
        var anim = female.GetComponent<Animator>();
        if (anim == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[走路参数] 帧{Time.frameCount} 控制器={anim.runtimeAnimatorController?.name}");
        sb.AppendLine("帧\t状态\tSpeed\tAimX\tAimZ\tIsAiming\tclip");

        int frame = 0;
        const int total = 12;
        EditorApplication.update += Step;

        void Step()
        {
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
            var clips = anim.GetCurrentAnimatorClipInfo(0);
            string clipStr = clips.Length > 0 ? clips[0].clip.name : "(无)";
            sb.AppendLine($"{Time.frameCount}\t{stateName}\t{anim.GetFloat("Speed"):F2}\t" +
                          $"{anim.GetFloat("AimX"):F2}\t{anim.GetFloat("AimZ"):F2}\t" +
                          $"{anim.GetBool("IsAiming")}\t{clipStr}");
            frame++;
            if (frame >= total)
            {
                EditorApplication.update -= Step;
                Debug.Log(sb.ToString());
                try { System.IO.File.AppendAllText("D:/tmp/walk_params.txt", sb.ToString() + "\n"); } catch { }
            }
        }
    }
}

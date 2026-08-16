using UnityEditor;
using UnityEngine;

/// <summary>
/// 运行时诊断：采样走路/站立时的状态机行为 + 脚踝/脚部骨骼旋转，
/// 定位"脚部不自然/踝关节断裂"与"站立举枪"两个问题。
/// 结果写入 D:/tmp/walk_foot_diag.txt
/// 菜单：工具/诊断走路脚部（英文别名 Tools/DiagWalkFoot）
/// </summary>
public static class WalkFootDiag
{
    [MenuItem("工具/诊断走路脚部", false, 1080)]
    [MenuItem("Tools/DiagWalkFoot", false, 1080)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[脚部诊断] 请先激活 RangedPlayer 并在 Play Mode 下运行");
            return;
        }
        var anim = female.GetComponent<Animator>();
        if (anim == null) return;

        // 临时禁用 PlayerController 防干扰
        var player = GameObject.Find("Player");
        var pc = player != null ? player.GetComponent<PlayerController>() : null;
        bool pcWas = pc != null && pc.enabled;
        if (pc != null) pc.enabled = false;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[脚部诊断] 帧{Time.frameCount} 控制器={anim.runtimeAnimatorController?.name}");

        // 骨骼：脚踝链
        var hips = female.transform.Find("mixamorig1:Hips");
        var lul = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg");
        var ll = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg/mixamorig1:LeftLeg");
        var lf = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg/mixamorig1:LeftLeg/mixamorig1:LeftFoot");
        var ltoe = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg/mixamorig1:LeftLeg/mixamorig1:LeftFoot/mixamorig1:LeftToeBase");
        var rl = female.transform.Find("mixamorig1:Hips/mixamorig1:RightUpLeg/mixamorig1:RightLeg");
        var rf = female.transform.Find("mixamorig1:Hips/mixamorig1:RightUpLeg/mixamorig1:RightLeg/mixamorig1:RightFoot");

        sb.AppendLine($"骨骼: Hips={hips != null} LUL={lul != null} LL={ll != null} LF={lf != null} LToe={ltoe != null} RL={rl != null} RF={rf != null}");

        // 阶段1：走路采样（Speed=0.4）
        anim.SetBool("IsAiming", false);
        anim.SetFloat("Speed", 0.4f);
        anim.Play("Walk", 0, 0f);
        sb.AppendLine($"\n【走路中】Speed=0.4 IsAiming=false");
        sb.AppendLine("帧\t状态\t膝L°\t踝L°(FootX)\t踝L°(FootY)\t踝L°(FootZ)\t脚L Y\tHipsY\tIsAiming");

        int frame = 0;
        const int total = 15;
        EditorApplication.update += StepWalk;

        void StepWalk()
        {
            anim.SetBool("IsAiming", false);
            anim.SetFloat("Speed", 0.4f);
            var info = anim.GetCurrentAnimatorStateInfo(0);
            string stateName = StateName(anim, 0);
            float kneeL = (lul != null && ll != null)
                ? Vector3.Angle(lul.TransformDirection(Vector3.down), ll.TransformDirection(Vector3.down)) : -1;
            string footStr = "?";
            if (lf != null) footStr = $"{lf.localEulerAngles.x:F0}/{lf.localEulerAngles.y:F0}/{lf.localEulerAngles.z:F0}";
            float footY = lf != null ? lf.position.y : -1;
            float hipsY = hips != null ? hips.position.y : -1;
            sb.AppendLine($"{Time.frameCount}\t{stateName}\t{kneeL:F1}\t{footStr}\t{footY:F3}\t{hipsY:F3}\t{anim.GetBool("IsAiming")}");
            frame++;
            if (frame >= total)
            {
                EditorApplication.update -= StepWalk;
                // 阶段2：站立（Speed=0）
                anim.SetFloat("Speed", 0f);
                sb.AppendLine($"\n【站立】Speed=0（走路后立即停下）");
                sb.AppendLine("帧\t状态\tIsAiming\tclip");
                int frame2 = 0;
                const int total2 = 10;
                EditorApplication.update += StepIdle;

                void StepIdle()
                {
                    anim.SetFloat("Speed", 0f);
                    var info2 = anim.GetCurrentAnimatorStateInfo(0);
                    var clips = anim.GetCurrentAnimatorClipInfo(0);
                    string clipStr = clips.Length > 0 ? clips[0].clip.name : "(无)";
                    sb.AppendLine($"{Time.frameCount}\t{StateName(anim, 0)}\t{anim.GetBool("IsAiming")}\t{clipStr}");
                    frame2++;
                    if (frame2 >= total2)
                    {
                        EditorApplication.update -= StepIdle;
                        if (pc != null) pc.enabled = pcWas;
                        Debug.Log(sb.ToString());
                        try { System.IO.File.AppendAllText("D:/tmp/walk_foot_diag.txt", sb.ToString() + "\n"); } catch { }
                    }
                }
            }
        }
    }

    private static string StateName(Animator a, int layer)
    {
        if (a == null || a.runtimeAnimatorController == null) return "?";
        var info = a.GetCurrentAnimatorStateInfo(layer);
        var ctrl = (UnityEditor.Animations.AnimatorController)a.runtimeAnimatorController;
        if (ctrl == null || ctrl.layers.Length <= layer) return "?";
        foreach (var cs in ctrl.layers[layer].stateMachine.states)
        {
            if (cs.state.nameHash == info.shortNameHash) return cs.state.name;
        }
        return $"hash{info.shortNameHash % 10000}";
    }
}

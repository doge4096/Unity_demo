using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 运行时验证修正后的待机（AimIdle）骨骼朝向：强制播放 AimIdle（修正动画），
/// 采样 Hips/Spine/左手（枪口）世界 Y 旋转，确认角度已修正（应接近 0°）。
/// 菜单：工具/验证待机角度（英文别名 Tools/VerifyAimIdleAngle）
/// </summary>
public static class VerifyAimIdleAngle
{
    [MenuItem("工具/验证待机角度", false, 1106)]
    [MenuItem("Tools/VerifyAimIdleAngle", false, 1106)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[待机角度] 请先激活 RangedPlayer 并在 Play Mode 下运行");
            return;
        }
        var anim = female.GetComponent<Animator>();
        if (anim == null) return;

        var player = GameObject.Find("Player");
        var pc = player != null ? player.GetComponent<PlayerController>() : null;
        bool pcWas = pc != null && pc.enabled;
        if (pc != null) pc.enabled = false;

        female.transform.rotation = Quaternion.identity;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[待机角度] 帧{Time.frameCount} 角色Y=0°（世界正前）");
        sb.AppendLine("帧\t状态\tHipsY°\tSpineY°\t左手Y°\tclip");

        var hips = female.transform.Find("mixamorig1:Hips");
        var spine = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine");
        var lHand = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:LeftShoulder/mixamorig1:LeftArm/mixamorig1:LeftForeArm/mixamorig1:LeftHand");

        anim.SetBool("IsAiming", true);
        anim.SetFloat("Speed", 0f);
        anim.SetFloat("AimX", 0f);
        anim.SetFloat("AimZ", 0f);
        anim.Play("AimIdle", 0, 0f);

        int frame = 0;
        const int total = 10;
        EditorApplication.update += Step;

        void Step()
        {
            anim.SetBool("IsAiming", true);
            anim.SetFloat("Speed", 0f);
            var info = anim.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != Animator.StringToHash("AimIdle"))
                anim.Play("AimIdle", 0, info.normalizedTime);

            var clips = anim.GetCurrentAnimatorClipInfo(0);
            string clipStr = clips.Length > 0 ? clips[0].clip.name : "(无)";
            string hY = hips != null ? Norm(hips.eulerAngles.y).ToString("F1") : "?";
            string sY = spine != null ? Norm(spine.eulerAngles.y).ToString("F1") : "?";
            string lY = lHand != null ? Norm(lHand.eulerAngles.y).ToString("F1") : "?";
            sb.AppendLine($"{Time.frameCount}\tAimIdle\t{hY}\t{sY}\t{lY}\t{clipStr}");
            frame++;
            if (frame >= total)
            {
                EditorApplication.update -= Step;
                if (pc != null) pc.enabled = pcWas;
                Debug.Log(sb.ToString());
                try { System.IO.File.AppendAllText("D:/tmp/aimidle_angle.txt", sb.ToString() + "\n"); } catch { }
            }
        }
    }

    private static float Norm(float a)
    {
        while (a > 180f) a -= 360f;
        while (a < -180f) a += 360f;
        return a;
    }
}

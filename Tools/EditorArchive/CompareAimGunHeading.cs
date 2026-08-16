using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 对比各瞄准状态（AimIdle/AimWalk/AimRun/AimShoot）的持枪左手朝向 + 角色骨骼朝向，
/// 确认 aimWalk 走路时"枪口/手臂偏左前 43°"是否与其它瞄准状态一致（动画源姿态）还是异常。
/// 菜单：工具/对比瞄准持枪朝向（英文别名 Tools/CompareAimGunHeading）
/// </summary>
public static class CompareAimGunHeading
{
    [MenuItem("工具/对比瞄准持枪朝向", false, 1102)]
    [MenuItem("Tools/CompareAimGunHeading", false, 1102)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[持枪朝向] 请先激活 RangedPlayer 并在 Play Mode 下运行");
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
        sb.AppendLine($"[持枪朝向] 帧{Time.frameCount} 角色Y=0°（世界正前）");
        sb.AppendLine("状态\tHipsY°\tSpineY°\t左手Y°\t右手Y°\t左手位置");

        var hips = female.transform.Find("mixamorig1:Hips");
        var spine = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine");
        var lHand = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:LeftShoulder/mixamorig1:LeftArm/mixamorig1:LeftForeArm/mixamorig1:LeftHand");
        var rHand = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:RightShoulder/mixamorig1:RightArm/mixamorig1:RightForeArm/mixamorig1:RightHand");

        string[] states = { "AimIdle", "AimWalk", "AimRun", "AimShoot" };
        int idx = 0;
        int frame = 0;
        const int framesPer = 12;

        void SampleState(string stateName)
        {
            anim.SetBool("IsAiming", true);
            anim.SetFloat("Speed", stateName == "AimRun" ? 1f : 0.4f);
            anim.SetFloat("AimX", 0f);
            anim.SetFloat("AimZ", 1f);
            anim.Play(stateName, 0, 0f);
        }

        SampleState(states[0]);
        EditorApplication.update += Step;

        void Step()
        {
            anim.SetBool("IsAiming", true);
            anim.SetFloat("Speed", states[idx] == "AimRun" ? 1f : 0.4f);
            anim.SetFloat("AimX", 0f);
            anim.SetFloat("AimZ", 1f);
            var info = anim.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != Animator.StringToHash(states[idx]))
                anim.Play(states[idx], 0, info.normalizedTime);

            if (frame == 5)
            {
                string hY = hips != null ? Norm(hips.eulerAngles.y).ToString("F1") : "?";
                string sY = spine != null ? Norm(spine.eulerAngles.y).ToString("F1") : "?";
                string lY = lHand != null ? Norm(lHand.eulerAngles.y).ToString("F1") : "?";
                string rY = rHand != null ? Norm(rHand.eulerAngles.y).ToString("F1") : "?";
                string lPos = lHand != null ? lHand.position.ToString("F2") : "?";
                sb.AppendLine($"{states[idx]}\t{hY}\t{sY}\t{lY}\t{rY}\t{lPos}");
            }
            frame++;
            if (frame >= framesPer)
            {
                frame = 0;
                idx++;
                if (idx >= states.Length)
                {
                    EditorApplication.update -= Step;
                    if (pc != null) pc.enabled = pcWas;
                    Debug.Log(sb.ToString());
                    try { System.IO.File.AppendAllText("D:/tmp/aim_gun_heading.txt", sb.ToString() + "\n"); } catch { }
                }
                else
                {
                    SampleState(states[idx]);
                }
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

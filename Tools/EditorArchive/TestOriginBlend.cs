using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 测试 AimWalk 混合树在 (0,0) 输入时的骨骼姿态（4方向各25%混合）：
/// 采样 Hips/Spine/左手世界朝向 + 膝弯角，确认原点混合是否导致"朝向偏左前"的怪异姿态。
/// 菜单：工具/测试原点混合姿态（英文别名 Tools/TestOriginBlend）
/// </summary>
public static class TestOriginBlend
{
    [MenuItem("工具/测试原点混合姿态", false, 1101)]
    [MenuItem("Tools/TestOriginBlend", false, 1101)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[原点混合] 请先激活 RangedPlayer 并在 Play Mode 下运行");
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
        sb.AppendLine($"[原点混合] 帧{Time.frameCount} 角色Y=0°");
        sb.AppendLine("帧\tAimX\tAimZ\tclip\tHipsY°\t左手Y°\t膝L°");

        var hips = female.transform.Find("mixamorig1:Hips");
        var lul = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg");
        var ll = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg/mixamorig1:LeftLeg");
        var lHand = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:LeftShoulder/mixamorig1:LeftArm/mixamorig1:LeftForeArm/mixamorig1:LeftHand");

        anim.SetBool("IsAiming", false);
        anim.SetFloat("Speed", 0.4f);
        anim.SetFloat("AimX", 0f);
        anim.SetFloat("AimZ", 0f);
        anim.Play("Walk", 0, 0f);

        int frame = 0;
        const int total = 10;
        EditorApplication.update += Step;

        void Step()
        {
            anim.SetFloat("Speed", 0.4f);
            anim.SetFloat("AimX", 0f);
            anim.SetFloat("AimZ", 0f);
            var info = anim.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != Animator.StringToHash("Walk"))
                anim.Play("Walk", 0, info.normalizedTime);

            var clips = anim.GetCurrentAnimatorClipInfo(0);
            string clipStr = clips.Length > 0
                ? string.Join("|", System.Array.ConvertAll(clips, c => $"{c.clip.name.Replace("female_aimWalk", "").Replace("_fixed", "")}×{c.weight:F0}"))
                : "(无)";
            float hY = hips != null ? Norm(hips.eulerAngles.y) : -999;
            float lY = lHand != null ? Norm(lHand.eulerAngles.y) : -999;
            float kneeL = (lul != null && ll != null)
                ? Vector3.Angle(lul.TransformDirection(Vector3.down), ll.TransformDirection(Vector3.down)) : -1;
            sb.AppendLine($"{Time.frameCount}\t0\t0\t{clipStr}\t{hY:F1}\t{lY:F1}\t{kneeL:F1}");
            frame++;
            if (frame >= total)
            {
                EditorApplication.update -= Step;
                if (pc != null) pc.enabled = pcWas;
                Debug.Log(sb.ToString());
                try { System.IO.File.AppendAllText("D:/tmp/origin_blend.txt", sb.ToString() + "\n"); } catch { }
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

using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 运行时采样走路/跑步时骨骼世界朝向（非阻塞，VerifyWalkClip 模式）：
/// 对比 aimWalk 与 aimRun 的 Hips/Spine/Chest/Head 世界 Y 旋转，
/// 检查走路动画是否让角色骨骼朝向偏左（RootQ 恒定偏转是否体现在骨骼上）。
/// 菜单：工具/采样骨骼朝向（英文别名 Tools/SampleBoneHeading）
/// </summary>
public static class SampleBoneHeading
{
    [MenuItem("工具/采样骨骼朝向", false, 1099)]
    [MenuItem("Tools/SampleBoneHeading", false, 1099)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[骨骼朝向] 请先激活 RangedPlayer 并在 Play Mode 下运行");
            return;
        }
        var anim = female.GetComponent<Animator>();
        if (anim == null) return;

        var player = GameObject.Find("Player");
        var pc = player != null ? player.GetComponent<PlayerController>() : null;
        bool pcWas = pc != null && pc.enabled;
        if (pc != null) pc.enabled = false;

        // 角色统一朝 0°（世界正前）
        female.transform.rotation = Quaternion.identity;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[骨骼朝向] 帧{Time.frameCount} 角色Y=0°");
        sb.AppendLine("帧\t状态\tHipsY°\tSpineY°\tChestY°\tHeadY°\t左手Y°");

        var hips = female.transform.Find("mixamorig1:Hips");
        var spine = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine");
        var spine2 = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2");
        var head = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:Neck/mixamorig1:Head");
        var lHand = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:LeftShoulder/mixamorig1:LeftArm/mixamorig1:LeftForeArm/mixamorig1:LeftHand");

        // 先采样走路（Walk 状态，AimX/AimZ=0/1 纯前向）
        anim.SetBool("IsAiming", false);
        anim.SetFloat("Speed", 0.4f);
        anim.SetFloat("AimX", 0f);
        anim.SetFloat("AimZ", 1f);
        anim.Play("Walk", 0, 0f);

        int frame = 0;
        const int total = 10;
        EditorApplication.update += Step;

        void Step()
        {
            anim.SetFloat("Speed", 0.4f);
            anim.SetFloat("AimX", 0f);
            anim.SetFloat("AimZ", 1f);
            var info = anim.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != Animator.StringToHash("Walk"))
                anim.Play("Walk", 0, info.normalizedTime);

            sb.AppendLine($"{Time.frameCount}\tWalk\t{hY(hips)}\t{hY(spine)}\t{hY(spine2)}\t{hY(head)}\t{hY(lHand)}");
            frame++;
            if (frame >= total)
            {
                EditorApplication.update -= Step;
                // 接着采样跑步
                anim.SetFloat("Speed", 1f);
                anim.Play("Run", 0, 0f);
                int frame2 = 0;
                const int total2 = 10;
                EditorApplication.update += Step2;

                void Step2()
                {
                    anim.SetFloat("Speed", 1f);
                    anim.SetFloat("AimX", 0f);
                    anim.SetFloat("AimZ", 1f);
                    var info2 = anim.GetCurrentAnimatorStateInfo(0);
                    if (info2.shortNameHash != Animator.StringToHash("Run"))
                        anim.Play("Run", 0, info2.normalizedTime);
                    sb.AppendLine($"{Time.frameCount}\tRun\t{hY(hips)}\t{hY(spine)}\t{hY(spine2)}\t{hY(head)}\t{hY(lHand)}");
                    frame2++;
                    if (frame2 >= total2)
                    {
                        EditorApplication.update -= Step2;
                        if (pc != null) pc.enabled = pcWas;
                        Debug.Log(sb.ToString());
                        try { System.IO.File.AppendAllText("D:/tmp/bone_heading.txt", sb.ToString() + "\n"); } catch { }
                    }
                }
            }
        }
    }

    private static float hY(Transform t)
    {
        if (t == null) return -999f;
        float a = t.eulerAngles.y;
        while (a > 180f) a -= 360f;
        while (a < -180f) a += 360f;
        return a;
    }
}

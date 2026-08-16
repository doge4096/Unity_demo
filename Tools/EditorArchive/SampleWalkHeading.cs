using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 运行时采样：播放 Walk（AimWalk 混合树）时采样 Hips 骨骼的 Y 旋转（本地+世界）、
/// 角色 transform 朝向、以及混合树实际输出的 clip，量化"走路朝向偏左前"问题。
/// 菜单：工具/采样走路朝向（英文别名 Tools/SampleWalkHeading）
/// </summary>
public static class SampleWalkHeading
{
    [MenuItem("工具/采样走路朝向", false, 1095)]
    [MenuItem("Tools/SampleWalkHeading", false, 1095)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[朝向] 请先激活 RangedPlayer 并在 Play Mode 下运行");
            return;
        }
        var anim = female.GetComponent<Animator>();
        if (anim == null) return;

        var player = GameObject.Find("Player");
        var pc = player != null ? player.GetComponent<PlayerController>() : null;
        bool pcWas = pc != null && pc.enabled;
        if (pc != null) pc.enabled = false;

        var hips = female.transform.Find("mixamorig1:Hips");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[朝向] 帧{Time.frameCount} applyRootMotion={anim.applyRootMotion}");
        sb.AppendLine("帧\t状态\tclip\t角色Y°\tHips本地Y°\tHips世界Y°\tAimX\tAimZ");

        // 走路：设 AimX/AimZ 为"前"(0,1)，纯前向移动
        anim.SetBool("IsAiming", false);
        anim.SetFloat("Speed", 0.4f);
        anim.SetFloat("AimX", 0f);
        anim.SetFloat("AimZ", 1f);
        anim.Play("Walk", 0, 0f);

        int frame = 0;
        const int total = 15;
        EditorApplication.update += Step;

        void Step()
        {
            anim.SetFloat("Speed", 0.4f);
            anim.SetFloat("AimX", 0f);
            anim.SetFloat("AimZ", 1f);
            var info = anim.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != Animator.StringToHash("Walk"))
                anim.Play("Walk", 0, info.normalizedTime);

            var clips = anim.GetCurrentAnimatorClipInfo(0);
            string clipStr = clips.Length > 0 ? clips[0].clip.name : "(无)";
            float roleY = female.transform.eulerAngles.y;
            float hipsLocalY = hips != null ? hips.localEulerAngles.y : -1;
            float hipsWorldY = hips != null ? hips.eulerAngles.y : -1;

            sb.AppendLine($"{Time.frameCount}\tWalk\t{clipStr}\t{roleY:F1}\t{hipsLocalY:F1}\t{hipsWorldY:F1}\t" +
                          $"{anim.GetFloat("AimX"):F2}\t{anim.GetFloat("AimZ"):F2}");
            frame++;
            if (frame >= total)
            {
                EditorApplication.update -= Step;
                if (pc != null) pc.enabled = pcWas;
                Debug.Log(sb.ToString());
                try { System.IO.File.AppendAllText("D:/tmp/walk_heading.txt", sb.ToString() + "\n"); } catch { }
            }
        }
    }
}

using UnityEditor;
using UnityEngine;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 运行时对比：原版 female_aimWalk_fixed vs 手臂修正版 female_aimWalk3_fixed
/// （手臂曲线替换为 aimShoot 持枪姿态）的持枪左手世界朝向 + Hips 朝向，
/// 验证走路"斜向"是否因持枪手臂姿态造成。
/// 菜单：工具/对比走路持枪修正（英文别名 Tools/CompareWalkGunFix）
/// </summary>
public static class CompareWalkGunFix
{
    [MenuItem("工具/对比走路持枪修正", false, 1116)]
    [MenuItem("Tools/CompareWalkGunFix", false, 1116)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[持枪对比] 请先激活 RangedPlayer 并在 Play Mode 下运行");
            return;
        }
        var anim = female.GetComponent<Animator>();
        if (anim == null) return;

        var player = GameObject.Find("Player");
        var pc = player != null ? player.GetComponent<PlayerController>() : null;
        bool pcWas = pc != null && pc.enabled;
        if (pc != null) pc.enabled = false;

        female.transform.rotation = Quaternion.identity;

        var lHand = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:LeftShoulder/mixamorig1:LeftArm/mixamorig1:LeftForeArm/mixamorig1:LeftHand");
        var hips = female.transform.Find("mixamorig1:Hips");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[持枪对比] 帧{Time.frameCount} 角色Y=0°");
        sb.AppendLine("版本\tHipsY°\t左手Y°\tclip");

        // 先播原版
        anim.SetBool("IsAiming", false);
        anim.SetFloat("Speed", 0.4f);
        anim.SetFloat("AimX", 0f);
        anim.SetFloat("AimZ", 1f);
        anim.Play("Walk", 0, 0f);
        int frame = 0;
        const int total = 8;
        EditorApplication.update += Step;

        void Step()
        {
            anim.SetFloat("AimX", 0f);
            anim.SetFloat("AimZ", 1f);
            if (frame == 4)
            {
                var clips = anim.GetCurrentAnimatorClipInfo(0);
                string clipStr = clips.Length > 0 ? clips[0].clip.name : "(无)";
                sb.AppendLine($"原版\t{hips.eulerAngles.y:F1}\t{lHand.eulerAngles.y:F1}\t{clipStr}");
            }
            frame++;
            if (frame >= total)
            {
                EditorApplication.update -= Step;
                // 切换修正版
                var overrideCtrl = new AnimatorOverrideController(anim.runtimeAnimatorController);
                var fixedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Fixed/female_aimWalk3_fixed.anim");
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                overrideCtrl.GetOverrides(pairs);
                for (int i = 0; i < pairs.Count; i++)
                {
                    if (pairs[i].Key != null && pairs[i].Key.name == "female_aimWalk_fixed")
                        pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, fixedClip);
                }
                overrideCtrl.ApplyOverrides(pairs);
                anim.runtimeAnimatorController = overrideCtrl;

                anim.Play("Walk", 0, 0f);
                int frame2 = 0;
                const int total2 = 8;
                EditorApplication.update += Step2;

                void Step2()
                {
                    anim.SetFloat("AimX", 0f);
                    anim.SetFloat("AimZ", 1f);
                    if (frame2 == 4)
                    {
                        var clips2 = anim.GetCurrentAnimatorClipInfo(0);
                        string clipStr2 = clips2.Length > 0 ? clips2[0].clip.name : "(无)";
                        sb.AppendLine($"修正版\t{hips.eulerAngles.y:F1}\t{lHand.eulerAngles.y:F1}\t{clipStr2}");
                    }
                    frame2++;
                    if (frame2 >= total2)
                    {
                        EditorApplication.update -= Step2;
                        if (pc != null) pc.enabled = pcWas;
                        Debug.Log(sb.ToString());
                        try { System.IO.File.AppendAllText("D:/tmp/walk_gun_compare.txt", sb.ToString() + "\n"); } catch { }
                    }
                }
            }
        }
    }
}

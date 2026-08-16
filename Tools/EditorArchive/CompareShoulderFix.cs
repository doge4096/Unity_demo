using UnityEditor;
using UnityEngine;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 运行时对比：原版 female_aimWalk_fixed vs 肩膀校正版 female_aimWalk4_fixed
/// 的持枪左手世界朝向 + Hips 朝向，验证肩膀曲线校正是否解决"枪口偏左"。
/// 菜单：工具/对比肩膀校正（英文别名 Tools/CompareShoulderFix）
/// </summary>
public static class CompareShoulderFix
{
    [MenuItem("工具/对比肩膀校正", false, 1119)]
    [MenuItem("Tools/CompareShoulderFix", false, 1119)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[肩膀对比] 请先激活 RangedPlayer 并在 Play Mode 下运行");
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
        var rHand = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:RightShoulder/mixamorig1:RightArm/mixamorig1:RightForeArm/mixamorig1:RightHand");
        var hips = female.transform.Find("mixamorig1:Hips");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[肩膀对比] 帧{Time.frameCount} 角色Y=0°");
        sb.AppendLine("版本\tHipsY°\t左手Y°\t右手Y°\tclip");

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
                sb.AppendLine($"原版\t{hips.eulerAngles.y:F1}\t{lHand.eulerAngles.y:F1}\t{rHand.eulerAngles.y:F1}\t{clipStr}");
            }
            frame++;
            if (frame >= total)
            {
                EditorApplication.update -= Step;
                var overrideCtrl = new AnimatorOverrideController(anim.runtimeAnimatorController);
                var fixedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Fixed/female_aimWalk4_fixed.anim");
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
                        sb.AppendLine($"肩膀校正\t{hips.eulerAngles.y:F1}\t{lHand.eulerAngles.y:F1}\t{rHand.eulerAngles.y:F1}\t{clipStr2}");
                    }
                    frame2++;
                    if (frame2 >= total2)
                    {
                        EditorApplication.update -= Step2;
                        if (pc != null) pc.enabled = pcWas;
                        Debug.Log(sb.ToString());
                        try { System.IO.File.AppendAllText("D:/tmp/shoulder_compare.txt", sb.ToString() + "\n"); } catch { }
                    }
                }
            }
        }
    }
}

using UnityEditor;
using UnityEngine;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 运行时对比：播放原版 female_aimWalk_fixed vs 修正版 female_aimWalk2_fixed 的持枪左手世界朝向，
/// 验证 RootQ 置 identity 是否解决"枪口偏左43°"问题。
/// 用 AnimatorOverrideController 临时覆盖混合树前向节点。
/// 菜单：工具/对比走路修正效果（英文别名 Tools/CompareWalkFix）
/// </summary>
public static class CompareWalkFix
{
    [MenuItem("工具/对比走路修正效果", false, 1113)]
    [MenuItem("Tools/CompareWalkFix", false, 1113)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[走路修正] 请先激活 RangedPlayer 并在 Play Mode 下运行");
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
        sb.AppendLine($"[走路修正] 帧{Time.frameCount} 角色Y=0°");
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
                // 切换修正版：用 override 覆盖
                var overrideCtrl = new AnimatorOverrideController(anim.runtimeAnimatorController);
                var fixedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Fixed/female_aimWalk2_fixed.anim");
                var pairs = new System.Collections.Generic.List<KeyValuePair<AnimationClip, AnimationClip>>();
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
                        try { System.IO.File.AppendAllText("D:/tmp/walk_fix_compare.txt", sb.ToString() + "\n"); } catch { }
                    }
                }
            }
        }
    }
}

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 运行时采样：对比 Female 角色在 female_Walk_fixed vs man_Walking_fixed 下的完整骨骼姿态
/// （上身前倾角/肩膀高度/摆臂/膝弯角），判断当前走路观感更像哪个动画。
/// 结果写入 D:/tmp/walk_pose_compare.txt
/// 菜单：工具/对比走路姿态来源（英文别名 Tools/CompareWalkPose）
/// </summary>
public static class WalkPoseCompare
{
    [MenuItem("工具/对比走路姿态来源", false, 1060)]
    [MenuItem("Tools/CompareWalkPose", false, 1060)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[姿态对比] 请先激活 RangedPlayer 并在 Play Mode 下运行");
            return;
        }
        var anim = female.GetComponent<Animator>();
        if (anim == null) return;

        // 临时禁用 PlayerController 防参数干扰
        var player = GameObject.Find("Player");
        var pc = player != null ? player.GetComponent<PlayerController>() : null;
        bool pcWas = pc != null && pc.enabled;
        if (pc != null) pc.enabled = false;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[姿态对比] 帧{Time.frameCount} 控制器={anim.runtimeAnimatorController?.name}");
        SampleClip(female, anim, "Assets/Art/Animations/Fixed/female_Walk_fixed.anim", "female_Walk_fixed", sb);
        SampleClip(female, anim, "Assets/Art/Animations/Fixed/man_Walking_fixed.anim", "man_Walking_fixed", sb);

        if (pc != null) pc.enabled = pcWas;
        Debug.Log(sb.ToString());
        try { System.IO.File.AppendAllText("D:/tmp/walk_pose_compare.txt", sb.ToString() + "\n"); } catch { }
    }

    private static bool HasOverride(List<KeyValuePair<AnimationClip, AnimationClip>> ovr, AnimationClip clip)
    {
        foreach (var kv in ovr)
            if (kv.Value != null && kv.Value.name == clip.name) return true;
        return false;
    }

    private static void SampleClip(GameObject female, Animator anim, string clipPath, string label, System.Text.StringBuilder sb)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) { sb.AppendLine($"\n== {label}: 加载失败 {clipPath}"); return; }

        // 骨骼
        var hips = female.transform.Find("mixamorig1:Hips");
        var spine = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine");
        var lul = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg");
        var ll = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg/mixamorig1:LeftLeg");
        var rl = female.transform.Find("mixamorig1:Hips/mixamorig1:RightUpLeg/mixamorig1:RightLeg");
        var luArm = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:LeftShoulder/mixamorig1:LeftArm");
        var ruArm = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:RightShoulder/mixamorig1:RightArm");
        if (hips == null || spine == null || lul == null || ll == null)
        {
            sb.AppendLine($"\n== {label}: 骨骼路径不匹配 Hips={hips!=null} Spine={spine!=null} LUL={lul!=null} LL={ll!=null}");
            return;
        }

        // 切到指定 clip（用 AnimatorOverrideController 覆盖 Walk 动画）
        var overrideCtrl = new AnimatorOverrideController(anim.runtimeAnimatorController);
        var ovr = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrideCtrl.GetOverrides(ovr);
        for (int i = 0; i < ovr.Count; i++)
        {
            if (ovr[i].Key != null && (ovr[i].Key.name == "female_Walk_fixed" || ovr[i].Key.name == "man_Walking_fixed"))
                ovr[i] = new KeyValuePair<AnimationClip, AnimationClip>(ovr[i].Key, clip);
        }
        if (!HasOverride(ovr, clip))
        {
            sb.AppendLine($"\n== {label}: 控制器里没有 Walk clip 可覆盖（name 不匹配）");
            return;
        }
        overrideCtrl.ApplyOverrides(ovr);
        anim.runtimeAnimatorController = overrideCtrl;
        anim.SetBool("IsAiming", false);
        anim.SetFloat("Speed", 0.4f);
        anim.Play("Walk", 0, 0f);

        sb.AppendLine($"\n== {label}（clip 覆盖播放）==");
        sb.AppendLine("帧\t上身前倾°\t肩L高\t肩R高\t膝L°\t膝R°\tHipsY");

        // 采样 20 帧（一帧一采，异步 update 循环会跑满帧率，用帧数计数）
        int frame = 0;
        const int total = 20;
        float lastT = -1f;
        EditorApplication.update += Step;
        void Step()
        {
            anim.SetFloat("Speed", 0.4f);
            var info = anim.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != Animator.StringToHash("Walk"))
                anim.Play("Walk", 0, info.normalizedTime);

            // 上身前倾：Hips→Spine 向量与世界向下的夹角（0=完全直立）
            Vector3 spineDir = (spine.position - hips.position).normalized;
            float lean = Vector3.Angle(spineDir, Vector3.up);
            // 肩高度（世界 Y，相对 Hips）
            float lArmY = luArm != null ? luArm.position.y : 0f;
            float rArmY = ruArm != null ? ruArm.position.y : 0f;
            // 膝弯角
            float kneeL = Vector3.Angle(lul.TransformDirection(Vector3.down), ll.TransformDirection(Vector3.down));
            float kneeR = rl != null ? Vector3.Angle(
                female.transform.Find("mixamorig1:Hips/mixamorig1:RightUpLeg").TransformDirection(Vector3.down),
                rl.TransformDirection(Vector3.down)) : -1f;

            sb.AppendLine($"{Time.frameCount}\t{lean:F1}\t{lArmY:F3}\t{rArmY:F3}\t{kneeL:F1}\t{kneeR:F1}\t{hips.position.y:F3}");
            frame++;
            if (frame >= total)
            {
                EditorApplication.update -= Step;
            }
        }
    }
}

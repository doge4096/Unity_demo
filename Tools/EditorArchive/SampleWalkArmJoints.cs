using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 采样走路时手臂链各关节的局部欧拉角（Shoulder→Arm→ForeArm→Hand）+ 世界朝向，
/// 定位"持枪左手偏左 43°"来自哪个骨骼。
/// 菜单：工具/采样走路手臂关节（英文别名 Tools/SampleWalkArmJoints）
/// </summary>
public static class SampleWalkArmJoints
{
    [MenuItem("工具/采样走路手臂关节", false, 1120)]
    [MenuItem("Tools/SampleWalkArmJoints", false, 1120)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[手臂关节] 请先激活 RangedPlayer 并在 Play Mode 下运行");
            return;
        }
        var anim = female.GetComponent<Animator>();
        if (anim == null) return;

        var player = GameObject.Find("Player");
        var pc = player != null ? player.GetComponent<PlayerController>() : null;
        bool pcWas = pc != null && pc.enabled;
        if (pc != null) pc.enabled = false;

        female.transform.rotation = Quaternion.identity;

        var joints = new (string name, Transform t)[]
        {
            ("Hips", female.transform.Find("mixamorig1:Hips")),
            ("Spine", female.transform.Find("mixamorig1:Hips/mixamorig1:Spine")),
            ("Spine2", female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2")),
            ("LShoulder", female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:LeftShoulder")),
            ("LArm", female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:LeftShoulder/mixamorig1:LeftArm")),
            ("LForeArm", female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:LeftShoulder/mixamorig1:LeftArm/mixamorig1:LeftForeArm")),
            ("LHand", female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:LeftShoulder/mixamorig1:LeftArm/mixamorig1:LeftForeArm/mixamorig1:LeftHand")),
        };

        anim.SetBool("IsAiming", false);
        anim.SetFloat("Speed", 0.4f);
        anim.SetFloat("AimX", 0f);
        anim.SetFloat("AimZ", 1f);
        anim.Play("Walk", 0, 0f);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[手臂关节] 帧{Time.frameCount} 角色Y=0°");
        sb.AppendLine("关节\t本地欧拉(°)\t世界欧拉(°)");

        int frame = 0;
        const int total = 10;
        EditorApplication.update += Step;

        void Step()
        {
            anim.SetFloat("AimX", 0f);
            anim.SetFloat("AimZ", 1f);
            var info = anim.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != Animator.StringToHash("Walk"))
                anim.Play("Walk", 0, info.normalizedTime);

            if (frame == 5)
            {
                foreach (var (name, t) in joints)
                {
                    if (t == null) { sb.AppendLine($"{name}\t缺失"); continue; }
                    Vector3 le = t.localEulerAngles;
                    Vector3 we = t.eulerAngles;
                    sb.AppendLine($"{name}\t{le.x:F1}/{le.y:F1}/{le.z:F1}\t{we.x:F1}/{we.y:F1}/{we.z:F1}");
                }
            }
            frame++;
            if (frame >= total)
            {
                EditorApplication.update -= Step;
                if (pc != null) pc.enabled = pcWas;
                Debug.Log(sb.ToString());
                try { System.IO.File.AppendAllText("D:/tmp/walk_arm_joints.txt", sb.ToString() + "\n"); } catch { }
            }
        }
    }
}

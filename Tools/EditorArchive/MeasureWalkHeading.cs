using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 测量 female_aimWalk_fixed 播放时脚步位移方向 vs 角色 forward：
/// 采样左脚位置变化，计算实际步进方向与世界夹角，判断走路动画本身是否"斜着走"。
/// 菜单：工具/测量走路步进方向（英文别名 Tools/MeasureWalkHeading）
/// </summary>
public static class MeasureWalkHeading
{
    [MenuItem("工具/测量走路步进方向", false, 1117)]
    [MenuItem("Tools/MeasureWalkHeading", false, 1117)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[步进方向] 请先激活 RangedPlayer 并在 Play Mode 下运行");
            return;
        }
        var anim = female.GetComponent<Animator>();
        if (anim == null) return;

        var player = GameObject.Find("Player");
        var pc = player != null ? player.GetComponent<PlayerController>() : null;
        bool pcWas = pc != null && pc.enabled;
        if (pc != null) pc.enabled = false;

        female.transform.rotation = Quaternion.identity;

        var lFoot = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg/mixamorig1:LeftLeg/mixamorig1:LeftFoot");
        var rFoot = female.transform.Find("mixamorig1:Hips/mixamorig1:RightUpLeg/mixamorig1:RightLeg/mixamorig1:RightFoot");
        var hips = female.transform.Find("mixamorig1:Hips");

        anim.SetBool("IsAiming", false);
        anim.SetFloat("Speed", 0.4f);
        anim.SetFloat("AimX", 0f);
        anim.SetFloat("AimZ", 1f);
        anim.Play("Walk", 0, 0f);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[步进方向] 帧{Time.frameCount} 角色Y=0°（forward=+Z）");
        sb.AppendLine("帧\t左脚X\t左脚Z\t右脚X\t右脚Z\tHipsX\tHipsZ");

        int frame = 0;
        const int total = 30;
        EditorApplication.update += Step;

        void Step()
        {
            anim.SetFloat("AimX", 0f);
            anim.SetFloat("AimZ", 1f);
            var info = anim.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != Animator.StringToHash("Walk"))
                anim.Play("Walk", 0, info.normalizedTime);

            string lx = lFoot != null ? lFoot.position.x.ToString("F3") : "?";
            string lz = lFoot != null ? lFoot.position.z.ToString("F3") : "?";
            string rx = rFoot != null ? rFoot.position.x.ToString("F3") : "?";
            string rz = rFoot != null ? rFoot.position.z.ToString("F3") : "?";
            string hx = hips != null ? hips.position.x.ToString("F3") : "?";
            string hz = hips != null ? hips.position.z.ToString("F3") : "?";
            sb.AppendLine($"{Time.frameCount}\t{lx}\t{lz}\t{rx}\t{rz}\t{hx}\t{hz}");
            frame++;
            if (frame >= total)
            {
                EditorApplication.update -= Step;
                if (pc != null) pc.enabled = pcWas;
                Debug.Log(sb.ToString());
                try { System.IO.File.AppendAllText("D:/tmp/walk_foot_path.txt", sb.ToString() + "\n"); } catch { }
            }
        }
    }
}

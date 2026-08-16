using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 对比走路 vs 跑步时角色骨骼世界朝向（Hips/Spine/Chest/头 + 枪口方向）：
/// 检查 aimWalk 动画是否让上身/头部朝向偏左（RootQ 恒定偏转 39° 是否体现在骨骼上）。
/// 菜单：工具/对比走路跑步朝向（英文别名 Tools/CompareWalkRunHeading）
/// </summary>
public static class CompareWalkRunHeading
{
    [MenuItem("工具/对比走路跑步朝向", false, 1098)]
    [MenuItem("Tools/CompareWalkRunHeading", false, 1098)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[朝向对比] 请先激活 RangedPlayer 并在 Play Mode 下运行");
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
        sb.AppendLine($"[朝向对比] 帧{Time.frameCount} 角色Y=0°（世界正前）");
        sb.AppendLine("状态\tHipsY°\tSpineY°\tChestY°\tHeadY°\t枪口方向");

        var hips = female.transform.Find("mixamorig1:Hips");
        var spine = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine");
        var spine1 = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1");
        var spine2 = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2");
        var head = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:Neck/mixamorig1:Head");
        var lHand = female.transform.Find("mixamorig1:Hips/mixamorig1:Spine/mixamorig1:Spine1/mixamorig1:Spine2/mixamorig1:LeftShoulder/mixamorig1:LeftArm/mixamorig1:LeftForeArm/mixamorig1:LeftHand");

        string[] states = { "Walk", "Run" };
        foreach (var stateName in states)
        {
            anim.SetBool("IsAiming", false);
            anim.SetFloat("Speed", stateName == "Walk" ? 0.4f : 1f);
            anim.SetFloat("AimX", 0f);
            anim.SetFloat("AimZ", 1f);
            anim.Play(stateName, 0, 0f);

            int frame = 0;
            const int total = 10;
            EditorApplication.update += Step;

            void Step()
            {
                anim.SetFloat("Speed", stateName == "Walk" ? 0.4f : 1f);
                anim.SetFloat("AimX", 0f);
                anim.SetFloat("AimZ", 1f);
                var info = anim.GetCurrentAnimatorStateInfo(0);
                if (info.shortNameHash != Animator.StringToHash(stateName))
                    anim.Play(stateName, 0, info.normalizedTime);

                if (frame == 5) // 稳定后采样
                {
                    string hY = hips != null ? NormalizeAngle(hips.eulerAngles.y).ToString("F1") : "?";
                    string sY = spine != null ? NormalizeAngle(spine.eulerAngles.y).ToString("F1") : "?";
                    string cY = spine2 != null ? NormalizeAngle(spine2.eulerAngles.y).ToString("F1") : "?";
                    string headY = head != null ? NormalizeAngle(head.eulerAngles.y).ToString("F1") : "?";
                    string gun = lHand != null ? NormalizeAngle(lHand.eulerAngles.y).ToString("F1") : "?";
                    sb.AppendLine($"{stateName}\t{hY}\t{sY}\t{cY}\t{headY}\t左手指向Y={gun}°");
                }
                frame++;
                if (frame >= total)
                {
                    EditorApplication.update -= Step;
                }
            }
            // 等待该状态采样完
            int guard = 0;
            while (guard < 300 && !sb.ToString().Contains($"{stateName}\t"))
            {
                System.Threading.Thread.Sleep(10);
                guard++;
            }
        }

        if (pc != null) pc.enabled = pcWas;
        Debug.Log(sb.ToString());
        try { System.IO.File.AppendAllText("D:/tmp/heading_compare.txt", sb.ToString() + "\n"); } catch { }
    }

    private static float NormalizeAngle(float a)
    {
        while (a > 180f) a -= 360f;
        while (a < -180f) a += 360f;
        return a;
    }
}

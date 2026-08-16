using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 模拟真实走路逻辑：角色面向 45°（模拟"向左前移动"），
/// 按 PlayerController 同款公式计算 localMoveDir → AimX/AimZ，
/// 采样混合树实际输出 + Hips 朝向 + 步伐位移方向，判断走路动画是否"朝左前"。
/// 菜单：工具/模拟走路方向（英文别名 Tools/SimWalkDir）
/// </summary>
public static class SimWalkDir
{
    [MenuItem("工具/模拟走路方向", false, 1097)]
    [MenuItem("Tools/SimWalkDir", false, 1097)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[模拟走路] 请先激活 RangedPlayer 并在 Play Mode 下运行");
            return;
        }
        var anim = female.GetComponent<Animator>();
        if (anim == null) return;

        var player = GameObject.Find("Player");
        var pc = player != null ? player.GetComponent<PlayerController>() : null;
        bool pcWas = pc != null && pc.enabled;
        if (pc != null) pc.enabled = false;

        // 场景 A：角色面向 0°（正前），移动方向 0° → 应播"前"动画
        // 场景 B：角色面向 45°（左前），移动方向 45° → 动画应跟着角色朝左前（正确）
        // 场景 C：角色面向 0° 但移动方向 45°（角色没转，直接斜向移动）→ localMoveDir 应算出左前方向
        var scenarios = new (string label, float roleYaw, float moveAngle)[]
        {
            ("A:角色朝0°移动0°", 0f, 0f),
            ("B:角色朝45°移动45°", 45f, 45f),
            ("C:角色朝0°但移动45°", 0f, 45f),
        };

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[模拟走路] 帧{Time.frameCount}");
        sb.AppendLine("场景\t角色Y°\t移动角°\tlocalDir\tAimX\tAimZ\t输出clip");

        anim.SetBool("IsAiming", false);
        anim.SetFloat("Speed", 0.4f);

        int idx = 0;
        int frame = 0;
        const int framesPer = 8;
        EditorApplication.update += Step;

        void Step()
        {
            if (idx >= scenarios.Length)
            {
                EditorApplication.update -= Step;
                if (pc != null) pc.enabled = pcWas;
                Debug.Log(sb.ToString());
                try { System.IO.File.AppendAllText("D:/tmp/sim_walk_dir.txt", sb.ToString() + "\n"); } catch { }
                return;
            }

            var (label, roleYaw, moveAngle) = scenarios[idx];
            // 设角色朝向
            female.transform.rotation = Quaternion.Euler(0f, roleYaw, 0f);
            // 模拟移动方向（世界空间）
            Vector3 moveDir = Quaternion.Euler(0f, moveAngle, 0f) * Vector3.forward;
            // PlayerController 同款公式
            Vector3 localMoveDir = female.transform.InverseTransformDirection(moveDir.normalized);
            anim.SetFloat("AimX", localMoveDir.x);
            anim.SetFloat("AimZ", localMoveDir.z);
            var info = anim.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != Animator.StringToHash("Walk"))
                anim.Play("Walk", 0, info.normalizedTime);

            if (frame == 3)
            {
                var clips = anim.GetCurrentAnimatorClipInfo(0);
                string clipStr = clips.Length > 0
                    ? string.Join("|", System.Array.ConvertAll(clips, c => $"{c.clip.name.Replace("female_aimWalk", "").Replace("_fixed", "")}×{c.weight:F0}"))
                    : "(无)";
                sb.AppendLine($"{label}\t{roleYaw:F0}\t{moveAngle:F0}\t({localMoveDir.x:F2},{localMoveDir.z:F2})\t" +
                              $"{localMoveDir.x:F2}\t{localMoveDir.z:F2}\t{clipStr}");
            }
            frame++;
            if (frame >= framesPer) { frame = 0; idx++; }
        }
    }
}

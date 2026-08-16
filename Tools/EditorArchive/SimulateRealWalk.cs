using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 模拟真实走路（不禁用 PlayerController）：通过反射调用 PlayerController 的私有字段
/// 注入移动输入（模拟按 W 前进 + 相机朝向 0°），观察：
/// - 角色 transform 朝向（应 = 移动方向）
/// - Animator 的 AimX/AimZ（应 = 0,1 前向，因为非瞄准走路强制前向）
/// - 实际播放的 clip
/// 菜单：工具/模拟真实走路（英文别名 Tools/SimulateRealWalk）
/// </summary>
public static class SimulateRealWalk
{
    [MenuItem("工具/模拟真实走路", false, 1114)]
    [MenuItem("Tools/SimulateRealWalk", false, 1114)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[真实走路] 请先激活 RangedPlayer 并在 Play Mode 下运行");
            return;
        }
        var anim = female.GetComponent<Animator>();
        var player = GameObject.Find("Player");
        var pc = player != null ? player.GetComponent<PlayerController>() : null;
        if (pc == null) { Debug.LogError("[真实走路] 找不到 PlayerController"); return; }

        // 反射注入 _moveDirection（模拟按 W 前进）
        var field = typeof(PlayerController).GetField("_moveDirection",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fieldSprint = typeof(PlayerController).GetField("_isSprinting",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) field.SetValue(pc, new Vector3(0f, 0f, 1f)); // W = 前进
        if (fieldSprint != null) fieldSprint.SetValue(pc, false);        // 不冲刺

        // 相机朝向 0°
        var cam = Camera.main;
        if (cam != null) cam.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[真实走路] 帧{Time.frameCount} 模拟按 W 前进");
        sb.AppendLine("帧\t角色Y°\tSpeed\tAimX\tAimZ\tIsAiming\tclip");

        int frame = 0;
        const int total = 12;
        EditorApplication.update += Step;

        void Step()
        {
            // 每帧重新注入输入（PlayerController.Update 会覆盖 _moveDirection？不会，它读 Input.GetAxis）
            var info = anim.GetCurrentAnimatorStateInfo(0);
            var clips = anim.GetCurrentAnimatorClipInfo(0);
            string clipStr = clips.Length > 0 ? clips[0].clip.name : "(无)";
            sb.AppendLine($"{Time.frameCount}\t{female.transform.eulerAngles.y:F1}\t" +
                          $"{anim.GetFloat("Speed"):F2}\t{anim.GetFloat("AimX"):F2}\t{anim.GetFloat("AimZ"):F2}\t" +
                          $"{anim.GetBool("IsAiming")}\t{clipStr}");
            frame++;
            if (frame >= total)
            {
                EditorApplication.update -= Step;
                if (field != null) field.SetValue(pc, Vector3.zero); // 还原
                Debug.Log(sb.ToString());
                try { System.IO.File.AppendAllText("D:/tmp/real_walk.txt", sb.ToString() + "\n"); } catch { }
            }
        }
    }
}

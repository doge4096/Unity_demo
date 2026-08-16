using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 测试 AimWalk 2D 混合树在不同 AimX/AimZ 输入下的实际输出 clip 权重，
/// 定位"走路朝向偏左前"：检查 (0,0) 输入时 SimpleDirectional 混合树是否权重混乱。
/// 菜单：工具/测试走路混合方向（英文别名 Tools/TestWalkBlendDir）
/// </summary>
public static class TestWalkBlendDir
{
    [MenuItem("工具/测试走路混合方向", false, 1096)]
    [MenuItem("Tools/TestWalkBlendDir", false, 1096)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[混合方向] 请先激活 RangedPlayer 并在 Play Mode 下运行");
            return;
        }
        var anim = female.GetComponent<Animator>();
        if (anim == null) return;

        var player = GameObject.Find("Player");
        var pc = player != null ? player.GetComponent<PlayerController>() : null;
        bool pcWas = pc != null && pc.enabled;
        if (pc != null) pc.enabled = false;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[混合方向] 帧{Time.frameCount} 控制器={anim.runtimeAnimatorController?.name}");

        // 测试输入：前 / 右 / 后 / 左 / 原点 / 左前
        var inputs = new (string label, float x, float z)[]
        {
            ("前(0,1)", 0f, 1f),
            ("右(1,0)", 1f, 0f),
            ("后(0,-1)", 0f, -1f),
            ("左(-1,0)", -1f, 0f),
            ("原点(0,0)", 0f, 0f),
            ("左前(-0.7,0.7)", -0.7f, 0.7f),
        };

        anim.SetBool("IsAiming", false);
        anim.SetFloat("Speed", 0.4f);
        anim.Play("Walk", 0, 0f);

        int idx = 0;
        int frame = 0;
        const int framesPerInput = 6;
        EditorApplication.update += Step;

        void Step()
        {
            if (idx >= inputs.Length)
            {
                EditorApplication.update -= Step;
                if (pc != null) pc.enabled = pcWas;
                Debug.Log(sb.ToString());
                try { System.IO.File.AppendAllText("D:/tmp/walk_blend_dir.txt", sb.ToString() + "\n"); } catch { }
                return;
            }

            var (label, x, z) = inputs[idx];
            anim.SetFloat("Speed", 0.4f);
            anim.SetFloat("AimX", x);
            anim.SetFloat("AimZ", z);
            var info = anim.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != Animator.StringToHash("Walk"))
                anim.Play("Walk", 0, info.normalizedTime);

            if (frame == 2) // 混合树稳定后采样一次
            {
                var clips = anim.GetCurrentAnimatorClipInfo(0);
                string clipStr = clips.Length > 0
                    ? string.Join(" | ", System.Array.ConvertAll(clips, c => $"{c.clip.name}×{c.weight:F2}"))
                    : "(无)";
                sb.AppendLine($"{label} (AimX={x:F2} AimZ={z:F2}) → {clipStr}");
            }

            frame++;
            if (frame >= framesPerInput)
            {
                frame = 0;
                idx++;
            }
        }
    }
}

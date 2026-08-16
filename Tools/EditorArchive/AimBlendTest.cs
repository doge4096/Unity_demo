using UnityEditor;
using UnityEngine;

/// <summary>
/// 运行时测试：强制进入 AimWalk（2D 混合树）并依次驱动 AimX/AimZ 方向，
/// 采样每帧实际输出的子动画 clip 名 + 权重，验证方向切换是否生效。
/// 菜单：工具/混合树方向测试（英文别名 Tools/AimBlendTest）
/// 结果写入 D:/tmp/aim_blend_test.txt
/// </summary>
public static class AimBlendTest
{
    /// <summary>自然过渡测试：不强制 Play，每帧只设参数，验证状态机是否能自然进入 AimWalk</summary>
    [MenuItem("工具/混合树自然过渡测试", false, 1010)]
    [MenuItem("Tools/AimBlendNaturalTest", false, 1010)]
    public static void RunNatural()
    {
        var female = GameObject.Find("Female");
        if (female == null || !Application.isPlaying)
        {
            Debug.LogError("[自然过渡] 请先激活 RangedPlayer 并在 Play Mode 下运行");
            return;
        }
        var anim = female.GetComponent<Animator>();
        if (anim == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[自然过渡] 帧{Time.frameCount} 控制器={anim.runtimeAnimatorController?.name}");
        int frame = 0;
        const int totalFrames = 60;
        EditorApplication.update += Step;

        void Step()
        {
            if (frame >= totalFrames)
            {
                EditorApplication.update -= Step;
                Debug.Log(sb.ToString());
                try { System.IO.File.AppendAllText("D:/tmp/aim_blend_test.txt", sb.ToString() + "\n"); } catch { }
                return;
            }

            // 模拟 PlayerController 每帧写入的参数（不强制 Play）
            anim.SetBool("IsAiming", true);
            anim.SetFloat("Speed", 0.4f);
            anim.SetFloat("AimX", 1f);
            anim.SetFloat("AimZ", 0f);

            frame++;
            if (frame % 10 == 0)
            {
                var st = anim.GetCurrentAnimatorStateInfo(0);
                var clips = anim.GetCurrentAnimatorClipInfo(0);
                string clipStr = clips.Length > 0 ? clips[0].clip.name : "(无)";
                sb.AppendLine($"帧{frame}: 状态hash={st.shortNameHash} 状态名={StateName(anim, 0)} clip={clipStr} " +
                              $"IsAiming={anim.GetBool("IsAiming")} Speed={anim.GetFloat("Speed"):F2} " +
                              $"AimX={anim.GetFloat("AimX"):F2} AimZ={anim.GetFloat("AimZ"):F2}");
            }
        }
    }

    [MenuItem("工具/混合树方向测试", false, 1009)]
    [MenuItem("Tools/AimBlendTest", false, 1009)]
    public static void Run()
    {
        var female = GameObject.Find("Female");
        if (female == null)
        {
            Debug.LogError("[混合测试] 找不到 Female，请先激活 RangedPlayer");
            return;
        }
        var anim = female.GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogError("[混合测试] Female 无 Animator");
            return;
        }
        // 腿部骨骼（膝弯角用，与 SampleKnee 一致）
        var lul = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg");
        var ll = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg/mixamorig1:LeftLeg");
        var rl = female.transform.Find("mixamorig1:Hips/mixamorig1:RightUpLeg/mixamorig1:RightLeg");
        if (lul == null || ll == null)
            Debug.LogWarning("[混合测试] 找不到腿部骨骼路径（mixamorig1 前缀）");
        if (!Application.isPlaying)
        {
            Debug.LogError("[混合测试] 请在 Play Mode 下运行");
            return;
        }

        // 方向序列：前/右/后/左/斜向前右
        var dirs = new (string name, float x, float z)[]
        {
            ("前", 0f, 1f),
            ("右", 1f, 0f),
            ("后", 0f, -1f),
            ("左", -1f, 0f),
            ("斜前右", 0.7f, 0.7f),
        };

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[混合测试] 帧{Time.frameCount} 控制器={anim.runtimeAnimatorController?.name}");
        int idx = 0;
        int frame = 0;
        const int sampleEvery = 6;   // 每 6 帧采样一次
        const int framesPerDir = 18; // 每个方向保持 18 帧

        anim.Play("AimWalk", 0, 0f);
        EditorApplication.update += Step;

        void Step()
        {
            if (idx >= dirs.Length)
            {
                EditorApplication.update -= Step;
                Debug.Log(sb.ToString());
                try { System.IO.File.AppendAllText("D:/tmp/aim_blend_test.txt", sb.ToString() + "\n"); } catch { }
                return;
            }

            // 每帧强制留在 AimWalk 并写入方向参数（PlayerController 每帧会覆盖 IsAiming/Speed）
            anim.Play("AimWalk", 0, 0f);
            anim.SetBool("IsAiming", true);
            anim.SetFloat("Speed", 0.4f);
            anim.SetFloat("AimX", dirs[idx].x);
            anim.SetFloat("AimZ", dirs[idx].z);

            frame++;
            if (frame % sampleEvery == 0)
            {
                var st = anim.GetCurrentAnimatorStateInfo(0);
                var clips = anim.GetCurrentAnimatorClipInfo(0);
                string clipStr = clips.Length > 0
                    ? string.Join(" | ", System.Array.ConvertAll(clips, c => $"{c.clip.name}×{c.weight:F2}"))
                    : "(无)";
                // 膝弯角：大腿/小腿本地向下轴夹角
                string kneeStr = "骨骼缺失";
                if (lul != null && ll != null)
                {
                    float knee = Vector3.Angle(lul.TransformDirection(Vector3.down), ll.TransformDirection(Vector3.down));
                    string rlStr = rl != null ? rl.localEulerAngles.x.ToString("F1") : "?";
                    kneeStr = $"膝弯={knee:F1}° 左腿X={ll.localEulerAngles.x:F1} 右腿X={rlStr}";
                }
                sb.AppendLine($"[{dirs[idx].name}] AimX={anim.GetFloat("AimX"):F2} AimZ={anim.GetFloat("AimZ"):F2} " +
                              $"状态hash={st.shortNameHash} clips: {clipStr} | {kneeStr}");
            }

            if (frame >= framesPerDir)
            {
                frame = 0;
                idx++;
                if (idx < dirs.Length)
                    anim.Play("AimWalk", 0, 0f);
            }
        }
    }

    private static string StateName(Animator a, int layer)
    {
        if (a == null || a.runtimeAnimatorController == null) return "?";
        var info = a.GetCurrentAnimatorStateInfo(layer);
        var ctrl = a.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (ctrl == null || ctrl.layers.Length <= layer) return "?";
        foreach (var cs in ctrl.layers[layer].stateMachine.states)
        {
            if (cs.state.nameHash == info.shortNameHash)
                return cs.state.name;
        }
        return $"hash{info.shortNameHash % 10000}";
    }
}

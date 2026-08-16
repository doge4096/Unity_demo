using UnityEditor;
using UnityEngine;

/// <summary>
/// 运行时采样：膝盖弯曲角 + 状态名 + 参数（配合 MCP 分步驱动）
/// 菜单：工具/采样膝盖与状态（英文别名 Tools/SampleKneeAndState）
/// 膝弯角 = 大腿方向(LeftUpLeg 向下) 与 小腿方向(LeftLeg 向下) 的夹角，0°=伸直
/// </summary>
public static class SampleKnee
{
    [MenuItem("工具/采样膝盖与状态", false, 1002)]
    [MenuItem("Tools/SampleKneeAndState", false, 1002)]
    public static void Sample()
    {
        var female = GameObject.Find("Female");
        if (female == null) { Debug.LogError("[膝盖] 找不到 Female（可能被 GameManager 隐藏）"); return; }
        var anim = female.GetComponent<Animator>();
        var h = female.transform.Find("mixamorig1:Hips");
        var lul = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg");
        var ll = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg/mixamorig1:LeftLeg");
        var lf = female.transform.Find("mixamorig1:Hips/mixamorig1:LeftUpLeg/mixamorig1:LeftLeg/mixamorig1:LeftFoot");
        if (lul == null || ll == null)
        {
            Debug.LogError($"[膝盖] 骨骼路径不匹配: LeftUpLeg={(lul != null)} LeftLeg={(ll != null)} LeftFoot={(lf != null)}");
            return;
        }

        // 膝弯角：大腿/小腿的骨骼本地向下轴夹角（0°=完全伸直）
        Vector3 thigh = lul.TransformDirection(Vector3.down);
        Vector3 shin = ll.TransformDirection(Vector3.down);
        float kneeAngle = Vector3.Angle(thigh, shin);

        // 髋关节摆动幅度参考：Hips Y 位移（动画在播放时波动）
        string s0 = StateName(anim, 0);
        string s1 = StateName(anim, 1);
        string line = $"[膝盖] {Time.frameCount} 状态={s0}/{s1} | 膝弯角={kneeAngle:F1}° | LeftLegX={ll.localEulerAngles.x:F1} | " +
                      $"HipsY={h.position.y:F3} | HipsWorld={h.position.ToString("F2")} | GOY={female.transform.position.y:F2} | " +
                      $"Speed={anim.GetFloat("Speed"):F2} Aim={anim.GetBool("IsAiming")} " +
                      $"AimX={anim.GetFloat("AimX"):F2} AimZ={anim.GetFloat("AimZ"):F2}";
        Debug.Log(line);
        // 同时写文件（绕开 console 轮询被字体警告淹没的问题）
        try { System.IO.File.AppendAllText("D:/tmp/sample_knee.txt", line + "\n"); }
        catch { }
    }

    private static string StateName(Animator a, int layer)
    {
        if (a == null || a.runtimeAnimatorController == null) return "?";
        var info = a.GetCurrentAnimatorStateInfo(layer);
        var ctrl = (UnityEditor.Animations.AnimatorController)a.runtimeAnimatorController;
        if (ctrl == null || ctrl.layers.Length <= layer) return "?";
        foreach (var cs in ctrl.layers[layer].stateMachine.states)
        {
            if (cs.state.nameHash == info.shortNameHash)
                return cs.state.name;
        }
        return $"hash{info.shortNameHash % 10000}";
    }
}

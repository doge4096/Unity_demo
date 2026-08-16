using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Text;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 骨骼姿态诊断：实例化 Female.fbx + 临时控制器播放 female_Walk_fixed，
/// 逐帧采样实际骨骼姿态：
/// - 膝盖角：UpperLeg 骨骼局部旋转（模拟弯曲）
/// - 腰部：Hips/Spine 世界旋转角速度
/// 输出逐帧角速度 + 振荡统计，定位"摇动"来源
/// 菜单：Tools/Diag Bone Pose（英文）
/// </summary>
public static class BonePoseDiag
{
    private const string ModelPath = "Assets/Art/Models/Female.fbx";
    private const string ClipPath = "Assets/Art/Animations/Fixed/female_Walk_fixed.anim";

    [MenuItem("Tools/Diag Bone Pose")]
    public static void Run()
    {
        var sb = new StringBuilder();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (model == null || clip == null) { Debug.LogError("模型或动画不存在"); return; }

        string tmpCtrl = "Assets/Screenshots/_tmp_bonepose.controller";
        if (File.Exists(tmpCtrl)) AssetDatabase.DeleteAsset(tmpCtrl);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(tmpCtrl);
        ctrl.layers[0].stateMachine.AddState("Play").motion = clip;

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        inst.transform.position = Vector3.zero;
        var anim = inst.GetComponent<Animator>();
        var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(ModelPath);
        if (avatar != null && avatar.isHuman) anim.avatar = avatar;
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.Rebind();

        // 找骨骼（模糊匹配）：先打印所有含 leg/hip/spine 的骨骼名
        sb.AppendLine("骨架中 leg/hip/spine 相关骨骼:");
        foreach (var t in inst.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLower();
            if (n.Contains("leg") || n.Contains("hip") || n.Contains("spine"))
                sb.AppendLine($"  '{t.name}' 父='{(t.parent != null ? t.parent.name : "-")}'");
        }

        var bones = new Dictionary<string, Transform>();
        foreach (var t in inst.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLower();
            if (n.Contains("hips")) bones["hips"] = t;
            if (n.Contains("spine") && !bones.ContainsKey("spine")) bones["spine"] = t;
            if (n.Contains("upleg") && n.Contains("left")) bones["upperL"] = t;
            if (n.Contains("upleg") && n.Contains("right")) bones["upperR"] = t;
            if (n == "leftleg" || (n.Contains("left") && n.Contains("leg") && !n.Contains("upleg"))) bones["lowerL"] = t;
            if (n == "rightleg" || (n.Contains("right") && n.Contains("leg") && !n.Contains("upleg"))) bones["lowerR"] = t;
        }
        sb.AppendLine("匹配结果: " + string.Join(", ", bones.Keys));
        foreach (var kv in bones) sb.AppendLine($"  {kv.Key} = {kv.Value.name} 路径={PathOf(kv.Value)}");

        // 逐帧采样（60fps 按 clip 帧率）
        float dt = 1f / clip.frameRate;
        int frames = Mathf.CeilToInt(clip.length * clip.frameRate);
        sb.AppendLine($"\n-- 逐帧采样（dt={dt * 1000:F0}ms，倍速1x）--");
        sb.AppendLine("帧\t膝L°\t膝R°\tHips俯仰\tSpine俯仰\tHips角速°/帧\tSpine角速°/帧");

        float prevHipsY = 0, prevSpineY = 0;
        float prevLegLY = 0, prevLegRY = 0;
        var kL = new List<float>(); var kR = new List<float>();
        var hY = new List<float>(); var sY = new List<float>();
        var legLD = new List<float>(); var legRD = new List<float>();
        for (int i = 0; i <= frames; i++)
        {
            anim.Play("Play", 0, i / (float)frames);
            anim.Update(dt);
            float kl = KneeAngle(bones, "upperL", "lowerL");
            float kr = KneeAngle(bones, "upperR", "lowerR");
            float hy = bones.ContainsKey("hips") ? bones["hips"].eulerAngles.x : 0;
            float sy = bones.ContainsKey("spine") ? bones["spine"].eulerAngles.x : 0;
            // 小腿骨骼自身旋转（左/右 Leg）——用欧拉角 X，且记录帧间最短弧角速度
            float llx = bones.ContainsKey("lowerL") ? bones["lowerL"].localEulerAngles.x : 0;
            float lrx = bones.ContainsKey("lowerR") ? bones["lowerR"].localEulerAngles.x : 0;
            kL.Add(kl); kR.Add(kr); hY.Add(hy); sY.Add(sy);
            legLD.Add(llx); legRD.Add(lrx);
            float dh = i == 0 ? 0 : Mathf.Abs(Mathf.DeltaAngle(prevHipsY, hy));
            float ds = i == 0 ? 0 : Mathf.Abs(Mathf.DeltaAngle(prevSpineY, sy));
            float dll = i == 0 ? 0 : Mathf.Abs(Mathf.DeltaAngle(prevLegLY, llx));
            float dlr = i == 0 ? 0 : Mathf.Abs(Mathf.DeltaAngle(prevLegRY, lrx));
            if (i % 2 == 0 || i == frames)
                sb.AppendLine($"{i:D3}\t{kl:F1}\t{kr:F1}\t{hy:F1}\t{sy:F1}\t小L{dll:F1}\t小R{dlr:F1}\tH{dh:F2}\tS{ds:F2}");
            prevHipsY = hy; prevSpineY = sy; prevLegLY = llx; prevLegRY = lrx;
        }

        // 角速度统计（DeltaAngle 去环绕）
        sb.AppendLine($"\n-- 统计（最短弧角速度，单位 度/帧@60fps）--");
        sb.AppendLine($"膝L(大腿): 最大帧间角速={MaxSpeed(kL):F1}°/帧 平均={AvgSpeed(kL):F1}°/帧 翻转={Flips(kL)}次");
        sb.AppendLine($"膝R(大腿): 最大帧间角速={MaxSpeed(kR):F1}°/帧 平均={AvgSpeed(kR):F1}°/帧 翻转={Flips(kR)}次");
        sb.AppendLine($"小L(小腿): 最大帧间角速={MaxSpeed(legLD):F1}°/帧 平均={AvgSpeed(legLD):F1}°/帧 翻转={Flips(legLD)}次");
        sb.AppendLine($"小R(小腿): 最大帧间角速={MaxSpeed(legRD):F1}°/帧 平均={AvgSpeed(legRD):F1}°/帧 翻转={Flips(legRD)}次");
        sb.AppendLine($"Hips俯仰: 最大帧间角速={MaxSpeed(hY):F1}°/帧 平均={AvgSpeed(hY):F1}°/帧 翻转={Flips(hY)}次");
        sb.AppendLine($"Spine俯仰: 最大帧间角速={MaxSpeed(sY):F1}°/帧 平均={AvgSpeed(sY):F1}°/帧 翻转={Flips(sY)}次");

        Object.DestroyImmediate(inst);
        AssetDatabase.DeleteAsset(tmpCtrl);
        Directory.CreateDirectory("Assets/Screenshots");
        File.WriteAllText("Assets/Screenshots/bone_pose_diag.txt", sb.ToString());
        Debug.Log("[BonePoseDiag] 完成: Assets/Screenshots/bone_pose_diag.txt");
    }

    /// <summary>膝盖角 = 大腿骨骼局部旋转 X 轴（近似弯曲角），骨骼缺失时返回 NaN</summary>
    private static float KneeAngle(Dictionary<string, Transform> bones, string upper, string lower)
    {
        if (!bones.ContainsKey(upper) || !bones.ContainsKey(lower)) return float.NaN;
        // 大腿局部旋转角度（相对父级）
        return bones[upper].localEulerAngles.x;
    }

    private static string PathOf(Transform t)
    {
        var sb = new StringBuilder();
        var p = t;
        while (p != null) { sb.Insert(0, "/" + p.name); p = p.parent; }
        return sb.ToString();
    }

    private static float Min(List<float> l) { float m = float.MaxValue; foreach (var v in l) if (!float.IsNaN(v) && v < m) m = v; return m == float.MaxValue ? 0 : m; }
    private static float Max(List<float> l) { float m = float.MinValue; foreach (var v in l) if (!float.IsNaN(v) && v > m) m = v; return m == float.MinValue ? 0 : m; }
    /// <summary>最大帧间角速度（最短弧，去环绕）</summary>
    private static float MaxSpeed(List<float> l)
    {
        float m = 0;
        for (int i = 1; i < l.Count; i++)
        {
            float d = Mathf.Abs(Mathf.DeltaAngle(l[i - 1], l[i]));
            if (d > m) m = d;
        }
        return m;
    }
    /// <summary>平均帧间角速度</summary>
    private static float AvgSpeed(List<float> l)
    {
        float sum = 0; int n = 0;
        for (int i = 1; i < l.Count; i++)
        {
            sum += Mathf.Abs(Mathf.DeltaAngle(l[i - 1], l[i]));
            n++;
        }
        return n == 0 ? 0 : sum / n;
    }
    private static int Flips(List<float> l)
    {
        int dir = 0, flips = 0;
        for (int i = 1; i < l.Count; i++)
        {
            float d = Mathf.DeltaAngle(l[i - 1], l[i]);
            if (Mathf.Abs(d) < 0.1f) continue;
            int nd = d > 0 ? 1 : -1;
            if (dir != 0 && nd != dir) flips++;
            dir = nd;
        }
        return flips;
    }
}

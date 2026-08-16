using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 细化采样：女角色左腿 vs 男角色左腿 的逐帧旋转角差，定位甩动的时间分布
/// 菜单：Tools/Sample Left Leg Detail（英文）
/// </summary>
public static class LeftLegDetailSampler
{
    [MenuItem("Tools/Sample Left Leg Detail")]
    public static void Sample()
    {
        var sb = new StringBuilder();
        SampleLeg("男(正常): man LeftLeg", "Assets/Art/Models/man.fbx",
            "Assets/Art/Animators/MeleeAnimator.controller", "Walk", "mixamorig:LeftLeg", sb);
        SampleLeg("女(甩动): Female LeftUpLeg", "Assets/Art/Models/Female.fbx",
            "Assets/Art/Animators/RangedAnimator.controller", "Walk", "mixamorig1:LeftUpLeg", sb);
        SampleLeg("女(甩动): Female LeftFoot", "Assets/Art/Models/Female.fbx",
            "Assets/Art/Animators/RangedAnimator.controller", "Walk", "mixamorig1:LeftFoot", sb);

        var outPath = "Assets/Screenshots/leftleg_detail.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[LeftLegDetail] 完成，结果: " + outPath);
    }

    private static void SampleLeg(string label, string modelPath, string ctrlPath, string stateName, string bonePath, StringBuilder sb)
    {
        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlPath);
        if (modelPrefab == null || ctrl == null) { sb.AppendLine($"[{label}] 加载失败"); return; }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
        inst.transform.position = new Vector3(0f, 50f, 0f);
        var anim = inst.GetComponent<Animator>();
        if (anim == null) anim = inst.AddComponent<Animator>();
        var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(modelPath);
        if (avatar != null && avatar.isHuman) anim.avatar = avatar;
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var bone = FindInChildren(inst.transform, bonePath);
        sb.AppendLine($"\n===== {label} =====");
        if (bone == null) { sb.AppendLine($"找不到骨骼 {bonePath}"); Object.DestroyImmediate(inst); return; }

        int steps = 60; // 0.05s 步进，60 步 = 3s
        Quaternion prev = Quaternion.identity;
        bool first = true;
        float sum = 0f, worst = 0f, worstT = 0f;
        int over20 = 0;
        for (int i = 0; i <= steps; i++)
        {
            float nt = i / (float)steps;
            anim.Play(stateName, 0, Mathf.Repeat(nt, 1f));
            anim.Update(0.05f);
            var rot = bone.localRotation;
            if (!first)
            {
                float diff = Quaternion.Angle(prev, rot);
                sum += diff;
                if (diff > worst) { worst = diff; worstT = i * 0.05f; }
                if (diff > 20f) over20++;
                // 打印每 5 步的跳变，及所有 >25° 的点
                if (i % 5 == 0 || diff > 25f)
                    sb.AppendLine($"  t={i * 0.05f:F2}s 角差={diff:F1}°{(diff > 25f ? "  <<< 异常跳变" : "")}");
            }
            prev = rot;
            first = false;
        }
        sb.AppendLine($"  累计={sum:F1}° 平均={sum / steps:F1}° 单段最大={worst:F1}°(@{worstT:F2}s) 超20°次数={over20}");

        Object.DestroyImmediate(inst);
    }

    private static Transform FindInChildren(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (child.name == name) return child;
            var found = FindInChildren(child, name);
            if (found != null) return found;
        }
        return null;
    }
}

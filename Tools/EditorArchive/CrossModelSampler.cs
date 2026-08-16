using UnityEditor;
using UnityEngine;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 变量分离实验：同一动画在不同模型上的脚部骨骼旋转轨迹
/// 对比 female 模型 vs male 模型跑 female_Walk_fixed 的左脚旋转（每帧欧拉角）
/// 如果 male 模型正常 → 问题在 Female 模型的 Avatar/骨骼；如果都怪异 → 动画问题
/// 菜单：Tools/Sample Cross Model（英文）
/// </summary>
public static class CrossModelSampler
{
    [MenuItem("Tools/Sample Cross Model")]
    public static void Run()
    {
        var sb = new StringBuilder();
        SampleFoots("女模型+女动画(现状)", "Assets/Art/Models/Female.fbx",
            "Assets/Art/Animators/RangedAnimator.controller", "Walk", "mixamorig1:", sb);
        SampleFoots("男模型+女动画(实验)", "Assets/Art/Models/man.fbx",
            "Assets/Art/Animators/RangedAnimator.controller", "Walk", "mixamorig:", sb);

        var outPath = "Assets/Screenshots/cross_model.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[CrossModel] 完成，结果: " + outPath);
    }

    private static void SampleFoots(string label, string modelPath, string ctrlPath, string stateName, string prefix, StringBuilder sb)
    {
        sb.AppendLine($"\n========== {label} ==========");
        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlPath);
        if (modelPrefab == null || ctrl == null) { sb.AppendLine("加载失败"); return; }

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

        // 采样 LeftFoot/LeftToeBase/RightFoot 的每帧 localEulerAngles
        var lf = FindInChildren(inst.transform, prefix + "LeftFoot");
        var lt = FindInChildren(inst.transform, prefix + "LeftToeBase");
        var rf = FindInChildren(inst.transform, prefix + "RightFoot");
        if (lf == null) { sb.AppendLine("找不到 LeftFoot（prefix=" + prefix + "）"); Object.DestroyImmediate(inst); return; }

        int steps = 24;
        for (int i = 0; i <= steps; i++)
        {
            float nt = i / (float)steps;
            anim.Play(stateName, 0, nt);
            anim.Update(0.1f);
            string le = lf.localEulerAngles.ToString("F1");
            string te = lt != null ? lt.localEulerAngles.ToString("F1") : "无";
            string re = rf != null ? rf.localEulerAngles.ToString("F1") : "无";
            sb.AppendLine($"t={i * 100 / steps,3}%: LFoot={le} LToe={te} RFoot={re}");
        }
        Object.DestroyImmediate(inst);
    }

    private static Transform FindInChildren(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>())
            if (t.name == name) return t;
        return null;
    }
}

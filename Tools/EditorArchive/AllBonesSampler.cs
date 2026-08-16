using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// 全骨骼采样诊断（决定性实验）：4 组矩阵（模型 × 动画）交叉采样，
/// 输出所有骨骼的单帧最大跳变（Quaternion.Angle），定位问题在模型还是动画
/// - 女+女 高、女+男 不高 → 女动画问题
/// - 女+女 高、男+女 不高 → 女模型/Avatar 问题
/// - 女+女 高、女+男 也高 → 女模型通用问题
/// 阈值 15° = 视觉可见甩动（0.033s 内转 15°）
/// 菜单：Tools/Sample All Bones（英文）
/// </summary>
public static class AllBonesSampler
{
    const float Threshold = 15f;

    [MenuItem("Tools/Sample All Bones")]
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("========== 全骨骼单帧最大跳变（°）对比矩阵 ==========");
        sb.AppendLine("（>15° 标 [甩]，左右骨骼分别列出）");

        // 4 组矩阵：模型 × 动画
        var groups = new (string label, string model, string clip)[]
        {
            ("女+女", "Assets/Art/Models/Female.fbx", "Assets/Art/Animations/Fixed/female_Walk_fixed.anim"),
            ("女+男", "Assets/Art/Models/Female.fbx", "Assets/Art/Animations/Fixed/man_Walking_fixed.anim"),
            ("男+女", "Assets/Art/Models/man.fbx", "Assets/Art/Animations/Fixed/female_Walk_fixed.anim"),
            ("男+男", "Assets/Art/Models/man.fbx", "Assets/Art/Animations/Fixed/man_Walking_fixed.anim"),
        };
        var results = new List<(string label, Dictionary<string, float> worst)>();
        foreach (var g in groups)
            results.Add((g.label, SampleOne(g.label, g.model, g.clip, sb)));

        // 汇总全部骨骼名（去模型前缀）
        var bones = new SortedSet<string>();
        foreach (var r in results)
            foreach (var k in r.worst.Keys)
                bones.Add(k);

        sb.AppendLine($"\n骨骼名{"",-12} 女+女  女+男  男+女  男+男");
        foreach (var bone in bones)
        {
            float w0 = GetWorst(results[0], bone), w1 = GetWorst(results[1], bone);
            float w2 = GetWorst(results[2], bone), w3 = GetWorst(results[3], bone);
            bool anyBad = w0 > Threshold || w1 > Threshold || w2 > Threshold || w3 > Threshold;
            sb.AppendLine($"{bone,-16} {w0,5:F1}  {w1,5:F1}  {w2,5:F1}  {w3,5:F1}{(anyBad ? "  [甩]" : "")}");
        }

        var outPath = "Assets/Screenshots/all_bones.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[AllBones] 完成，结果: " + outPath);
    }

    private static float GetWorst((string label, Dictionary<string, float> worst) r, string bone)
        => r.worst.TryGetValue(bone, out var v) ? v : -1f;

    /// <summary>单组采样：临时控制器播放指定 clip，记录所有骨骼单帧最大跳变</summary>
    private static Dictionary<string, float> SampleOne(string label, string modelPath, string clipPath, StringBuilder sb)
    {
        var worst = new Dictionary<string, float>();
        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (modelPrefab == null || clip == null)
        {
            sb.AppendLine($"[{label}] 加载失败 模型={modelPrefab != null} clip={clip != null}");
            return worst;
        }

        // 临时控制器（只含一个状态，motion = 目标 clip）
        string tmpCtrl = "Assets/Screenshots/_tmp_sampler.controller";
        if (File.Exists(tmpCtrl)) AssetDatabase.DeleteAsset(tmpCtrl);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(tmpCtrl);
        ctrl.layers[0].stateMachine.AddState("Play").motion = clip;

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
        if (inst == null) return worst;
        inst.transform.position = new Vector3(0f, 50f, 0f);
        var anim = inst.GetComponent<Animator>();
        if (anim == null) anim = inst.AddComponent<Animator>();
        var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(modelPath);
        if (avatar != null && avatar.isHuman) anim.avatar = avatar;
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // 采样全部骨骼（20 步，每步记录帧间跳变）
        var prevRot = new Dictionary<Transform, Quaternion>();
        for (int i = 0; i <= 20; i++)
        {
            float nt = clip.length > 0f ? i / 20f : 0f;
            anim.Play("Play", 0, Mathf.Repeat(nt, 1f));
            anim.Update(0.1f);
            foreach (var b in inst.GetComponentsInChildren<Transform>())
            {
                if (b == inst.transform) continue;
                string name = b.name;
                // 去模型前缀（mixamorig: / mixamorig1: / CC_Base_ 等）
                int colon = name.LastIndexOf(':');
                if (colon >= 0) name = name.Substring(colon + 1);
                if (name.StartsWith("CC_Base_")) name = name.Substring("CC_Base_".Length);

                if (prevRot.TryGetValue(b, out var pr))
                {
                    float diff = Quaternion.Angle(pr, b.localRotation);
                    if (!worst.TryGetValue(name, out var w) || diff > w)
                        worst[name] = diff;
                }
                prevRot[b] = b.localRotation;
            }
        }

        Object.DestroyImmediate(inst);
        AssetDatabase.DeleteAsset(tmpCtrl);
        return worst;
    }
}

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 采样诊断（方法 B）：Animator 重定向驱动后采样骨骼旋转帧间跳变
/// 对比男(正常)女(甩动)走路动画在模型上的表现，验证 hasExtraRoot 假设
/// 菜单：Tools/Sample Retarget Jitter（英文）
/// </summary>
public static class BoneJitterSampler
{
    [MenuItem("Tools/Sample Retarget Jitter")]
    public static void Sample()
    {
        var sb = new StringBuilder();
        SampleOne("男(正常): man + MeleeAnimator/Walk", "Assets/Art/Models/man.fbx",
            "Assets/Art/Animators/MeleeAnimator.controller", "Walk", sb);
        SampleOne("女(甩动): Female + RangedAnimator/Walk", "Assets/Art/Models/Female.fbx",
            "Assets/Art/Animators/RangedAnimator.controller", "Walk", sb);

        var outPath = "Assets/Screenshots/retarget_jitter.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[JitterSampler] 完成，结果: " + outPath);
    }

    private static void SampleOne(string label, string modelPath, string ctrlPath, string stateName, StringBuilder sb)
    {
        sb.AppendLine($"\n========== {label} ==========");
        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlPath);
        if (modelPrefab == null || ctrl == null)
        {
            sb.AppendLine($"加载失败: 模型={modelPrefab != null} 控制器={ctrl != null}");
            return;
        }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
        if (inst == null) { sb.AppendLine("实例化失败"); return; }
        inst.transform.position = new Vector3(0f, 50f, 0f);

        // Animator：用模型自己的 Avatar 做 Humanoid 重定向
        var anim = inst.GetComponent<Animator>();
        if (anim == null) anim = inst.AddComponent<Animator>();
        var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(modelPath);
        if (avatar != null && avatar.isHuman)
            anim.avatar = avatar;
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // 计算 clip 时长（从状态读取第一个 motion）
        var clip = GetStateClip(ctrl, stateName);
        float duration = clip != null && clip.length > 0f ? clip.length : 1f;

        var prevRot = new Dictionary<Transform, Quaternion>();
        var jitterSum = new Dictionary<Transform, float>();
        var worst = new Dictionary<Transform, float>();

        int steps = 20;
        for (int i = 0; i <= steps; i++)
        {
            float nt = duration > 0f ? i / (float)steps : 0f;
            anim.Play(stateName, 0, Mathf.Repeat(nt, 1f));
            anim.Update(0.1f);
            foreach (var b in inst.GetComponentsInChildren<Transform>())
            {
                if (b == inst.transform) continue;
                var rot = b.localRotation;
                if (prevRot.TryGetValue(b, out var pr))
                {
                    float diff = Quaternion.Angle(pr, rot);
                    if (!jitterSum.ContainsKey(b)) { jitterSum[b] = 0f; worst[b] = 0f; }
                    jitterSum[b] += diff;
                    if (diff > worst[b]) worst[b] = diff;
                }
                prevRot[b] = rot;
            }
        }

        sb.AppendLine($"clip 时长={duration:F3}s avatar={anim.avatar?.name} isHuman={anim.avatar?.isHuman}");
        var list = new List<KeyValuePair<Transform, float>>(jitterSum);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        sb.AppendLine($"--- 重定向后累计跳变 Top 10 ---");
        for (int i = 0; i < Mathf.Min(10, list.Count); i++)
        {
            var kp = list[i];
            sb.AppendLine($"  {kp.Key.name}: 累计={kp.Value:F1}° 单帧最大={worst[kp.Key]:F1}°");
        }

        Object.DestroyImmediate(inst);
    }

    private static AnimationClip GetStateClip(UnityEditor.Animations.AnimatorController ctrl, string stateName)
    {
        foreach (var layer in ctrl.layers)
        {
            foreach (var state in layer.stateMachine.states)
            {
                if (state.state.name == stateName)
                    return state.state.motion as AnimationClip;
            }
        }
        return null;
    }
}

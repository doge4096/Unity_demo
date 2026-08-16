using UnityEditor;
using UnityEngine;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 实验：把 aimWalk 的手臂/肩膀曲线替换为 aimShoot 的（持枪姿态），
/// 生成 female_aimWalk3_fixed.anim，验证走路持枪枪口是否朝前（左手世界 Y 接近 0）。
/// 只替换 Left/Right Shoulder + Arm + Hand 肌肉曲线（Down-Up/Front-Back/Twist/In-Out），
/// 腿部曲线保持 aimWalk 原样（走路步态不变）。
/// 菜单：工具/生成持枪修正走路动画（英文别名 Tools/FixWalkGunPose）
/// </summary>
public static class FixWalkGunPose
{
    private const string SrcPath = "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim";
    private const string GunPath = "Assets/Art/Animations/female_aimShoot.fbx";
    private const string DstPath = "Assets/Art/Animations/Fixed/female_aimWalk3_fixed.anim";

    [MenuItem("工具/生成持枪修正走路动画", false, 1115)]
    [MenuItem("Tools/FixWalkGunPose", false, 1115)]
    public static void Run()
    {
        var sb = new StringBuilder();
        var src = AssetDatabase.LoadAssetAtPath<AnimationClip>(SrcPath);
        var gun = AssetDatabase.LoadAssetAtPath<AnimationClip>(GunPath);
        if (src == null || gun == null)
        {
            sb.AppendLine($"加载失败: src={src != null} gun={gun != null}");
            Debug.Log(sb.ToString());
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(DstPath) != null)
        {
            AssetDatabase.DeleteAsset(DstPath);
            AssetDatabase.SaveAssets();
        }
        AssetDatabase.Refresh();

        var dst = new AnimationClip();
        dst.frameRate = src.frameRate;
        dst.wrapMode = src.wrapMode;
        dst.legacy = false;
        var settings = AnimationUtility.GetAnimationClipSettings(src);
        AnimationUtility.SetAnimationClipSettings(dst, settings);

        // 收集 aimShoot 的手臂曲线（按名字）
        var gunCurves = new Dictionary<string, AnimationCurve>();
        foreach (var b in AnimationUtility.GetCurveBindings(gun))
        {
            if (!IsArmCurve(b.propertyName)) continue;
            var c = AnimationUtility.GetEditorCurve(gun, b);
            if (c != null) gunCurves[b.propertyName] = c;
        }
        sb.AppendLine($"aimShoot 手臂曲线数: {gunCurves.Count}");

        int replaced = 0;
        foreach (var b in AnimationUtility.GetCurveBindings(src))
        {
            var curve = AnimationUtility.GetEditorCurve(src, b);
            if (curve == null) continue;

            // 手臂曲线：用 aimShoot 的对应曲线（按时间比例重采样）
            if (IsArmCurve(b.propertyName) && gunCurves.TryGetValue(b.propertyName, out var gunCurve))
            {
                var newKeys = new Keyframe[curve.length];
                for (int i = 0; i < curve.length; i++)
                {
                    float t = curve.keys[i].time;
                    float srcT = t * gun.length / src.length;
                    float val = gunCurve.Evaluate(srcT);
                    var k = curve.keys[i];
                    k.value = val;
                    k.inTangent = 0f;
                    k.outTangent = 0f;
                    newKeys[i] = k;
                }
                AnimationUtility.SetEditorCurve(dst, b, new AnimationCurve(newKeys));
                replaced++;
                continue;
            }

            // 其余（腿部/躯干）保持 aimWalk 原样
            AnimationUtility.SetEditorCurve(dst, b, curve);
        }

        AssetDatabase.CreateAsset(dst, DstPath);
        EditorUtility.SetDirty(dst);
        AssetDatabase.SaveAssets();

        var verify = AssetDatabase.LoadAssetAtPath<AnimationClip>(DstPath);
        sb.AppendLine($"生成完成: {DstPath}");
        sb.AppendLine($"时长={verify.length:F3}s 帧率={verify.frameRate} 循环={verify.isLooping}");
        sb.AppendLine($"替换手臂曲线 {replaced} 条（来自 aimShoot 持枪姿态）");

        var outPath = "Assets/Screenshots/fix_walk_gun.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log(sb.ToString());
    }

    /// <summary>判断是否为手臂/肩膀/手部肌肉曲线（非腿部/躯干）</summary>
    private static bool IsArmCurve(string name)
    {
        if (name.StartsWith("Left") || name.StartsWith("Right"))
        {
            return name.Contains("Shoulder") || name.Contains("Arm") || name.Contains("Hand") ||
                   name.Contains("Elbow") || name.Contains("Wrist");
        }
        return false;
    }
}

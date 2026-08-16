using UnityEditor;
using UnityEngine;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 精细实验：把 aimWalk 的肩膀曲线（Left/Right Shoulder Down-Up / Front-Back）替换为
/// aimShoot 的肩膀曲线（持枪时肩膀放松 0°），其他曲线（手臂/手/腿）保持 aimWalk 原样。
/// 生成 female_aimWalk4_fixed.anim，验证持枪左手世界朝向是否从 43° 校正到接近 aimShoot 的 16°。
/// 菜单：工具/实验肩膀校正（英文别名 Tools/ExpShoulderFix）
/// </summary>
public static class ExpShoulderFix
{
    private const string SrcPath = "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim";
    private const string GunPath = "Assets/Art/Animations/female_aimShoot.fbx";
    private const string DstPath = "Assets/Art/Animations/Fixed/female_aimWalk4_fixed.anim";

    private static readonly string[] ShoulderCurves = {
        "Left Shoulder Down-Up", "Left Shoulder Front-Back",
        "Right Shoulder Down-Up", "Right Shoulder Front-Back",
    };

    [MenuItem("工具/实验肩膀校正", false, 1118)]
    [MenuItem("Tools/ExpShoulderFix", false, 1118)]
    public static void Run()
    {
        var sb = new StringBuilder();
        var src = AssetDatabase.LoadAssetAtPath<AnimationClip>(SrcPath);
        var gun = AssetDatabase.LoadAssetAtPath<AnimationClip>(GunPath);
        if (src == null || gun == null) { sb.AppendLine($"加载失败 src={src!=null} gun={gun!=null}"); Debug.Log(sb.ToString()); return; }

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

        // aimShoot 肩膀曲线
        var gunCurves = new Dictionary<string, AnimationCurve>();
        foreach (var b in AnimationUtility.GetCurveBindings(gun))
        {
            if (System.Array.IndexOf(ShoulderCurves, b.propertyName) >= 0)
            {
                var c = AnimationUtility.GetEditorCurve(gun, b);
                if (c != null) gunCurves[b.propertyName] = c;
            }
        }
        sb.AppendLine($"aimShoot 肩膀曲线: {gunCurves.Count} 条");

        int replaced = 0;
        foreach (var b in AnimationUtility.GetCurveBindings(src))
        {
            var curve = AnimationUtility.GetEditorCurve(src, b);
            if (curve == null) continue;

            if (System.Array.IndexOf(ShoulderCurves, b.propertyName) >= 0 && gunCurves.TryGetValue(b.propertyName, out var gc))
            {
                var newKeys = new Keyframe[curve.length];
                for (int i = 0; i < curve.length; i++)
                {
                    float t = curve.keys[i].time;
                    float srcT = t * gun.length / src.length;
                    var k = curve.keys[i];
                    k.value = gc.Evaluate(srcT);
                    k.inTangent = 0f;
                    k.outTangent = 0f;
                    newKeys[i] = k;
                }
                AnimationUtility.SetEditorCurve(dst, b, new AnimationCurve(newKeys));
                replaced++;
                continue;
            }

            AnimationUtility.SetEditorCurve(dst, b, curve);
        }

        AssetDatabase.CreateAsset(dst, DstPath);
        EditorUtility.SetDirty(dst);
        AssetDatabase.SaveAssets();

        var verify = AssetDatabase.LoadAssetAtPath<AnimationClip>(DstPath);
        sb.AppendLine($"生成完成: {DstPath} 时长={verify.length:F3}s");
        sb.AppendLine($"替换肩膀曲线 {replaced} 条");

        var outPath = "Assets/Screenshots/exp_shoulder.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log(sb.ToString());
    }
}

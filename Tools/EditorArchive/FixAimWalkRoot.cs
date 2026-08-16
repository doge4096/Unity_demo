using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 修正瞄准走路动画 female_aimWalk_fixed 的持枪姿态：
/// 该动画 RootQ.y=0.3345（根骨骼绕 Y 恒定偏转 ~39°），导致走路时持枪左手/枪口世界朝向偏左 43°（"斜向走"观感）。
/// 与待机 AimIdle 修正同法：生成 female_aimWalk_fixed 修正版（RootQ 置 identity），
/// 生成 female_aimWalk2_fixed.anim 到 Fixed 目录，之后用控制器替换工具指向它。
/// 菜单：工具/生成修正走路动画（英文别名 Tools/FixAimWalkRoot）
/// </summary>
public static class FixAimWalkRoot
{
    private const string SrcPath = "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim";
    private const string DstPath = "Assets/Art/Animations/Fixed/female_aimWalk2_fixed.anim";

    [MenuItem("工具/生成修正走路动画", false, 1112)]
    [MenuItem("Tools/FixAimWalkRoot", false, 1112)]
    public static void Run()
    {
        var sb = new StringBuilder();
        var src = AssetDatabase.LoadAssetAtPath<AnimationClip>(SrcPath);
        if (src == null) { sb.AppendLine($"源动画不存在: {SrcPath}"); Debug.Log(sb.ToString()); return; }

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

        int rootCurves = 0;
        foreach (var b in AnimationUtility.GetCurveBindings(src))
        {
            var curve = AnimationUtility.GetEditorCurve(src, b);
            if (curve == null) continue;

            // RootQ 曲线：消除根骨骼绕 Y 恒定偏转 → identity (0,0,0,1)
            bool isRootQ = b.path == "" && (b.propertyName == "RootQ.x" || b.propertyName == "RootQ.y" ||
                                            b.propertyName == "RootQ.z" || b.propertyName == "RootQ.w");
            if (isRootQ)
            {
                var keys = curve.keys;
                for (int i = 0; i < keys.Length; i++)
                {
                    var k = keys[i];
                    k.value = (b.propertyName == "RootQ.w") ? 1f : 0f;
                    k.inTangent = 0f;
                    k.outTangent = 0f;
                    keys[i] = k;
                }
                AnimationUtility.SetEditorCurve(dst, b, new AnimationCurve(keys));
                rootCurves++;
                continue;
            }

            AnimationUtility.SetEditorCurve(dst, b, curve);
        }

        AssetDatabase.CreateAsset(dst, DstPath);
        EditorUtility.SetDirty(dst);
        AssetDatabase.SaveAssets();

        var verify = AssetDatabase.LoadAssetAtPath<AnimationClip>(DstPath);
        sb.AppendLine($"生成完成: {DstPath}");
        sb.AppendLine($"时长={verify.length:F3}s 帧率={verify.frameRate} 循环={verify.isLooping}");
        sb.AppendLine($"RootQ 曲线修正 {rootCurves} 条 → identity（消除根骨骼绕 Y 偏转 39°）");

        var outPath = "Assets/Screenshots/fix_aimwalk_root.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log(sb.ToString());
    }
}

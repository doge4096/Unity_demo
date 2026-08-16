using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 修正待机（AimIdle）动画角度：female_aimIdle.fbx 的 RootQ 带恒定绕 Y 偏转
/// （RootQ.y=0.361 ≈ 42°，实测待机时 Hips +14°、持枪左手 +63°，姿势不正）。
/// 生成 female_aimIdle_fixed.anim：复制全部曲线，把 RootQ 设为 identity（消除根骨骼偏转），
/// guid 新生成，需把控制器 AimIdle 状态 motion 指向它。
/// 菜单：工具/生成修正待机动画（英文别名 Tools/FixAimIdleRoot）
/// </summary>
public static class FixAimIdleRoot
{
    private const string SrcPath = "Assets/Art/Animations/female_aimIdle.fbx";
    private const string DstPath = "Assets/Art/Animations/Fixed/female_aimIdle_fixed.anim";

    [MenuItem("工具/生成修正待机动画", false, 1104)]
    [MenuItem("Tools/FixAimIdleRoot", false, 1104)]
    public static void Run()
    {
        var sb = new StringBuilder();
        var src = AssetDatabase.LoadAssetAtPath<AnimationClip>(SrcPath);
        if (src == null) { sb.AppendLine($"源动画不存在: {SrcPath}"); Debug.Log(sb.ToString()); return; }

        // 删除旧目标（避免残留），再创建新 clip
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

        // 复制循环设置
        var settings = AnimationUtility.GetAnimationClipSettings(src);
        AnimationUtility.SetAnimationClipSettings(dst, settings);

        int rootCurves = 0;
        foreach (var b in AnimationUtility.GetCurveBindings(src))
        {
            var curve = AnimationUtility.GetEditorCurve(src, b);
            if (curve == null) continue;

            // RootQ 曲线（根骨骼四元数旋转）：消除恒定偏转 → 设为 identity
            bool isRootQ = b.path == "" && (b.propertyName == "RootQ.x" || b.propertyName == "RootQ.y" ||
                                            b.propertyName == "RootQ.z" || b.propertyName == "RootQ.w");
            if (isRootQ)
            {
                var keys = curve.keys;
                for (int i = 0; i < keys.Length; i++)
                {
                    var k = keys[i];
                    k.value = (b.propertyName == "RootQ.w") ? 1f : 0f; // identity 四元数 (0,0,0,1)
                    k.inTangent = 0f;
                    k.outTangent = 0f;
                    keys[i] = k;
                }
                AnimationUtility.SetEditorCurve(dst, b, new AnimationCurve(keys));
                rootCurves++;
                continue;
            }

            // 其余曲线原样复制
            AnimationUtility.SetEditorCurve(dst, b, curve);
        }

        // 写入资产
        AssetDatabase.CreateAsset(dst, DstPath);
        EditorUtility.SetDirty(dst);
        AssetDatabase.SaveAssets();

        // 重新加载验证
        var verify = AssetDatabase.LoadAssetAtPath<AnimationClip>(DstPath);
        sb.AppendLine($"生成完成: {DstPath}");
        sb.AppendLine($"时长={verify.length:F3}s 帧率={verify.frameRate} 循环={verify.isLooping}");
        sb.AppendLine($"RootQ 曲线修正 {rootCurves} 条 → identity（消除根骨骼绕 Y 偏转）");

        var outPath = "Assets/Screenshots/fix_aimidle.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log(sb.ToString());
    }
}

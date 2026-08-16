using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// 修复 female_aimWalk_fixed 的左脚 Twist 坏曲线（值域 -100°~-160°，整体偏移 ~-120°，
/// Mixamo 源数据问题，FixBadCurves 只修尖峰修不掉整体偏移）：
/// 用普通走路 female_Walk_fixed 的同名肌肉曲线（-13°~15°，正常）按时间比例重采样替换。
/// 只改 Fixed/*.anim 资产内容（guid 不变，控制器引用自动生效），输出诊断到 Assets/Screenshots/。
/// 菜单：工具/修复瞄准走路左脚扭曲（英文别名 Tools/FixAimWalkFootTwist）
/// </summary>
public static class FixAimWalkFootTwist
{
    private const string TargetClip = "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim";
    private const string SourceClip = "Assets/Art/Animations/Fixed/female_Walk_fixed.anim";
    // 需要修复的坏曲线（瞄准走路相对普通走路整体偏移异常）：脚踝 Twist + 小腿 Twist
    private static readonly string[] BadCurves = {
        "Left Foot Twist In-Out",
        "Left Lower Leg Twist In-Out",
    };

    [MenuItem("工具/修复瞄准走路左脚扭曲", false, 1090)]
    [MenuItem("Tools/FixAimWalkFootTwist", false, 1090)]
    public static void Run()
    {
        var sb = new StringBuilder();
        var target = AssetDatabase.LoadAssetAtPath<AnimationClip>(TargetClip);
        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceClip);
        if (target == null || source == null)
        {
            Debug.LogError($"[修脚] 加载失败: target={target != null} source={source != null}");
            return;
        }

        foreach (var curveName in BadCurves)
        {
            // 找源曲线（正常）与目标坏曲线
            AnimationCurve srcCurve = null;
            AnimationCurve dstCurve = null;
            foreach (var b in AnimationUtility.GetCurveBindings(source))
            {
                if (b.propertyName == curveName) { srcCurve = AnimationUtility.GetEditorCurve(source, b); break; }
            }
            foreach (var b in AnimationUtility.GetCurveBindings(target))
            {
                if (b.propertyName == curveName) { dstCurve = AnimationUtility.GetEditorCurve(target, b); break; }
            }
            if (srcCurve == null) { sb.AppendLine($"源动画无 {curveName} 曲线"); continue; }
            if (dstCurve == null) { sb.AppendLine($"目标动画无 {curveName} 曲线"); continue; }

            sb.AppendLine($"[修脚] 源(female_Walk_fixed) {curveName}: {srcCurve.length} 帧 值域[{SrcRange(srcCurve)}]");
            sb.AppendLine($"[修脚] 目标(female_aimWalk_fixed) {curveName}: {dstCurve.length} 帧 值域[{SrcRange(dstCurve)}] ← 待修");

            // 按时间比例重采样：目标曲线时长 srcT → dstT，关键帧数量保持目标原有
            float srcLen = source.length;
            float dstLen = target.length;
            var newKeys = new Keyframe[dstCurve.length];
            for (int i = 0; i < dstCurve.length; i++)
            {
                float dstT = dstCurve.keys[i].time;
                float srcT = dstT * srcLen / dstLen; // 归一化时间映射
                float val = srcCurve.Evaluate(srcT);
                var k = dstCurve.keys[i];
                k.value = val;
                // 切线重置为 0，避免折角；相邻采样值已平滑
                k.inTangent = 0f;
                k.outTangent = 0f;
                newKeys[i] = k;
            }
            var fixedCurve = new AnimationCurve(newKeys);

            // 替换曲线
            foreach (var b in AnimationUtility.GetCurveBindings(target))
            {
                if (b.propertyName != curveName) continue;
                AnimationUtility.SetEditorCurve(target, b, fixedCurve);
                break;
            }
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();

            // 重新读验证
            foreach (var b in AnimationUtility.GetCurveBindings(target))
            {
                if (b.propertyName != curveName) continue;
                var after = AnimationUtility.GetEditorCurve(target, b);
                sb.AppendLine($"[修脚] 修复后: {after.length} 帧 值域[{SrcRange(after)}]");
                break;
            }
        }

        var outPath = "Assets/Screenshots/fix_aimwalk_foot.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log(sb.ToString());
    }

    private static string SrcRange(AnimationCurve c)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (var k in c.keys)
        {
            if (k.value < min) min = k.value;
            if (k.value > max) max = k.value;
        }
        return $"{min * Mathf.Rad2Deg:F1}°~{max * Mathf.Rad2Deg:F1}°";
    }
}

using UnityEditor;
using UnityEngine;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 女走路膝盖/腰部诊断 v2：
/// 1. 逐帧输出关键肌肉曲线值（膝盖：Upper/Lower Leg；腰部：Spine/Chest；根位移）
/// 2. 振荡检测：帧间差方向翻转次数（平滑波形翻转少，高频抖动每帧翻转）
/// 3. 对照 man_Walking_fixed 同类曲线判断质量
/// 输出到 Assets/Screenshots/female_walk_diag.txt
/// 菜单：Tools/Diag Female Walk（英文）
/// </summary>
public static class FemaleWalkDiag
{
    private const string ClipPath = "Assets/Art/Animations/Fixed/female_Walk_fixed.anim";
    private const string RefClipPath = "Assets/Art/Animations/Fixed/man_Walking_fixed.anim";

    // 关注曲线：膝盖链 + 腰部 + 根
    private static readonly string[] Targets = {
        "RootT.y", "RootT.z",
        "Left Upper Leg Front-Back", "Right Upper Leg Front-Back",
        "Left Lower Leg Stretch", "Right Lower Leg Stretch",
        "Left Foot Up-Down", "Right Foot Up-Down",
        "Spine Front-Back", "Spine Left-Right", "Spine Twist Left-Right",
        "Chest Front-Back", "Chest Left-Right", "Chest Twist Left-Right",
    };

    [MenuItem("Tools/Diag Female Walk")]
    public static void Run()
    {
        var sb = new StringBuilder();
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        var refClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RefClipPath);
        if (clip == null) { sb.AppendLine("动画不存在: " + ClipPath); }
        else
        {
            sb.AppendLine($"== {clip.name} == 时长={clip.length:F3}s rate={clip.frameRate} 循环={clip.isLooping}");
            sb.AppendLine($"\n【逐帧诊断】每行 = 帧号: 各曲线值（目标=女，参照=男）");
            sb.AppendLine($"帧\t" + string.Join("\t", Targets.Select(TagName)));
            Analyze(clip, refClip, sb);
        }

        Directory.CreateDirectory("Assets/Screenshots");
        File.WriteAllText("Assets/Screenshots/female_walk_diag.txt", sb.ToString());
        Debug.Log("[FemaleWalkDiag] 完成，结果: Assets/Screenshots/female_walk_diag.txt");
    }

    private static string TagName(string t)
    {
        // 女：L/U Leg Stretch → "膝L/膝R"；Spine → "腰前/腰侧/腰转"；Chest → "胸前/胸侧/胸转"
        if (t.Contains("Lower Leg")) return t.Contains("Left") ? "膝L" : "膝R";
        if (t.Contains("Upper Leg")) return t.Contains("Left") ? "大腿L" : "大腿R";
        if (t.Contains("Foot")) return t.Contains("Left") ? "脚L" : "脚R";
        if (t.Contains("Spine Front")) return "腰前后";
        if (t.Contains("Spine Left")) return "腰侧";
        if (t.Contains("Spine Twist")) return "腰转";
        if (t.Contains("Chest Front")) return "胸前后";
        if (t.Contains("Chest Left")) return "胸侧";
        if (t.Contains("Chest Twist")) return "胸转";
        if (t == "RootT.y") return "根Y";
        if (t == "RootT.z") return "根Z";
        return t;
    }

    private static void Analyze(AnimationClip clip, AnimationClip refClip, StringBuilder sb)
    {
        float dt = 1f / clip.frameRate;
        int frames = Mathf.CeilToInt(clip.length * clip.frameRate);

        // 预取目标曲线（女）与参照曲线（男，存在则用）
        var female = Targets.Select(t => GetCurve(clip, t)).ToArray();
        var male = refClip == null ? null : Targets.Select(t => GetCurve(refClip, t)).ToArray();

        // 逐帧输出（每 2 帧一行，控制行数）
        for (int i = 0; i <= frames; i += 1)
        {
            var line = new StringBuilder($"{i:D3}\t");
            for (int j = 0; j < Targets.Length; j++)
            {
                float v = female[j] != null ? female[j].Evaluate(i * dt) : float.NaN;
                line.Append($"{(float.IsNaN(v) ? "-" : v.ToString("F2"))}\t");
            }
            sb.AppendLine(line.ToString());
        }

        // 值域对比（女 vs 男）
        sb.AppendLine($"\n【值域对比】女 vs 男（肌肉值）");
        for (int j = 0; j < Targets.Length; j++)
        {
            var (fmin, fmax) = RangeOf(female[j], frames, dt);
            string refInfo = "";
            if (male != null && male[j] != null)
            {
                var (mmin, mmax) = RangeOf(male[j], frames, dt);
                refInfo = $"  男[{mmin:F2}~{mmax:F2}]";
            }
            string flag = fmax > 1.05f ? " ← 超限(>1.0)!" : "";
            sb.AppendLine($"  {TagName(Targets[j])}: 女[{fmin:F2}~{fmax:F2}]{refInfo}{flag}");
        }

        // 振荡统计：帧间差方向翻转次数（女/男对照）
        sb.AppendLine($"\n【振荡统计】帧间差符号翻转次数（平滑≈0~2；高频抖动≈帧数一半）");
        for (int j = 0; j < Targets.Length; j++)
        {
            int fFlip = Oscillations(female[j], frames, dt);
            int mFlip = male != null && male[j] != null ? Oscillations(male[j], frames, dt) : -1;
            string refInfo = mFlip >= 0 ? $"  [男对照: {mFlip} 次]" : "";
            string verdict = fFlip > frames / 3 ? " ← 高频抖动!" : "";
            sb.AppendLine($"  {TagName(Targets[j])} ({Targets[j]}): 翻转 {fFlip} 次{refInfo}{verdict}");
        }
    }

    /// <summary>计算曲线帧间差值方向翻转次数（去死区 0.005）</summary>
    private static int Oscillations(AnimationCurve curve, int frames, float dt)
    {
        if (curve == null || curve.length == 0) return -1;
        float prev = curve.Evaluate(0);
        int dir = 0, flips = 0;
        for (int i = 1; i <= frames; i++)
        {
            float v = curve.Evaluate(i * dt);
            float d = v - prev;
            if (Mathf.Abs(d) < 0.005f) { prev = v; continue; }
            int ndir = d > 0 ? 1 : -1;
            if (dir != 0 && ndir != dir) flips++;
            dir = ndir;
            prev = v;
        }
        return flips;
    }

    private static (float, float) RangeOf(AnimationCurve curve, int frames, float dt)
    {
        if (curve == null || curve.length == 0) return (float.NaN, float.NaN);
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i <= frames; i++)
        {
            float v = curve.Evaluate(i * dt);
            if (v < min) min = v;
            if (v > max) max = v;
        }
        return (min, max);
    }

    private static AnimationCurve GetCurve(AnimationClip clip, string muscle)
    {
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.propertyName == muscle)
                return AnimationUtility.GetEditorCurve(clip, b);
        }
        return null;
    }
}

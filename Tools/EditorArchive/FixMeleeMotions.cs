using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 修复 MeleeAnimator：Unity 解析磁盘 YAML 引用失败（motion 全 null）时的强制重赋值
/// 做法：解析 YAML 文本拿"状态名 → 引用 guid"，API 遍历状态重新 LoadAssetAtPath + 赋值
/// （与 RangedAnimator 重建同路径，已验证可序列化出正确引用）
/// 菜单：Tools/Fix Melee Motions（英文）
/// </summary>
public static class FixMeleeMotions
{
    const string CtrlPath = "Assets/Art/Animators/MeleeAnimator.controller";

    [MenuItem("Tools/Fix Melee Motions")]
    public static void Run()
    {
        var sb = new StringBuilder();

        // 1. 强制重导入控制器（清除可能存在的缓存）
        AssetDatabase.ImportAsset(CtrlPath, ImportAssetOptions.ForceUpdate);

        // 2. 解析 YAML 文本：AnimatorState 块 → m_Motion 引用 guid
        var guidMap = new Dictionary<string, string>(); // 状态名 -> 引用 guid
        string text = File.ReadAllText(CtrlPath.Replace('/', Path.DirectorySeparatorChar));
        var blocks = Regex.Split(text, "(?=--- !u!)");
        foreach (var b in blocks)
        {
            var typeM = Regex.Match(b, @"--- !u!(\d+)");
            if (typeM.Groups[1].Value != "1102") continue; // 只处理 AnimatorState
            var nameM = Regex.Match(b, @"m_Name: (\S+)");
            var motionM = Regex.Match(b, @"m_Motion: \{fileID: 7400000, guid: ([0-9a-f]+), type: 2\}");
            if (!nameM.Success || !motionM.Success) continue;
            guidMap[nameM.Groups[1].Value] = motionM.Groups[1].Value;
        }
        sb.AppendLine("YAML 解析: 找到 " + guidMap.Count + " 个 7400000 引用（状态名 → guid）");
        foreach (var kv in guidMap)
        {
            string path = AssetDatabase.GUIDToAssetPath(kv.Value);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null)
                sb.AppendLine($"  {kv.Key} -> {Path.GetFileName(path)} ✓");
            else
                sb.AppendLine($"  {kv.Key} -> guid {kv.Value} 解析失败 ✗");
        }

        // 3. API 遍历状态：motion 为 null 且 YAML 有引用 → 重新赋值
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
        if (ctrl == null) { sb.AppendLine("控制器加载失败"); }
        else
        {
            int fixedCount = 0;
            foreach (var layer in ctrl.layers)
                fixedCount += FixStateMachine(layer.stateMachine, guidMap, sb);
            if (fixedCount > 0)
            {
                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                sb.AppendLine("重新赋值 " + fixedCount + " 处 motion 并保存");
            }
        }

        var outPath = "Assets/Screenshots/fix_melee_motions.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[FixMelee] 完成: " + outPath);
    }

    private static int FixStateMachine(AnimatorStateMachine sm, Dictionary<string, string> guidMap, StringBuilder sb)
    {
        int fixedCount = 0;
        if (sm == null) return 0;
        foreach (var st in sm.states)
        {
            if (st.state.motion != null) continue;
            if (st.state.name == "Empty") continue;
            if (!guidMap.TryGetValue(st.state.name, out var guid)) continue;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;
            st.state.motion = clip;
            fixedCount++;
            sb.AppendLine("  赋值 " + st.state.name + " <- " + Path.GetFileName(path));
        }
        foreach (var child in sm.stateMachines)
            fixedCount += FixStateMachine(child.stateMachine, guidMap, sb);
        return fixedCount;
    }
}

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 诊断走路动画的朝向偏移：对比动画首帧 Hips 旋转与模型 T-Pose 的 Hips 旋转，
/// 找出「脚朝左前方走」的固定偏转角度（直走偏、斜走正常的根源）
/// 菜单「工具/诊断走路朝向」
/// </summary>
public static class WalkHeadingDiag
{
    [MenuItem("工具/诊断走路朝向")]
    public static void Run()
    {
        var sb = new StringBuilder();
        string outPath = "D:/Project/unity/interview/Assets/Screenshots/walk_heading.txt";
        try
        {
            // 1. 读取各动画首帧 Hips/根骨骼的局部旋转
            string[] anims = {
                "Assets/Art/Animations/man_Walking.fbx",
                "Assets/Art/Animations/man_Run.fbx",
                "Assets/Art/Animations/man_Idle.fbx",
                "Assets/Art/Animations/female_Walk.fbx",
                "Assets/Art/Animations/female_Run.fbx"
            };
            foreach (var path in anims)
            {
                var objs = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var o in objs)
                {
                    if (!(o is AnimationClip clip)) continue;
                    if (clip.name.StartsWith("__")) continue;
                    sb.AppendLine($"===== 动画: {clip.name} ({path}) =====");
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    // 按骨骼路径分组收集旋转曲线
                    var rotGroups = new Dictionary<string, Dictionary<string, AnimationCurve>>();
                    foreach (var b in bindings)
                    {
                        if (!b.propertyName.StartsWith("m_LocalRotation")) continue;
                        // 只关心根/Hips 骨骼（root motion 骨骼）
                        if (b.path != "" && !b.path.Contains("Hips")) continue;
                        if (!rotGroups.ContainsKey(b.path))
                            rotGroups[b.path] = new Dictionary<string, AnimationCurve>();
                        rotGroups[b.path][b.propertyName] = AnimationUtility.GetEditorCurve(clip, b);
                    }
                    if (rotGroups.Count == 0)
                    {
                        sb.AppendLine("  (无 Hips/根旋转曲线)");
                    }
                    foreach (var kv in rotGroups)
                    {
                        sb.AppendLine($"  骨骼: '{kv.Key}'");
                        float x = 0, y = 0, z = 0, w = 1;
                        foreach (var c in kv.Value)
                        {
                            var curve = c.Value;
                            if (curve == null || curve.keys.Length == 0) continue;
                            float v = curve.keys[0].value;
                            if (c.Key.EndsWith(".x")) x = v;
                            else if (c.Key.EndsWith(".y")) y = v;
                            else if (c.Key.EndsWith(".z")) z = v;
                            else if (c.Key.EndsWith(".w")) w = v;
                        }
                        var q = new Quaternion(x, y, z, w);
                        sb.AppendLine($"    首帧 localRotation → euler y = {q.eulerAngles.y:F2}°");
                    }
                }
            }

            // 2. 模型 T-Pose 的 Hips 局部旋转
            CheckModel("Assets/Art/Models/man.fbx", sb);
            CheckModel("Assets/Art/Models/Female.fbx", sb);
            CheckModel("Assets/Art/Models/female.fbx", sb);
        }
        catch (System.Exception e)
        {
            sb.AppendLine("异常: " + e);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[走路朝向] " + sb.ToString());
    }

    static void CheckModel(string path, StringBuilder sb)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) { sb.AppendLine($"模型不存在: {path}"); return; }
        var inst = (GameObject)Object.Instantiate(go);
        try
        {
            var hips = inst.transform.Find("mixamorig:Hips");
            if (hips != null)
            {
                sb.AppendLine($"模型 {path}: mixamorig:Hips T-Pose 局部旋转 y = {hips.localEulerAngles.y:F2}°");
            }
            else
            {
                sb.AppendLine($"模型 {path}: 未找到 mixamorig:Hips，根 '{inst.transform.name}' 子节点: {string.Join(", ", ChildNames(inst.transform))}");
            }
        }
        finally
        {
            Object.DestroyImmediate(inst);
        }
    }

    static string[] ChildNames(Transform t)
    {
        var list = new List<string>();
        foreach (Transform c in t) list.Add(c.name);
        return list.ToArray();
    }
}

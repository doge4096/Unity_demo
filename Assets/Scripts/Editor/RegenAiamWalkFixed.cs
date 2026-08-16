using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// 原地重建 Fixed 目录全部固定动画（不删除资产 → 不破坏控制器引用，跑完不用重启）
/// 自动扫描 Fixed/*_fixed.anim，找同名 fbx 重建；自动设置"根变换旋转依据 = 原始"
/// 菜单：工具/重建全部固定动画
/// </summary>
public static class RegenAllFixed
{
    private const string FixedDir = "Assets/Art/Animations/Fixed";
    private const string AnimDir = "Assets/Art/Animations";

    [MenuItem("工具/重建全部固定动画", false, 1130)]
    [MenuItem("Tools/RegenAllFixed", false, 1130)]
    public static void Run()
    {
        int ok = 0, fail = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { FixedDir }))
        {
            var dst = AssetDatabase.GUIDToAssetPath(guid);
            if (!dst.EndsWith("_fixed.anim")) continue;

            var name = Path.GetFileNameWithoutExtension(dst);
            var fbxName = name.Substring(0, name.Length - "_fixed".Length);
            var fbxPath = AnimDir + "/" + fbxName + ".fbx";

            var srcClip = GetClip(fbxPath);
            if (srcClip == null) { Debug.LogWarning($"[重建] 找不到 fbx: {fbxPath}（跳过 {dst}）"); fail++; continue; }

            // 原地修改：加载现有资产，不动引用
            var dstClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(dst);
            if (dstClip == null)
            {
                dstClip = new AnimationClip();
                dstClip.legacy = false;
                AssetDatabase.CreateAsset(dstClip, dst);
            }

            dstClip.frameRate = srcClip.frameRate;
            dstClip.wrapMode = srcClip.wrapMode;

            // 清空旧曲线
            foreach (var b in AnimationUtility.GetCurveBindings(dstClip))
                AnimationUtility.SetEditorCurve(dstClip, b, null);

            // 复制新曲线
            foreach (var b in AnimationUtility.GetCurveBindings(srcClip))
            {
                var curve = AnimationUtility.GetEditorCurve(srcClip, b);
                if (curve != null) AnimationUtility.SetEditorCurve(dstClip, b, curve);
            }

            // 根变换旋转依据 = 原始
            var settings = AnimationUtility.GetAnimationClipSettings(srcClip);
            settings.keepOriginalOrientation = true;
            AnimationUtility.SetAnimationClipSettings(dstClip, settings);

            EditorUtility.SetDirty(dstClip);
            Debug.Log($"[重建] {dst} ← {fbxName}");
            ok++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[重建] 完成：成功 {ok} 个，失败 {fail} 个");
    }

    private static AnimationClip GetClip(string fbxPath)
    {
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (obj is AnimationClip c) return c;
        return null;
    }
}

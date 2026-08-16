using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 修复 RangedAnimator 层权重配置错误：
/// 上次重建控制器时 Base 层 defaultWeight=0、UpperBody(射击)层=1，权重反了
/// → 运行时 Base 层（Idle/Walk/Run）动画权重为 0 不可见，被射击层整身覆盖混合 → 骨骼甩动
/// 菜单：Tools/Fix Ranged Layer Weight
/// </summary>
public static class FixRangedAnimatorLayers
{
    [MenuItem("Tools/Fix Ranged Layer Weight")]
    public static void Fix()
    {
        const string path = "Assets/Art/Animators/RangedAnimator.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null)
        {
            Debug.LogError("[FixLayers] 控制器加载失败: " + path);
            return;
        }

        var layers = ctrl.layers;
        if (layers.Length != 2)
        {
            Debug.LogWarning($"[FixLayers] 层数不是 2（当前 {layers.Length}），跳过");
            return;
        }

        // Base 层权重固定 1，UpperBody(射击)层默认 0（触发时由代码/过渡切换）
        layers[0].defaultWeight = 1f;
        layers[1].defaultWeight = 0f;
        ctrl.layers = layers;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        Debug.Log($"[FixLayers] 已修复层权重：Base=1, UpperBody=0（原为 Base=0, UpperBody=1）");
    }
}

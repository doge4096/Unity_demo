using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 给 AimWalk 2D 混合树添加中心节点 (0,0) = female_aimWalk_fixed（前向动画）：
/// SimpleDirectional 混合树没有中心节点时，输入 (0,0) 会 4 方向各 25% 混合 → 姿态怪异/斜向走。
/// 加中心节点后：原点输入输出前向走路（正常），斜向输入仍按方向混合。
/// 处理 FemaleAnimator 的 AimWalk 和 RangedAnimator 的 AimMoveBlend。幂等。
/// 菜单：工具/修复混合树中心节点（英文别名 Tools/FixBlendCenter）
/// </summary>
public static class FixBlendCenter
{
    private const string FrontClipPath = "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim";

    [MenuItem("工具/修复混合树中心节点", false, 1110)]
    [MenuItem("Tools/FixBlendCenter", false, 1110)]
    public static void Run()
    {
        var frontClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(FrontClipPath);
        if (frontClip == null) { Debug.LogError("[混合中心] 找不到前向动画: " + FrontClipPath); return; }

        FixController("Assets/Art/Animators/FemaleAnimator.controller", "AimWalk", frontClip);
        FixController("Assets/Art/Animators/RangedAnimator.controller", "AimMoveBlend", frontClip);
        Debug.Log("[混合中心] 完成");
    }

    private static void FixController(string ctrlPath, string blendTreeName, AnimationClip frontClip)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
        if (ctrl == null) { Debug.LogWarning($"[混合中心] 控制器不存在: {ctrlPath}"); return; }

        int fixedTrees = 0;
        foreach (var layer in ctrl.layers)
            fixedTrees += FixInSM(layer.stateMachine, blendTreeName, frontClip);

        if (fixedTrees > 0)
        {
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log($"[混合中心] {ctrlPath}: 修复 {fixedTrees} 个 {blendTreeName} 混合树（添加中心节点）");
        }
        else
        {
            Debug.Log($"[混合中心] {ctrlPath}: 无 {blendTreeName} 或已是中心节点（幂等）");
        }
    }

    private static int FixInSM(AnimatorStateMachine sm, string name, AnimationClip frontClip)
    {
        int n = 0;
        if (sm == null) return 0;
        foreach (var st in sm.states)
        {
            if (st.state.motion is BlendTree bt)
            {
                if (bt.name == name && bt.blendType == BlendTreeType.SimpleDirectional2D)
                {
                    if (HasCenter(bt)) continue;
                    var children = bt.children;
                    // 追加中心节点 (0,0) = 前向走路
                    var newList = new System.Collections.Generic.List<ChildMotion>(children);
                    newList.Add(new ChildMotion
                    {
                        motion = frontClip,
                        position = Vector2.zero,
                        timeScale = 1f,
                    });
                    bt.children = newList.ToArray();
                    n++;
                    Debug.Log($"[混合中心]   {bt.name}: 添加中心节点 (0,0) = {frontClip.name}");
                }
                else if (bt.name == name && bt.blendType == BlendTreeType.Simple1D)
                {
                    // 忽略 1D 混合树
                }
            }
        }
        foreach (var child in sm.stateMachines)
            n += FixInSM(child.stateMachine, name, frontClip);
        return n;
    }

    private static bool HasCenter(BlendTree bt)
    {
        foreach (var c in bt.children)
        {
            if (c.position.magnitude < 0.01f) return true;
        }
        return false;
    }
}

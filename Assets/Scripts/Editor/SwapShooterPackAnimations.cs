// 走路换持枪跑步动画 + 修复瞄准走路混合树
// 菜单：工具/走路换持枪跑步并修复瞄准走路
// 功能：
//   1. Walk 状态 → female_aimRun_fixed（AimRun 状态正在用的持枪跑步动画，走路靠 walkPlaybackSpeed 降速播放）
//   2. 重建 AimWalk 状态的 2D 混合树（中心前向 + 前后左右四方向，AimX/AimZ 驱动）——
//      修复"瞄准走路往左右后动画不播放"（混合树丢失导致状态 motion 为空）
// 幂等：混合树已存在时跳过重建，可重复运行
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SwapShooterPackAnimations
{
    const string ControllerPath = "Assets/Art/Animators/FemaleAnimator.controller";
    // 走路动画：AimRun 状态正在用的持枪跑步动画（降速播当走路）
    const string WalkClipPath = "Assets/Art/Animations/Fixed/female_aimRun_fixed.anim";
    // 瞄准走路四方向 + 中心（AimWalk 混合树用）
    const string AimWalkF = "Assets/Art/Animations/Fixed/female_aimWalk_fixed.anim";
    const string AimWalkR = "Assets/Art/Animations/Fixed/female_aimWalkRight_fixed.anim";
    const string AimWalkB = "Assets/Art/Animations/Fixed/female_aimWalkBack_fixed.anim";
    const string AimWalkL = "Assets/Art/Animations/Fixed/female_aimWalkLeft_fixed.anim";

    [MenuItem("工具/走路换持枪跑步并修复瞄准走路")]
    public static void SwapWalkToRun()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"替换失败：找不到控制器 {ControllerPath}");
            return;
        }

        var walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClipPath);
        if (walkClip == null)
        {
            Debug.LogError($"替换失败：找不到走路动画 {WalkClipPath}");
            return;
        }

        int swapped = 0;      // Walk 状态替换数
        bool btOk = false;    // AimWalk 混合树是否已就绪（原样保留或本次重建）
        foreach (var layer in controller.layers)
        {
            foreach (var child in layer.stateMachine.states)
            {
                var s = child.state;
                if (s.name == "Walk")
                {
                    s.motion = walkClip;
                    swapped++;
                }
                else if (s.name == "AimWalk")
                {
                    // 已有混合树（motion 是 BlendTree）→ 不动；motion 为空 → 重建
                    if (s.motion is BlendTree) btOk = true;
                    else if (TryRebuildAimWalkTree(controller, s)) btOk = true;
                }
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"替换完成：Walk→{walkClip.name}（{swapped} 个状态）；AimWalk 混合树{(btOk ? "已就绪" : "重建失败！请检查四方向动画是否存在")}");
    }

    // 重建 AimWalk 2D 混合树：中心前向 + 前/右/后/左（参数 AimX/AimZ）
    static bool TryRebuildAimWalkTree(AnimatorController controller, AnimatorState state)
    {
        var f = AssetDatabase.LoadAssetAtPath<AnimationClip>(AimWalkF);
        var r = AssetDatabase.LoadAssetAtPath<AnimationClip>(AimWalkR);
        var b = AssetDatabase.LoadAssetAtPath<AnimationClip>(AimWalkB);
        var l = AssetDatabase.LoadAssetAtPath<AnimationClip>(AimWalkL);
        if (f == null || r == null || b == null || l == null)
        {
            Debug.LogError($"重建 AimWalk 混合树失败：方向动画缺失（f={f != null} r={r != null} b={b != null} l={l != null}）");
            return false;
        }

        var bt = new BlendTree
        {
            name = "AimWalk",
            blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = "AimX",
            blendParameterY = "AimZ",
            useAutomaticThresholds = false
        };
        AssetDatabase.AddObjectToAsset(bt, controller); // new 出来的 BlendTree 必须注册进控制器资产
        bt.AddChild(f, new Vector2(0f, 1f));   // 前
        bt.AddChild(r, new Vector2(1f, 0f));   // 右
        bt.AddChild(b, new Vector2(0f, -1f));  // 后
        bt.AddChild(l, new Vector2(-1f, 0f));  // 左
        bt.AddChild(f, Vector2.zero);          // 中心=前向（FixBlendCenter 做法：原点输入不混合成 4 方向 25%）
        state.motion = bt;
        return true;
    }
}

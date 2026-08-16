using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// 触地高度采样：播放走路/跑步动画，记录脚趾骨骼世界 Y 的最低值（触地深度）
/// 负值 = 穿地（脚插进地面），正值 = 悬空（脚离地走路）
/// 用于修正 CharacterController center.y，让胶囊体底部与真实脚底对齐
/// 菜单：Tools/Sample Foot Touch（英文）
/// </summary>
public static class FootTouchSampler
{
    [MenuItem("Tools/Sample Foot Touch")]
    public static void Run()
    {
        var sb = new StringBuilder();
        Sample("男: man_Walking", "Assets/Art/Models/man.fbx",
            "Assets/Art/Animations/Fixed/man_Walking_fixed.anim", "mixamorig:", sb);
        Sample("男: man_Run", "Assets/Art/Models/man.fbx",
            "Assets/Art/Animations/Fixed/man_Run_fixed.anim", "mixamorig:", sb);
        Sample("女: man_Run(已换)", "Assets/Art/Models/Female.fbx",
            "Assets/Art/Animations/Fixed/man_Run_fixed.anim", "mixamorig1:", sb);
        Sample("女: female_Walk(旧)", "Assets/Art/Models/Female.fbx",
            "Assets/Art/Animations/Fixed/female_Walk_fixed.anim", "mixamorig1:", sb);

        var outPath = "Assets/Screenshots/foot_touch.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[FootTouch] 完成，结果: " + outPath);
    }

    private static void Sample(string label, string modelPath, string clipPath, string prefix, StringBuilder sb)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (model == null || clip == null) { sb.AppendLine($"{label}: 加载失败"); return; }

        // 临时控制器
        string tmpCtrl = "Assets/Screenshots/_tmp_foot.controller";
        if (File.Exists(tmpCtrl)) AssetDatabase.DeleteAsset(tmpCtrl);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(tmpCtrl);
        ctrl.layers[0].stateMachine.AddState("Play").motion = clip;

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        inst.transform.position = Vector3.zero; // root Y=0：mixamo 模型根在脚底基准，脚趾最低点直接=触地深度
        var anim = inst.GetComponent<Animator>();
        if (anim == null) anim = inst.AddComponent<Animator>();
        var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(modelPath);
        if (avatar != null && avatar.isHuman) anim.avatar = avatar;
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // 找脚趾/脚骨骼
        var toes = new List<Transform>();
        foreach (var t in inst.GetComponentsInChildren<Transform>())
            if (t.name == prefix + "LeftToeBase" || t.name == prefix + "RightToeBase")
                toes.Add(t);
        if (toes.Count == 0) { sb.AppendLine($"{label}: 找不到脚趾骨骼"); Object.DestroyImmediate(inst); return; }

        int steps = 48;
        float minY = float.MaxValue, minTime = 0f;
        float minY2 = float.MaxValue;
        for (int i = 0; i <= steps; i++)
        {
            float nt = i / (float)steps;
            anim.Play("Play", 0, Mathf.Repeat(nt, 1f));
            anim.Update(0.05f);
            float y0 = toes[0].position.y;
            float y1 = toes.Count > 1 ? toes[1].position.y : y0;
            if (y0 < minY) { minY = y0; minTime = nt; }
            if (y1 < minY2) minY2 = y1;
        }
        sb.AppendLine($"\n{label}: 脚趾最低 Y = {minY:F3}（@t={minTime:P0}） / {minY2:F3}（另一脚）");
        sb.AppendLine($"  地面 Y=0：{(minY < 0f ? $"穿地 {(-minY) * 100:F0}cm" : $"悬空 {minY * 100:F0}cm")}");

        Object.DestroyImmediate(inst);
        AssetDatabase.DeleteAsset(tmpCtrl);
    }
}

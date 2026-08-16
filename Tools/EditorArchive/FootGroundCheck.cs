using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.IO;

/// <summary>
/// 地面触地检查 v3：
/// 1. 场景对象采样（真实控制器，含速度参数污染风险）
/// 2. 模型实例对照采样（骨架根放 Y=0，临时控制器直接播 clip，无污染）
/// 对比两组结果定位差异来源（场景状态配置 vs 动画本身）
/// 输出：场景 GO 高度 / 骨架根静态偏移 / 状态 speed 配置 / 两组触地深度
/// 菜单：Tools/Check Ground Touch（英文）
/// </summary>
public static class FootGroundCheck
{
    private const string MaleModel = "Assets/Art/Models/man.fbx";
    private const string FemaleModel = "Assets/Art/Models/Female.fbx";

    [MenuItem("Tools/Check Ground Touch")]
    public static void Run()
    {
        var sb = new StringBuilder();
        foreach (var go in Object.FindObjectsOfType<GameObject>(true))
        {
            if (go.name == "MeleePlayer") CheckScene(go, MaleModel, sb);
            else if (go.name == "RangedPlayer") CheckScene(go, FemaleModel, sb);
        }
        var outPath = "Assets/Screenshots/ground_touch.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[GroundTouch] 完成，结果: " + outPath);
    }

    // ---- 场景采样 ----
    private static void CheckScene(GameObject go, string modelPath, StringBuilder sb)
    {
        // 临时激活 inactive 父链（否则 Animator 不更新），采样后恢复
        var activated = ForceActiveChain(go);
        var anim = go.GetComponentInChildren<Animator>(true);
        sb.AppendLine($"== {go.name} ==  active={go.activeInHierarchy} GO={go.transform.position.y:F3}");
        if (anim == null)
        {
            sb.AppendLine("  无 Animator");
            foreach (var g in activated) g.SetActive(false);
            return;
        }

        var ctrl = anim.runtimeAnimatorController as AnimatorController;
        sb.AppendLine($"  控制器: {(ctrl != null ? ctrl.name : "null")}  avatar={(anim.avatar != null ? anim.avatar.name : "NULL!")} humanoid={anim.isHuman}");
        sb.AppendLine($"  Animator组件: on={anim.gameObject.name} enabled={anim.enabled} activeHierarchy={anim.gameObject.activeInHierarchy} applyRootMotion={anim.applyRootMotion}");
        sb.AppendLine($"  父链: {ParentChain(anim.transform)}");

        string[] states = { "Walk", "Run" };
        foreach (var s in states)
        {
            int unusedLayer;
            var st = FindStateByName(ctrl, s, out unusedLayer);
            string speedInfo = "?";
            if (st != null)
                speedInfo = $"speed={st.speed}{(st.speedParameterActive ? $"+param:{st.speedParameter}" : "")}";
            var (minY, maxY, clipName) = SampleSceneState(go, anim, s, ctrl);
            if (minY == float.MaxValue) { sb.AppendLine($"  [{s}] 状态未找到"); continue; }
            string verdict = minY < -0.005f ? $"穿地 {(-minY) * 100:F0}cm"
                : minY > 0.02f ? $"悬空 {minY * 100:F0}cm" : "贴地 ✓";
            sb.AppendLine($"  [场景:{s}] clip={clipName} {speedInfo} 脚趾最低={minY:F3} 最高={maxY:F3} → {verdict}");
        }

        // 模型实例对照
        sb.AppendLine($"  -- 模型实例对照（骨架根=0，纯动画）--");
        string[] clips = {
            "Assets/Art/Animations/Fixed/man_Walking_fixed.anim",
            "Assets/Art/Animations/Fixed/man_Run_fixed.anim",
            "Assets/Art/Animations/Fixed/female_Walk_fixed.anim",
            "Assets/Art/Animations/Fixed/man_Run_fixed.anim"
        };
        bool isMale = go.name == "MeleePlayer";
        int cIdx = isMale ? 0 : 2;
        for (int i = 0; i < 2; i++)
        {
            string clipPath = clips[cIdx + i];
            var (minY, maxY) = SampleInstance(modelPath, clipPath);
            string label = Path.GetFileNameWithoutExtension(clipPath);
            string verdict = minY < -0.005f ? $"穿地 {(-minY) * 100:F0}cm"
                : minY > 0.02f ? $"悬空 {minY * 100:F0}cm" : "贴地 ✓";
            sb.AppendLine($"  [实例:{label}] 骨架根=0 脚趾最低={minY:F3} 最高={maxY:F3} → {verdict}");
        }

        // 场景临时控制器采样（绕过场景控制器层/权重问题，直接验证骨骼贴地）
        sb.AppendLine($"  -- 场景临时控制器（绕过层/权重）--");
        foreach (string clipPath in new[] {
            "Assets/Art/Animations/Fixed/female_Walk_fixed.anim",
            "Assets/Art/Animations/Fixed/man_Run_fixed.anim" })
        {
            var (minY, maxY) = SampleSceneWithTempCtrl(anim, clipPath);
            string label = Path.GetFileNameWithoutExtension(clipPath);
            string verdict = minY < -0.005f ? $"穿地 {(-minY) * 100:F0}cm"
                : minY > 0.02f ? $"悬空 {minY * 100:F0}cm" : "贴地 ✓";
            sb.AppendLine($"  [场景临时:{label}] 脚趾最低={minY:F3} 最高={maxY:F3} → {verdict}");
        }

        // 恢复原激活状态
        foreach (var g in activated) g.SetActive(false);
    }

    /// <summary>临时换测试控制器在场景对象骨骼上采样（绕过原控制器的层/权重问题），采样后恢复原配置</summary>
    private static (float, float) SampleSceneWithTempCtrl(Animator anim, string clipPath)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) return (float.MaxValue, float.MinValue);

        var savedCtrl = anim.runtimeAnimatorController;
        var savedAvatar = anim.avatar;
        bool savedRM = anim.applyRootMotion;
        anim.applyRootMotion = false;

        string tmp = "Assets/Screenshots/_tmp_foot2.controller";
        if (File.Exists(tmp)) AssetDatabase.DeleteAsset(tmp);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(tmp);
        ctrl.layers[0].stateMachine.AddState("Play").motion = clip;
        anim.runtimeAnimatorController = ctrl;
        anim.Rebind();

        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i <= 48; i++)
        {
            anim.Play("Play", 0, i / 48f);
            anim.Update(0.05f);
            float y = MinToeY(anim.gameObject);
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        anim.runtimeAnimatorController = savedCtrl;
        anim.avatar = savedAvatar;
        anim.applyRootMotion = savedRM;
        anim.Rebind();
        AssetDatabase.DeleteAsset(tmp);
        return (minY, maxY);
    }

    private static string ParentChain(Transform t)
    {
        var sb = new StringBuilder();
        var p = t;
        while (p != null)
        {
            sb.Insert(0, $"{p.name}({(p.gameObject.activeSelf ? "on" : "off")}) <- ");
            p = p.parent;
        }
        return sb.ToString().TrimEnd(' ', '-', '<');
    }

    /// <summary>把目标到根的 inactive 祖先链临时激活，返回被激活的对象（采样后需恢复）</summary>
    private static List<GameObject> ForceActiveChain(GameObject go)
    {
        var changed = new List<GameObject>();
        if (go.activeInHierarchy) return changed;
        var chain = new List<Transform>();
        var p = go.transform;
        while (p != null) { chain.Insert(0, p); p = p.parent; }
        foreach (var t in chain)
        {
            if (!t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(true);
                changed.Add(t.gameObject);
            }
        }
        return changed;
    }

    // ---- 场景状态采样（固定速度参数避免污染；按状态实际所在层播放）----
    private static (float, float, string) SampleSceneState(GameObject go, Animator anim, string stateName, AnimatorController ctrl)
    {
        var saved = new Dictionary<string, float>();
        if (anim.parameters != null)
            foreach (var p in anim.parameters)
            {
                if (p.type != AnimatorControllerParameterType.Float) continue;
                saved[p.name] = anim.GetFloat(p.name);
                // 倍速类参数固定为 1，避免采样点偏移（Speed 本身不动：已直接 Play 目标状态）
                if (p.name.ToLower().Contains("speed") && p.name != "Speed")
                    anim.SetFloat(p.name, 1f);
            }
        float oldSpeed = anim.speed;
        anim.speed = 1f;

        anim.Rebind(); // 对象曾 inactive 时 Animator 可能未初始化，先重绑
        anim.Update(0.01f);

        string clipName = null;
        int layer = 0;
        var st = FindStateByName(ctrl, stateName, out layer);
        if (st != null)
        {
            var m = st.motion;
            if (m is AnimationClip c) clipName = c.name;
            else if (m is BlendTree) clipName = "BlendTree";
        }

        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i <= 48; i++)
        {
            anim.Play(stateName, layer, i / 48f);
            anim.Update(0.05f);
            float y = MinToeY(go);
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        anim.speed = oldSpeed;
        foreach (var kv in saved) anim.SetFloat(kv.Key, kv.Value);
        return (minY, maxY, clipName);
    }

    // ---- 模型实例采样（纯动画，无污染）----
    private static (float, float) SampleInstance(string modelPath, string clipPath)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (model == null || clip == null) return (float.MaxValue, float.MinValue);

        string tmpCtrl = "Assets/Screenshots/_tmp_foot.controller";
        if (File.Exists(tmpCtrl)) AssetDatabase.DeleteAsset(tmpCtrl);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(tmpCtrl);
        ctrl.layers[0].stateMachine.AddState("Play").motion = clip;

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        inst.transform.position = Vector3.zero;
        var anim = inst.GetComponent<Animator>();
        if (anim == null) anim = inst.AddComponent<Animator>();
        var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(modelPath);
        if (avatar != null && avatar.isHuman) anim.avatar = avatar;
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        float minY = float.MaxValue, maxY = float.MinValue;
        float rootBefore = anim.transform.position.y;
        float rootMaxMove = 0f;
        for (int i = 0; i <= 48; i++)
        {
            anim.Play("Play", 0, i / 48f);
            anim.Update(0.05f);
            float rootMove = Mathf.Abs(anim.transform.position.y - rootBefore);
            if (rootMove > rootMaxMove) rootMaxMove = rootMove;
            float y = MinToeY(inst);
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        Object.DestroyImmediate(inst);
        AssetDatabase.DeleteAsset(tmpCtrl);
        Debug.Log($"[GroundTouch] 实例 {Path.GetFileNameWithoutExtension(clipPath)}: 动画中骨架根最大移动={rootMaxMove:F3}m");
        return (minY, maxY);
    }

    private static AnimatorState FindStateByName(AnimatorController ctrl, string name, out int layerIndex)
    {
        layerIndex = 0;
        if (ctrl == null) return null;
        for (int i = 0; i < ctrl.layers.Length; i++)
        {
            var st = FindInSM(ctrl.layers[i].stateMachine, name);
            if (st != null) { layerIndex = i; return st; }
        }
        return null;
    }

    private static AnimatorState FindInSM(AnimatorStateMachine sm, string name)
    {
        if (sm == null) return null;
        foreach (var st in sm.states)
            if (st.state.name == name) return st.state;
        foreach (var child in sm.stateMachines)
        {
            var s = FindInSM(child.stateMachine, name);
            if (s != null) return s;
        }
        return null;
    }

    private static float MinToeY(GameObject go)
    {
        float min = float.MaxValue;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.EndsWith("ToeBase") ||
                (t.name.Contains("Foot") && !t.name.Contains("Footstep")))
            {
                if (t.position.y < min) min = t.position.y;
            }
        }
        return min;
    }
}

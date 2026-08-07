using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

/// <summary>
/// 最小 controller 验证实验：用 Unity API 从零创建 controller（只有 Idle+Run），
/// Play 模式运行时切换给 man 的 Animator，采样骨骼判断动画是否驱动
/// 菜单「工具/最小控制器测试」
/// </summary>
public static class MinControllerTest
{
    [MenuItem("工具/最小控制器测试")]
    [MenuItem("Tools/Min Controller Test")]
    public static void Run()
    {
        var sb = new System.Text.StringBuilder();
        try
        {
            // 1. 创建最小 controller 资产
            string path = "Assets/Art/Animators/MeleeAnimatorRebuilt.controller";
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            // 2. 加参数
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            // 3. 层0：Idle + Run
            var root = ctrl.layers[0].stateMachine;
            var idle = root.AddState("Idle");
            var run = root.AddState("Run");
            idle.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/man_Idle.fbx");
            run.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/man_Run.fbx");
            // 4. 过渡：Idle -> Run (Speed > 0.1)
            var t = idle.AddTransition(run);
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t.duration = 0.1f;
            root.defaultState = idle;
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            sb.AppendLine("最小 controller 已创建: " + path);

            // 5. 运行时切换（Play 模式）并采样
            if (Application.isPlaying)
            {
                var anim = default(Animator);
                foreach (var a in Resources.FindObjectsOfTypeAll<Animator>())
                {
                    if (a.gameObject.name == "man" && a.gameObject.scene.IsValid())
                    { anim = a; break; }
                }
                if (anim != null)
                {
                    anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Art/Animators/MinTest.controller");
                    anim.avatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/Art/Models/man.fbx");
                    anim.SetFloat("Speed", 1f);
                    var leg = anim.transform.Find("mixamorig:Hips/mixamorig:LeftUpLeg");
                    sb.AppendLine("已切换最小 controller, 立即采样腿: " + (leg != null ? leg.localEulerAngles.ToString() : "无"));
                    float start = Time.realtimeSinceStartup;
                    EditorApplication.update += LateSample;
                    void LateSample()
                    {
                        if (Time.realtimeSinceStartup - start < 0.5f) return;
                        EditorApplication.update -= LateSample;
                        sb.AppendLine("0.5s后采样腿: " + (leg != null ? leg.localEulerAngles.ToString() : "无"));
                        var st0 = anim.GetCurrentAnimatorStateInfo(0);
                        sb.AppendLine("0.5s后层0状态hash=" + st0.shortNameHash + " norm=" + st0.normalizedTime.ToString("F2") + " clip=" + (anim.GetCurrentAnimatorClipInfo(0).Length > 0 ? anim.GetCurrentAnimatorClipInfo(0)[0].clip.name : "无!"));
                        sb.AppendLine("实际播放clip: " + (anim.GetCurrentAnimatorClipInfo(0).Length > 0 ? anim.GetCurrentAnimatorClipInfo(0)[0].clip.name : "无!"));
                        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/min_test.txt", sb.ToString());
                    }
                }
                else { sb.AppendLine("未找到 man Animator"); }
            }
            else { sb.AppendLine("非 Play 模式，只创建了 controller"); File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/min_test.txt", sb.ToString()); }
        }
        catch (System.Exception e)
        {
            sb.AppendLine("异常: " + e);
            File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/min_test.txt", sb.ToString());
        }
        Debug.Log("[MinTest] " + sb.ToString());
    }
}

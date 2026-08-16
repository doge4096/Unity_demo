using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Text;
using System.IO;

/// <summary>
/// 运行时诊断 — 输出 Animator 每层当前状态、层权重、参数值（排查边跑边攻击问题）
/// 执行方式：Play 模式下 菜单「工具/诊断动画状态」
/// 结果写到 Assets/Screenshots/anim_state.txt
/// </summary>
public static class AnimStateDiagnose
{
    [MenuItem("工具/诊断动画状态")]
    [MenuItem("Tools/Diagnose Anim State")]
    public static void Run()
    {

        var sb = new StringBuilder();
        // FindObjectsOfTypeAll 能查未激活对象；遍历所有 Animator 找 MeleeAnimator 控制器
        var anim = default(Animator);
        AnimatorController controller = null;
        foreach (var a in Resources.FindObjectsOfTypeAll<Animator>())
        {
            if (a.runtimeAnimatorController != null &&
                (a.runtimeAnimatorController.name.Contains("Melee") || a.runtimeAnimatorController.name.Contains("Ranged")))
            {
                anim = a;
                sb.AppendLine($"找到 Animator: {a.gameObject.name} controller={a.runtimeAnimatorController.name} (active={a.gameObject.activeInHierarchy})");
                break;
            }
        }
        if (anim == null)
        {
            sb.AppendLine("未找到 MeleeAnimator 控制器的 Animator!");
        }
        else
        {
            controller = anim.runtimeAnimatorController as AnimatorController;
            sb.AppendLine($"== Animator: layerCount={anim.layerCount} 速度={anim.speed} ==");

            // 收集每层状态名 -> hash 映射（诊断脚本无法反查 hash，直接遍历 controller）
            for (int i = 0; i < anim.layerCount; i++)
            {
                var st = anim.GetCurrentAnimatorStateInfo(i);
                string stateName = "(未找到)";
                if (controller != null && i < controller.layers.Length)
                {
                    var sm = controller.layers[i].stateMachine;
                    foreach (var cs in sm.states)
                    {
                        if (cs.state.nameHash == st.shortNameHash || Animator.StringToHash(cs.state.name) == st.shortNameHash)
                        {
                            stateName = cs.state.name;
                            break;
                        }
                    }
                    var layer = controller.layers[i];
                    sb.AppendLine($"层{i} '{layer.name}' 运行时权重={anim.GetLayerWeight(i)} 资产权重={layer.defaultWeight} 状态='{stateName}' 播放时间={st.normalizedTime:F2}/{st.length:F2}s speedMultiplier={st.speedMultiplier}");
                }
            }

            // 打印参数值
            sb.AppendLine("== 参数 ==");
            foreach (var p in anim.parameters)
            {
                object v = null;
                switch (p.type)
                {
                    case AnimatorControllerParameterType.Float: v = anim.GetFloat(p.name); break;
                    case AnimatorControllerParameterType.Int: v = anim.GetInteger(p.name); break;
                    case AnimatorControllerParameterType.Bool: v = anim.GetBool(p.name); break;
                    case AnimatorControllerParameterType.Trigger: v = "(trigger)"; break;
                }
                sb.AppendLine($"  {p.name} ({p.type}) = {v}");
            }
        }

        // 打印每个状态的 motion 引用（排查 motion 是否丢失）
        if (controller != null)
        {
            sb.AppendLine("== 状态 motion 引用 ==");
            foreach (var layer in controller.layers)
            {
                foreach (var cs in layer.stateMachine.states)
                {
                    var st = cs.state;
                    var m = st.motion;
                    string info = m != null ? m.name : "NULL(motion丢失!)";
                    sb.AppendLine($"  [{layer.name}] {st.name} -> motion='{info}'");
                }
            }
        }

        // 打印 AnyState 过渡及条件（排查挂载/条件问题）
        if (controller != null)
        {
            sb.AppendLine("== AnyState 过渡 ==");
            foreach (var layer in controller.layers)
            {
                sb.AppendLine($"层 '{layer.name}' 权重={layer.defaultWeight} mask={(layer.avatarMask != null ? layer.avatarMask.name : "(无)")}");
                var asts = layer.stateMachine.anyStateTransitions;
                sb.AppendLine($"  AnyState过渡数={asts.Length}");
                foreach (var t in asts)
                {
                    string dst = t.destinationState != null ? t.destinationState.name : "(?)";
                    var conds = new StringBuilder();
                    foreach (var c in t.conditions)
                        conds.Append($"[{c.parameter}: mode{c.mode} 阈值{c.threshold}] ");
                    sb.AppendLine($"    -> {dst} 条件: {conds} 有ExitTime={t.hasExitTime} duration={t.duration}");
                }
            }
        }

        // 检查 man_attack1.fbx 的 clip 资产是否有效（controller 引用的 fileID 是否匹配）
        sb.AppendLine("== fbx clip 资产检查 ==");
        foreach (var fbxPath in new[] { "Assets/Art/Animations/man_attack1.fbx", "Assets/Art/Animations/man_attack2.fbx" })
        {
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var sub in subAssets)
            {
                if (sub is AnimationClip clip)
                {
                    // 通过 GlobalObjectId 拿真实 fileID
                    var gid = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(sub);
                    sb.AppendLine($"  {fbxPath} -> clip '{clip.name}' fileID={gid.targetObjectId} len={clip.length:F2}s loop={clip.isLooping}");
                }
            }
        }

        // 打印 AvatarMaskBodyPart 枚举全部成员（确认哪些部位存在）
        sb.AppendLine("AvatarMaskBodyPart 枚举: " + string.Join(",", System.Enum.GetNames(typeof(AvatarMaskBodyPart))));

        // 检查 clip 曲线数据（humanoid 动画是否为空）+ 实际 fileID（GlobalObjectId）
        foreach (var clipPath in new[] { "Assets/Art/Animations/man_attack1.fbx", "Assets/Art/Animations/man_Run.fbx" })
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (c != null)
            {
                var gid = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(c);
                sb.AppendLine($"clip检查 {clipPath.Split('/')[^1]}: empty={c.empty} len={c.length:F2}s frameRate={c.frameRate} loop={c.isLooping} 实际fileID={gid.targetObjectId} (无符号={gid.targetObjectId.ToString()})");
            }
        }

        // 打印 man_attack1 clip 的曲线绑定路径（mask 路径必须匹配它）
        var attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/man_attack1.fbx");
        if (attackClip != null)
        {
            var bindings = UnityEditor.AnimationUtility.GetCurveBindings(attackClip);
            var paths = new System.Collections.Generic.List<string>();
            foreach (var b in bindings)
                if (!paths.Contains(b.path)) paths.Add(b.path);
            sb.AppendLine("== man_attack1 曲线绑定路径（前 15）==");
            for (int i = 0; i < Mathf.Min(15, paths.Count); i++)
                sb.AppendLine($"  [{i}] '{paths[i]}'");
        }

        // 模型腰部骨骼映射检查（Chest/UpperChest 错位会导致腰部扭曲）
        if (anim != null && anim.avatar != null)
        {
            foreach (var b in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck })
            {
                var t = anim.GetBoneTransform(b);
                sb.AppendLine("模型映射 " + b + " -> " + (t != null ? t.name : "NULL!"));
            }
        }

        // 打印模型骨骼树完整路径（确认 mask transform 路径的基准）
        if (anim != null)
        {
            sb.AppendLine("== 模型骨骼树（前 25 个）==");
            int boneCount = 0;
            System.Action<Transform, string> walk = null;
            walk = (t, prefix) =>
            {
                if (boneCount >= 25) return;
                sb.AppendLine($"  {prefix}{t.name}");
                boneCount++;
                foreach (Transform child in t)
                    walk(child, prefix + t.name + "/");
            };
            walk(anim.transform, "");
        }

        // 直接检查 mask 资产能否加载（区分资产损坏 vs 引用失效）
        var maskAsset = AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/Art/Masks/UpperBody.mask");
        sb.AppendLine("GUID解析: " + AssetDatabase.GUIDToAssetPath("e7b7727639c977c48a64049e2470a19c"));
        // 资产直读 controller 的 mask 引用（排除 runtimeAnimatorController 转换问题）
        var ctrlAsset = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Art/Animators/MeleeAnimator.controller");
        if (ctrlAsset != null && ctrlAsset.layers.Length > 1)
        {
            var am = ctrlAsset.layers[1].avatarMask;
            sb.AppendLine("资产controller 层1 mask: " + (am != null ? am.name + " transform数=" + am.transformCount : "(null)"));
        }
        sb.AppendLine("== Mask 资产检查 ==");
        if (maskAsset == null)
        {
            sb.AppendLine("UpperBody.mask 加载失败 (返回 null)!");
        }
        else
        {
            sb.AppendLine($"UpperBody.mask 加载成功 transform数={maskAsset.transformCount}");
            // 遍历 AvatarMaskBodyPart 打印启用情况
            var enabled = new System.Collections.Generic.List<string>();
            foreach (AvatarMaskBodyPart part in System.Enum.GetValues(typeof(AvatarMaskBodyPart)))
            {
                if (maskAsset.GetHumanoidBodyPartActive(part))
                    enabled.Add(part.ToString());
            }
            sb.AppendLine($"  humanoid 启用部位: {(enabled.Count > 0 ? string.Join(",", enabled) : "(无)")}");
        }

        // Play 时间状态（动画是否推进）
        if (Application.isPlaying)
        {
            sb.AppendLine($"Time状态: time={Time.time:F2} timeScale={Time.timeScale} deltaTime={Time.deltaTime:F4} frameCount={Time.frameCount} realtimeSinceStartup={Time.realtimeSinceStartup:F1}");
        }

        // 渲染器可见性检查（CullUpdateTransforms 在不可见时冻结 Animator）
        if (anim != null)
        {
            foreach (var r in anim.GetComponentsInChildren<Renderer>(true))
                sb.AppendLine($"渲染器 {r.gameObject.name}: isVisible={r.isVisible} enabled={r.enabled}");
        }

        // culling 验证：临时改 AlwaysAnimate 并采样骨骼（运行时修改，不写场景）
        if (anim != null && Application.isPlaying)
        {
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var leg = anim.transform.Find("mixamorig:Hips/mixamorig:LeftUpLeg");
            if (leg != null)
            {
                sb.AppendLine($"culling已改AlwaysAnimate, 立即采样腿: {leg.localEulerAngles}");
                // 延迟 0.5s 后采样（用 update 回调）
                float start = Time.realtimeSinceStartup;
                EditorApplication.update += LateSample;
                void LateSample()
                {
                    if (Time.realtimeSinceStartup - start < 0.5f) return;
                    EditorApplication.update -= LateSample;
                    sb.AppendLine($"0.5s后采样腿: {leg.localEulerAngles}");
                    File.AppendAllText("D:/Project/unity/interview/Assets/Screenshots/anim_state.txt", sb.ToString());
                }
            }
        }

        // Animator 底层状态检查（Playable 系统是否运行）
        if (anim != null)
        {
            sb.AppendLine($"Animator底层: enabled={anim.enabled} isActiveAndEnabled={anim.isActiveAndEnabled} updateMode={anim.updateMode} culling={anim.cullingMode}");
            sb.AppendLine($"PlayableGraph: valid={anim.playableGraph.IsValid()} playing={anim.playableGraph.IsPlaying()} speed={anim.speed}");
            sb.AppendLine($"isHuman={anim.isHuman} hasBoundPlayables={anim.hasBoundPlayables}");
            // 运行时实际播放的 clip（确认烘焙后的 motion 是否有效）
            for (int li = 0; li < anim.layerCount; li++)
            {
                var clips = anim.GetCurrentAnimatorClipInfo(li);
                if (clips.Length > 0)
                    sb.AppendLine($"层{li} 实际播放clip: '{clips[0].clip.name}' weight={clips[0].weight} length={clips[0].clip.length:F2}s empty={clips[0].clip.empty}");
                else
                    sb.AppendLine($"层{li} 实际播放clip: 无!");
            }
        }

        // 多骨骼采样（Humanoid 后确认动画是否驱动）
        if (anim != null)
        {
            var s2 = anim.transform.Find("mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2");
            var leg = anim.transform.Find("mixamorig:Hips/mixamorig:LeftUpLeg");
            var arm = anim.transform.Find("mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:LeftShoulder/mixamorig:LeftArm");
            sb.AppendLine("多骨骼旋转: Spine2=" + (s2 != null ? s2.localEulerAngles.ToString() : "无") + " LeftUpLeg=" + (leg != null ? leg.localEulerAngles.ToString() : "无") + " LeftArm=" + (arm != null ? arm.localEulerAngles.ToString() : "无"));
            sb.AppendLine("Avatar 状态: " + (anim.avatar != null ? anim.avatar.name + " isHuman=" + anim.avatar.isHuman : "null"));
            // Humanoid 骨骼映射完整性检查
            if (anim.avatar != null)
            {
                var tLeg = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                var tHead = anim.GetBoneTransform(HumanBodyBones.Head);
                var tEye = anim.GetBoneTransform(HumanBodyBones.LeftEye);
                sb.AppendLine("GetBoneTransform: LeftUpLeg=" + (tLeg != null ? tLeg.name : "NULL!") + " Head=" + (tHead != null ? tHead.name : "NULL!") + " LeftEye=" + (tEye != null ? tEye.name : "NULL!"));
                sb.AppendLine("avatar 有效: " + anim.avatar.isValid);
            }
        }

        string outPath = "D:/Project/unity/interview/Assets/Screenshots/anim_state.txt";
        File.WriteAllText(outPath, sb.ToString());

        // 幅度量化：播放 Run 连续采样腿/腰旋转范围
        if (anim != null && Application.isPlaying)
        {
            anim.SetFloat("Speed", 1f);
            float start = Time.realtimeSinceStartup;
            var amp = new System.Text.StringBuilder();
            amp.AppendLine("== 幅度采样（Run, 20帧）==");
            int frame = 0;
            EditorApplication.update += AmpWatch;
            void AmpWatch()
            {
                if (Time.realtimeSinceStartup - start < 0.5f) return;
                frame++;
                var leg = anim.transform.Find("mixamorig:Hips/mixamorig:LeftUpLeg");
                var spine = anim.transform.Find("mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2");
                if (leg != null)
                {
                    string spineX = spine != null ? spine.localEulerAngles.x.ToString("F1") : "?";
                    amp.AppendLine("F" + frame + ": legX=" + leg.localEulerAngles.x.ToString("F1") + " spineX=" + spineX);
                }
                if (frame >= 20)
                {
                    EditorApplication.update -= AmpWatch;
                    File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/anim_amp.txt", amp.ToString());
                }
            }
        }

        // 攻击全程腰部/腿部旋转范围采样（Humanoid 转换是否放大幅度）
        if (anim != null && Application.isPlaying)
        {
            var chest = anim.transform.Find("mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2");
            var leg = anim.transform.Find("mixamorig:Hips/mixamorig:LeftUpLeg");
            if (chest != null)
            {
                anim.SetInteger("Combo", 1);
                anim.ResetTrigger("Attack");
                anim.SetTrigger("Attack");
                float start = Time.realtimeSinceStartup;
                var amp = new System.Text.StringBuilder();
                amp.AppendLine("== attack1 全程腰/腿旋转 ==");
                int f = 0;
                EditorApplication.update += AmpWatch;
                void AmpWatch()
                {
                    if (Time.realtimeSinceStartup - start < 0.25f) return;
                    f++;
                    if (f % 8 == 0)
                    {
                        string legS = leg != null ? leg.localEulerAngles.x.ToString("F0") : "?";
                        string chestS = chest.localEulerAngles.x.ToString("F0");
                        string chestY = chest.localEulerAngles.y.ToString("F0");
                        amp.AppendLine("F" + f + ": legX=" + legS + " chestX=" + chestS + " chestY=" + chestY);
                    }
                    if (f >= 64)
                    {
                        EditorApplication.update -= AmpWatch;
                        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/anim_attack_amp.txt", amp.ToString());
                    }
                }
            }
        }

        // 攻击中手/脚/头世界位置采样（判断姿势是否扭曲：位置错位）
        if (anim != null && Application.isPlaying)
        {
            var rHand = anim.transform.Find("mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:RightShoulder/mixamorig:RightArm/mixamorig:RightForeArm/mixamorig:RightHand");
            var lHand = anim.transform.Find("mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:LeftShoulder/mixamorig:LeftArm/mixamorig:LeftForeArm/mixamorig:LeftHand");
            var rFoot = anim.transform.Find("mixamorig:Hips/mixamorig:RightUpLeg/mixamorig:RightLeg/mixamorig:RightFoot");
            var head = anim.transform.Find("mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:Neck/mixamorig:Head");
            if (rHand != null)
            {
                anim.SetInteger("Combo", 1);
                anim.ResetTrigger("Attack");
                anim.SetTrigger("Attack");
                float start = Time.realtimeSinceStartup;
                var pos = new System.Text.StringBuilder();
                pos.AppendLine("== attack1 骨骼世界位置 ==");
                int f = 0;
                EditorApplication.update += PosWatch;
                void PosWatch()
                {
                    if (Time.realtimeSinceStartup - start < 0.25f) return;
                    f++;
                    if (f % 10 == 0)
                    {
                        string rh = rHand.position.ToString("F2");
                        string lh = lHand != null ? lHand.position.ToString("F2") : "?";
                        string rf = rFoot != null ? rFoot.position.ToString("F2") : "?";
                        string hd = head != null ? head.position.ToString("F2") : "?";
                        pos.AppendLine("F" + f + ": RHand=" + rh + " LHand=" + lh + " RFoot=" + rf + " Head=" + hd);
                    }
                    if (f >= 50)
                    {
                        EditorApplication.update -= PosWatch;
                        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/anim_pos.txt", pos.ToString());
                    }
                }
            }
        }

        // 攻击时 Hips 骨骼世界位置采样（排查飞起来：骨骼 Y 是否被动画驱动）
        if (anim != null && Application.isPlaying)
        {
            var hips = anim.transform.Find("mixamorig:Hips");
            if (hips != null)
            {
                anim.SetInteger("Combo", 1);
                anim.ResetTrigger("Attack");
                anim.SetTrigger("Attack");
                float start = Time.realtimeSinceStartup;
                var fly = new System.Text.StringBuilder();
                fly.AppendLine("== 攻击时 Hips 世界位置 ==");
                int f = 0;
                EditorApplication.update += FlyWatch;
                void FlyWatch()
                {
                    if (Time.realtimeSinceStartup - start < 0.3f) return;
                    f++;
                    fly.AppendLine("F" + f + ": HipsY=" + hips.position.y.ToString("F3") + " charY=" + anim.transform.position.y.ToString("F3"));
                    if (f >= 15)
                    {
                        EditorApplication.update -= FlyWatch;
                        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/anim_fly.txt", fly.ToString());
                    }
                }
            }
        }

        // attack1 开头帧采样（判断是否 T-pose：Hips Y + 手臂姿势）
        if (anim != null && Application.isPlaying)
        {
            var hips = anim.transform.Find("mixamorig:Hips");
            var arm = anim.transform.Find("mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:LeftShoulder/mixamorig:LeftArm");
            if (hips != null)
            {
                anim.SetInteger("Combo", 1);
                anim.ResetTrigger("Attack");
                anim.SetTrigger("Attack");
                float start = Time.realtimeSinceStartup;
                var tp = new System.Text.StringBuilder();
                tp.AppendLine("== attack1 开头帧采样 ==");
                int f = 0;
                EditorApplication.update += TpWatch;
                void TpWatch()
                {
                    if (Time.realtimeSinceStartup - start < 0.15f) return;
                    f++;
                    string armStr = arm != null ? arm.localEulerAngles.ToString("F0") : "?";
                    tp.AppendLine("F" + f + ": hipsY=" + hips.position.y.ToString("F3") + " arm=" + armStr + " state=" + anim.GetCurrentAnimatorStateInfo(0).shortNameHash);
                    if (f >= 12)
                    {
                        EditorApplication.update -= TpWatch;
                        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/anim_tpose.txt", tp.ToString());
                    }
                }
            }
        }

        // 根运动幅度采样（排查腰部/位移异常：deltaPosition/deltaRotation）
        if (anim != null && Application.isPlaying)
        {
            anim.Play("Run", 0, 0f);
            float start = Time.realtimeSinceStartup;
            var rm = new System.Text.StringBuilder();
            rm.AppendLine("== 根运动采样（Run）==");
            EditorApplication.update += RmWatch;
            void RmWatch()
            {
                if (Time.realtimeSinceStartup - start < 0.5f) return;
                EditorApplication.update -= RmWatch;
                rm.AppendLine($"deltaPosition={anim.deltaPosition} 幅度={anim.deltaPosition.magnitude:F3}");
                rm.AppendLine($"deltaRotation={anim.deltaRotation.eulerAngles} 幅度={anim.deltaRotation.eulerAngles.magnitude:F2}");
                File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/anim_root_motion.txt", rm.ToString());
            }
        }

        // 强制播放 + 间隔采样：验证动画是否真正驱动骨骼（Humanoid 曲线是否有效）
        if (anim != null && Application.isPlaying)
        {
            var leg = anim.transform.Find("mixamorig:Hips/mixamorig:LeftUpLeg");
            var watchLog = new System.Text.StringBuilder();
            watchLog.AppendLine("== 强制播放 Run + 间隔采样 ==");
            anim.Play("Run", 0, 0f);
            int frames = 0;
            EditorApplication.update += WatcherBones;
            void WatcherBones()
            {
                frames++;
                if (frames <= 30)
                {
                    if (frames % 6 == 0 && leg != null)
                        watchLog.AppendLine($"帧{frames}: LeftUpLeg={leg.localEulerAngles}");
                }
                else
                {
                    EditorApplication.update -= WatcherBones;
                    File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/anim_bone_watch.txt", watchLog.ToString());
                }
            }
        }

        // 多帧手臂采样：跑步攻击时连续记录 LeftArm 旋转（判断攻击动画是否真正驱动手臂）
        if (anim != null && Application.isPlaying)
        {
            var arm = anim.transform.Find("mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:LeftShoulder/mixamorig:LeftArm");
            if (arm != null)
            {
                var log = new System.Text.StringBuilder();
                log.AppendLine("== 多帧手臂采样（跑步攻击）==");
                anim.SetInteger("Combo", 1);
                anim.ResetTrigger("Attack");
                anim.SetTrigger("Attack");
                int n = 0;
                EditorApplication.update += Watcher;
                void Watcher()
                {
                    n++;
                    if (n <= 12)
                    {
                        log.AppendLine($"帧{n}: LeftArm Y={arm.localEulerAngles.y:F1} 层1状态={anim.GetCurrentAnimatorStateInfo(1).shortNameHash}");
                    }
                    else
                    {
                        EditorApplication.update -= Watcher;
                        File.WriteAllText("D:/Project/unity/interview/Assets/Screenshots/anim_arm_track.txt", log.ToString());
                    }
                }
            }
        }
        Debug.Log("[诊断] 已写入 " + outPath);
    }
}

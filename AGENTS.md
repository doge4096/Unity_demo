# AGENTS.md — Unity 肉鸽面试项目交接文档

> 本文件供任何 AI 协作工具（Codex / Claude Code 等）快速上手本项目。
> 项目根：`d:\Project\unity\interview`（Unity 2022.3.62f1c，Windows，中文版 Unity 界面）

---

## 1. 协作铁律（必须遵守）

- **回复、代码注释、Unity 按钮/菜单/字段名称一律简体中文**（技术术语保留英文）
- **禁止用截图做验证**（AI 无法可靠解析图片）→ 一律用 **console 日志 + 运行时组件查询**（反射读字段、activeSelf、Transform 位置打日志）
- 变量名/函数名用英文，注释用中文
- 禁止用 `&&` 拼接 `cd` + 命令，每一步单独列；npm/npx/curl 只允许在项目 `/frontend` 目录执行

## 2. 外部依赖：UnitySkills REST 服务（唯一远程操控 Unity 的通道）

Unity 编辑器内运行 UnitySkills 2.4.2 插件，提供 REST 接口：

```
POST http://127.0.0.1:8090/skill/{skillName}   body: JSON
```

**端口会变**（历史：54286 → 8090）。探测方法：
```
netstat -ano | grep Unity.exe 的 PID（任务管理器查 Unity 进程 PID）
```

### 关键 skill 与参数（全是踩坑换来的）

| skill | 参数 | 说明 |
|---|---|---|
| `editor_play` | `{}`（空 body） | **空 body = 进入播放**，不是查询！返回 "Already in play mode" 表示已在播放 |
| `editor_stop` | `{}` | 退出播放 |
| `editor_pause` | `{}` | 暂停；再次调用 = 取消暂停 |
| `editor_playmode_step` | `{"frames": N}` | 逐帧步进（暂停状态下） |
| `editor_execute_menu` | `{"menuPath":"Tools/xxx"}` | 执行编辑器菜单（参数名是 menuPath，不是 args） |
| `gameobject_set_active` | `{"entityId":"59156","active":true}` | 激活对象；entityId 用 `gameobject_find` / `gameobject_get_info` 查，每次会话会变 |
| `animator_set_parameter` | Bool→`boolValue`，Float→`floatValue`，Trigger→`intValue:1` | 设置 Animator 参数 |
| `animator_play` | `{"stateName":..., "layer":0, "normalizedTime":...}` | 强制播放指定状态（验证短动画用） |
| `editor_get_state` | `{}` | 查 isPlaying / isCompiling / unityVersion |
| `debug_force_recompile` | `{}` | **强制编译**（Unity 不自动编译时用它，等 ~20-30s） |
| `console_get_logs` | type/limit | **一直返回空，别用** → 诊断一律读 `%LOCALAPPDATA%\Unity\Editor\Editor.log` |

### 编译与运行节奏（重要）

1. **Play Mode 中不编译脚本** → 改代码前先 `editor_stop` 退出播放
2. 编译是异步的（bee）→ 改完等 15-20s，或 `debug_force_recompile` 后轮询 `editor_get_state.isCompiling`，或用 `Library/ScriptAssemblies/Assembly-CSharp-Editor.dll` 时间戳确认
3. 编译失败时菜单找不到 → 去 Editor.log 里 grep `error CS`
4. **GameManager 每轮 Play 都会隐藏两个角色** → 采样/操作前必须 `gameobject_set_active` 激活 RangedPlayer（或 MeleePlayer）
5. **Play 时 PlayerController 不控制任何角色**（停在选人界面）→ 实测逻辑必须先用「工具/模拟选择远程角色」模拟选人（见下）

## 3. 编辑器诊断/测试工具（菜单 = 中文名，REST 调用用英文别名）

位于 `Assets/Scripts/Editor/`（这些是历轮开发留下的，可放心保留，全部幂等）：

| 菜单（中文） | 英文别名（REST 用） | 作用 |
|---|---|---|
| 工具/采样膝盖与状态 | `Tools/SampleKneeAndState` | **核心采样**：帧号/动画状态/膝弯角/HipsY/Speed/Aim/AimX/AimZ，写入 `D:/tmp/sample_knee.txt` + Debug.Log |
| 工具/测试射击瞄准保持 | `Tools/TestAimHold` | 反射设置 `_aimHoldTimer`（模拟刚射击，观察瞄准保持） |
| 工具/模拟选择远程角色 | `Tools/TestSelectRanged` | 模拟选人（GameManager.OnCharacterSelected → Running，PlayerController 控制远程角色）——**实测逻辑链必跑** |
| 工具/诊断玩家控制器 | `Tools/DebugPlayer` | 打印 GameManager 状态 / PlayerController._currentCharacter / _aimHoldTimer / Female Animator 参数（控制链排查） |
| 工具/修复女性遮罩人形位 | `Tools/FixFemaleMask` | 修复 FemaleUpperBody.mask 的 humanoid 位（腿/根关闭） |
| 工具/检查遮罩骨骼 | `Tools/CheckMaskBones` | 打印 mask 每个 transform 位 + humanoid 位 |
| 工具/修复射击分层遮罩 | `Tools/FixUpperBodyMask` | 把 FemaleUpperBody.mask 挂到控制器 UpperBody 层 |
| 工具/重建女性控制器V2-混合树 | `Tools/RebuildFemaleControllerV2` | V2 Blend Tree 重建（幂等，guid 兜底表） |
| 工具/清理场景缺失脚本 | `Tools/CleanMissingScripts` | 清理场景/预制体 missing script |

另有历史诊断脚本（AllBonesSampler / BadCurveScanner / BoneJitterSampler / CurveDump / FbxRootDiagnose 等），按需使用。

## 4. 核心架构

```
Assets/Scripts/
├── Characters/PlayerController.cs   # 输入控制中枢：移动/攻击/瞄准/跳跃 + 射击后瞄准保持（aimHoldDuration）
├── Characters/CharacterBase.cs      # 角色基类
├── Characters/RangedCharacter.cs    # 远程（右键瞄准 + 射击）
├── Characters/MeleeCharacter.cs     # 近战（三段连击 + 格挡）
├── Core/GameManager.cs              # 状态机：MainMenu→CharacterSelect→Running→Paused→GameOver；OnCharacterSelected 激活角色
├── Camera/ThirdPersonCamera.cs      # 第三人称相机（SetAiming 放大镜头）
Assets/Art/
├── Animators/FemaleAnimator.controller  # 远程角色控制器：Base 层（移动/瞄准/射击）+ UpperBody 层（mask 分层）
├── Masks/FemaleUpperBody.mask       # 女性上半身 mask（humanoid 位：腿/根已关）
Assets/Scenes/SampleScene.unity      # 唯一场景
```

### 动画 guid 映射（改控制器必备，第七轮确立）

Idle=female_Idle(49f3afbf…) / Walk=man_Walking_fixed(70921a40…) / Run=man_Run_fixed(f7bbcba3…) / AimIdle=female_aimIdle / AimRun=female_aimRun_fixed / AimShoot=female_aimShoot / Shoot=female_shoot_fixed / AimHit=female_aimHit_fixed / Reload=female_reload_fixed / Hit=female_HitReaction / Die=female_death / JumpStart=female_jumpstart / JumpLoop=female_floating / JumpLand=female_landing

## 5. 核心踩坑（12 轮血泪，务必先读）

1. **AvatarMask 有两层位**：transform 位（m_Elements）+ humanoid 位（m_HumanoidMask）。Humanoid Animator 时 **humanoid 位是主开关**——transform 位全对但 humanoid 腿位 True = mask 无效（走路射击时腿被锁）。检查：`mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart)`。**AvatarMask 没有 humanoidBodyParts 属性**（Unity 2022.3 编译 CS1061）
2. **full-body 动画透传**：female_shoot_fixed.anim 是全身动画（130 条曲线含 LeftFootT/Q/RightFootIK/Root），任何没挡住腿的 mask 都会在走路时锁腿
3. **判"腿定住" vs "走路着地"**：定住 = 膝弯角恒定（±0.5°）；走路 = 45°→10° 大幅摆动，2-3° 短时出现是着地相位（正常）
4. **重建动画控制器必须幂等**：AddState/AddTransition/AddParameter 自动注册子对象；只有 new AnimatorStateMachine/BlendTree 需要 AddObjectToAsset；旧控制器可能 motion=null（历史 bug 版），必须用 StateClipGuids 字典按 guid 从磁盘找回
5. **Editor 脚本方法名不能叫 Debug()**（类内 Debug.Log 解析冲突 → CS0119）
6. **膝弯角诊断路径**：`Female/mixamorig1:Hips/LeftUpLeg/LeftLeg/LeftFoot`（mixamorig1: 前缀，女性模型）
7. **Editor.log 是唯一可靠日志源**（console_get_logs 永远返回空；sample_knee.txt 是文件兜底）

## 6. 当前进度（2026-08-16 第十八轮）

**第十八轮（本轮，走路/待机动画方案定型 + 瞄准走路混合树修复）**：
- **动画方案最终定型**（用户主导决策，Basic Shooter Pack 的 rifle aiming idle/walking 已删除）：
  - **待机 AimIdle** → female_aimIdle_fixed（用户手动改回，不再用 rifle aiming idle）
  - **走路 Walk** → female_aimRun_fixed（AimRun 正在用的持枪跑步动画），靠 **walkPlaybackSpeed=0.6**（PlayerController 新增 [SerializeField] 字段，原 const WalkPlaybackSpeed=2.1 废弃）慢放成走路步频；冲刺保持 sprintAnimSpeed=1.2
  - **瞄准走路 AimWalk** → 2D 混合树重建（修复"左右后方向不播放/全播前向"）：根因 = AimWalk 状态 motion 为空（控制器里 BlendTree 丢失）
- **新工具**：`SwapShooterPackAnimations.cs`（菜单「工具/走路换持枪跑步并修复瞄准走路」）：
  - Walk 状态 → female_aimRun_fixed
  - 重建 AimWalk 混合树：SimpleDirectional2D（AimX/AimZ），中心=female_aimWalk_fixed + 前/右/后/左 = female_aimWalk{,_Right,_Back,_Left}_fixed（guid 已核对），幂等（已有混合树跳过）
- **KayKit 动画包**（KayKit_Character_Animations_1.1，itch.io CC0 免费 161 动画）已导入但未用：骨骼非标准命名（hips/spine/chest + upperarm.l 等小写 .l/.r 后缀）→ Unity 自动判 Generic，需手动 Avatar 映射或脚本转 Humanoid 才能用；8 个分类 fbx 均 3MB 左右
- **Basic Shooter Pack**（Asset Store 免费 Mixamo 动画包，8/10 导入）：rifle aiming idle/walking 已删；其余（firing rifle/reloading/hit reaction/rifle run/strafe 系列）多为 Generic，如需用要转 Humanoid
- **协作模式变更**：用户要求"没明确说就不写脚本，先给操作流程让用户自己操作"（已记入记忆）

**第十七轮（历史，Mixamo 新版踩坑 + aimIdle 反弓排查 + 换站决定）**：
- **Mixamo 网站改版（根因级发现）**：新版（8/13 之后）下载行为：
  - **Without Skin（不带皮肤）= 无骨架**（meta `skeleton: []`、`animationType: 2` Generic，Unity 无法 Humanoid 导入）
  - **With Skin（带皮肤）= 有骨架 + 网格**（~101MB）
  - 旧版（8/5 前）Without Skin 是有骨架纯动画 → 8/5 批次动画正常、8/13+ 批次全踩坑（aimWalk 无骨架、aimIdle 带网格）
  - 下载弹窗已无 T-Pose/Pose 选项（只有 Skin / Format / FPS / Keyframe Reduction）；动画库搜 "T-Pose" 可作等效纯模型
- **aimIdle 反弓+手反向（未解决，已定位数据）**：
  - 旧 aimIdle.fbx 一直是 **131 字节坏文件**（git 7ef47be/184eb73）→ 之前的"待机"是空动画；新 aimIdle（8/15 101MB With Skin）是第一个真内容版本
  - 已排除：骨骼名一致（65=65）、骨架树一致、bind pose 差异（Hips 18.2° vs 模型 0°，但 aimShoot 手骨 115° 播放正常）→ 非 bind pose
  - 异常：肌肉曲线右手腕 Down-Up **-1.40**、右脚趾 Up-Down **-1.39**（正常动画≈0）；脊柱恒定后仰 +0.02~+0.06（正常动画摆动过零）
  - 模型 Female.fbx Spine=0.10434 vs 动画 Spine=0.097954（差 6%，可能非同一 Mixamo 角色；旧动画正常 → 长度差被 retarget 兼容，非主因）
  - 诊断工具：`Assets/Scripts/Editor/` 现有 + `d:/tmp/fbx_scan.py`（FBX 二进制骨骼解析，meta 缺失时用）
- **用户决定：放弃 Mixamo 换网站**（Ch48_nonPBR@T-Pose .fbx 无骨架无用）：
  - **Rokoko 动画库** https://animation.rokoko.com（免费、标准 T-pose 骨架、射击动画全、邮箱注册）
  - **Unity Asset Store** 免费 Humanoid 动画/角色（生态内 100% 兼容）
  - **核心原则：任何网站动画只要 Unity 按 Humanoid 导入成功（有骨架），即可重定向到现有 Female 模型，模型不用换**
- **待办**：用户选站 → 下载替换 aimIdle（Rifle Idle 类）→ 预览检查手/腰 → 工具/重建全部固定动画 → Play 测试
- 杂项：Mixamo 官网弹窗"进群领会员卡"广告 = 浏览器插件注入（无痕模式验证 + 清扩展）

**第十三~十四轮已完成并实测通过**：
- 走路动画从男动画换回女性动画：`RestoreFemaleWalk`（Walk: man_Walking_fixed → female_Walk_fixed）、`RestoreFemaleRun`（Run: man_Run_fixed → female_Run_fixed），撤销了历史实验 SwapFemaleRunToMale / WalkSwap 的遗留
- **统一全程举枪**（用户需求"不需要单独做放下枪的动作"）：
  - `UnifyMoveToAimPose`：Walk → AimWalk 混合树（female_aimWalk 系列）、Run → AimRun（female_aimRun_fixed）
  - `UnifyIdleAim`：Idle → AimIdle 动画（站立也举枪）
- **走路/跑步脚部坏曲线修复**（`FixAimWalkFootTwist`，瞄准走路动画的 Mixamo 源数据问题）：
  - female_aimWalk_fixed 的 Left Foot Twist：-163°~-100° → -6.9°~12.0°（正常）
  - female_aimWalk_fixed 的 Left Lower Leg Twist：68°~84° → 0.4°~17.2°（正常）
- **待机角度修正**（`FixAimIdleRoot` + `ApplyAimIdleFix`）：
  - 生成 female_aimIdle_fixed.anim（原版 RootQ.y=0.361 ≈ 42° 恒定偏转 → identity）
  - Idle/AimIdle 状态实测：HipsY 14°→0°、SpineY 0°（身体摆正）
- **走路朝向修正**（`FixBlendCenter` + PlayerController 修改）：
  - AimWalk 2D 混合树**添加中心节点 (0,0) = female_aimWalk_fixed**：原来原点输入 = 4 方向各 25% 混合（姿态怪异），现在原点输入 = 前向正常（实测 female_aimWalk_fixed×1.00）
  - PlayerController：非瞄准走路时 AimX=0/AimZ=1（强制前向），瞄准时才用 localMoveDir 驱动方向混合

**历史遗留（第十六轮已解决大部分）**：
- 走路"斜向走"持枪朝左前 43.5° 观感：第十六轮通过"Fixed 动画设根变换旋转依据=原始"解决了方向偏 45° 主问题；持枪姿态细节用户未再反馈
- 已删除的实验资产：female_aimWalk2/3/4_fixed.anim（RootQ/手臂/肩膀替换版均无效，勿重建）
- git 工作树有大量未提交变更（动画 fbx/meta、控制器、脚本、新诊断工具等，用户未要求提交）

**本轮新增诊断工具**（全部幂等，可放心保留）：
- `VerifyWalkClip` / `VerifyRunClip`（工具/验证走路/跑步动画）：运行时打印实际播放的 clip 资产路径 + 膝弯角
- `SampleBoneHeading`（工具/采样骨骼朝向）：走路/跑步时 Hips/Spine/左手世界朝向
- `TestWalkBlendDir`（工具/测试走路混合方向）：混合树各输入方向的实际输出
- `CompareAimGunHeading` / `CompareWalkRunHeading` / `CompareFootCurves` / `CompareRunFoot` / `CompareLegTwist` / `CompareArmCurves` / `CompareGunArm`：动画曲线对比诊断
- `FixBlendCenter`（工具/修复混合树中心节点）、`FixAimWalkFootTwist`（工具/修复瞄准走路左脚扭曲）、`FixAimIdleRoot`（工具/生成修正待机动画）、`ApplyAimIdleFix`（工具/应用修正待机动画）、`RestoreFemaleWalk`（工具/恢复女性走路动画）、`RestoreFemaleRun`（工具/恢复女性跑步动画）、`UnifyMoveToAimPose`（工具/统一移动为瞄准姿态）、`UnifyIdleAim`（工具/待机统一为举枪）

## 7. 给接手 AI 的工作建议

- 先跑 `Tools/SampleKneeAndState` 看输出格式，再动手改任何动画相关逻辑
- 改 PlayerController / 动画逻辑后：editor_stop → 编译确认 → editor_play → gameobject_set_active 激活 RangedPlayer → Tools/TestSelectRanged → 用 TestAimHold + SampleKneeAndState 验证
- 验证曲线：膝弯角（腿锁没锁）+ 状态名（切到哪个动画）+ IsAiming（瞄准保持）
- **aimIdle 反弓问题（第十八轮已闭环）**：用户最终决定换回 female_aimIdle_fixed（放弃 Basic Shooter Pack 的 rifle aiming idle，文件已删），实测"基本正常"
- **排查动画资产问题时先查 `.fbx.meta` 的 `rigImportWarnings` / `animationImportErrors` 字段**（`d:/tmp/check_rig_warnings.py` 可全量扫），比翻 Editor.log 快
- **判定 fbx 是否有骨架**：`.fbx.meta` 里 `humanDescription.skeleton: []` 或 `animationType: 2`（Generic）= 无骨架；`animationType: 3`（Humanoid）+ skeleton 非空 = 正常；文件 < 1KB 是坏下载

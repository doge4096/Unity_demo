using UnityEngine;

/// <summary>
/// 玩家控制器 — 读取输入并驱动当前活跃角色移动/攻击/跳跃
/// 长时间跳跃：起跳 → 空中循环 → 落地，三段动画自动切换
/// 选人后由 GameManager 调用 SetCharacter() 指定操控的角色
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("相机引用")]
    [SerializeField] private Transform cameraTransform;

    [Header("移动设置")]
    [SerializeField] private float rotationSmoothTime = 0.1f;

    [Header("跳跃设置")]
    [SerializeField] private float jumpHeight = 2f;            // 跳跃高度（米），调大 = 空中更久
    [SerializeField] private float gravity = -12f;             // 重力加速度，调小 = 下落更慢
    [SerializeField] private float groundedCheckOffset = 0.02f; // 着地检测微小偏移（小=落地判定更准，防提前播落地动画）
    [SerializeField] private LayerMask groundLayer = ~0;       // 地面层
    [SerializeField] private float landingDuration = 0.4f;     // 落地动画预估时长（秒）
    [SerializeField] private int maxJumps = 2;                 // 最大跳跃次数（2 = 二段跳）

    [Header("动画速度（代码统一配置，Inspector 可调）")]
    [SerializeField] private float attack1AnimSpeed = 2.5f; // 第1段攻击动画速度——素材 2.4s/段太慢，提速让连击间隔小于连击重置窗口（1.5s）
    [SerializeField] private float attack2AnimSpeed = 2.5f; // 第2段攻击动画速度（各段可单独调整）
    [SerializeField] private float attack3AnimSpeed = 2.5f; // 第3段攻击动画速度（各段可单独调整）
    [SerializeField] private float jumpAnimSpeed = 1f;     // 跳跃动画速度（起跳/空中/落地）
    [SerializeField] private float blockAnimSpeed = 1f;    // 格挡动画速度
    [SerializeField] private float hitAnimSpeed = 1f;      // 受击动画速度
    [SerializeField] private float dieAnimSpeed = 1f;      // 死亡动画速度
    [SerializeField] private float shootAnimSpeed = 1f;    // 射击动画速度（远程）
    [SerializeField] private float aimAnimSpeed = 1f;      // 瞄准动画速度（远程）

    [Header("当前操控角色（选人后由 GameManager 赋值）")]
    [SerializeField] private CharacterBase _currentCharacter;

    // 移动
    private Vector3 _moveDirection;
    private float _currentRotationVelocity;

    // 跳跃
    private float _verticalVelocity;
    private bool _isGrounded;
    private bool _wasGrounded;
    private bool _isLanding;          // 正在播放落地动画
    private int _jumpsUsed;           // 已使用的跳跃次数（用于二段跳）

    // 冲刺（Shift 切换开关）
    private bool _isSprinting;                      // 当前是否冲刺
    private const float SprintMultiplier = 2.4f;    // 冲刺速度倍率（普通 2.5 × 2.4 ≈ 6，匹配跑步动画）
    [SerializeField] private float sprintAnimSpeed = 1.2f;  // 冲刺时的动画播放速度（走路 1.0 → 冲刺此值，可调）
    private const float WalkAnimSpeed = 0.4f;       // 走路动画 Speed 参数（落在 Walk 状态区间 0.1~0.5 内）

    // 瞄准（远程）
    private bool _isAiming;                         // 远程角色瞄准中（角色面向相机方向）

    // Animator 参数 ID 缓存（比字符串快）
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int AnimSpeedParam = Animator.StringToHash("AnimSpeed"); // 动画播放速度倍率
    private static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
    private static readonly int IsBlockingParam = Animator.StringToHash("IsBlocking"); // 格挡
    private static readonly int IsAimingParam = Animator.StringToHash("IsAiming");   // 瞄准（远程）
    private static readonly int AimXParam = Animator.StringToHash("AimX");           // 瞄准移动方向
    private static readonly int AimZParam = Animator.StringToHash("AimZ");
    private static readonly int JumpStartParam = Animator.StringToHash("JumpStart");
    private static readonly int JumpLandParam = Animator.StringToHash("JumpLand");
    private static readonly int IsDeadParam = Animator.StringToHash("IsDead");

    /// <summary>当前操控的角色（HUD / RunManager 通过此属性访问）</summary>
    public CharacterBase CurrentCharacter => _currentCharacter;

    /// <summary>由 GameManager 在选人后调用</summary>
    public void SetCharacter(CharacterBase character)
    {
        _currentCharacter = character;
        _verticalVelocity = 0f;
        _isLanding = false;

        // 应用动画速度配置（每个动画状态的速度由代码统一控制）
        ApplyAnimationSpeeds();

        // 移动由 CharacterController 驱动，关闭 Animator 的根运动：
        // 避免动画自身的位移/旋转（root motion）叠加到 transform 上（走路偏移、攻击双重位移）
        // 攻击跨步位移仍由代码通过 Animator.deltaPosition 手动应用，不受影响
        if (_currentCharacter.Animator != null)
            _currentCharacter.Animator.applyRootMotion = false;

        Debug.Log($"[PlayerController] 操控角色切换为: {(character != null ? character.name : "null")}");
    }

    /// <summary>把 Inspector 配置的动画速度写入 Animator 的速度参数</summary>
    private void ApplyAnimationSpeeds()
    {
        if (_currentCharacter?.Animator == null) return;
        var anim = _currentCharacter.Animator;

        // 攻速：三段攻击动画速度交给角色（乘攻速倍率后写入 AttackSpeed 参数，道具加攻速时角色自己 RefreshAttackSpeed）
        _currentCharacter.AttackAnimSpeeds = new float[] { attack1AnimSpeed, attack2AnimSpeed, attack3AnimSpeed };
        _currentCharacter.RefreshAttackSpeed();
        anim.SetFloat("JumpSpeed", jumpAnimSpeed);
        anim.SetFloat("BlockSpeed", blockAnimSpeed);
        anim.SetFloat("HitSpeed", hitAnimSpeed);
        anim.SetFloat("DieSpeed", dieAnimSpeed);
        anim.SetFloat("ShootSpeed", shootAnimSpeed);
        anim.SetFloat("AimSpeed", aimAnimSpeed);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Paused)
            return;

        if (_currentCharacter == null) return;
        if (_currentCharacter.Controller == null) return;

        CheckGrounded();
        HandleGravity();
        HandleLandingDetection();
        HandleMovement();
        HandleAttack();
        HandleBlock();
        HandleJump();

        _wasGrounded = _isGrounded;
    }

    /// <summary>检测是否着地（只用 CharacterController 的真实碰撞，不用射线兜底——保证落地动画在真正触地时触发）</summary>
    private void CheckGrounded()
    {
        var cc = _currentCharacter.Controller;
        _isGrounded = cc.isGrounded;

        // 同步到动画控制器
        if (_currentCharacter.Animator != null)
            _currentCharacter.Animator.SetBool(IsGroundedParam, _isGrounded);
    }

    /// <summary>处理重力和垂直移动</summary>
    private void HandleGravity()
    {
        if (_isGrounded && _verticalVelocity < 0f && !_isLanding)
        {
            _verticalVelocity = -2f;
        }
        else
        {
            _verticalVelocity += gravity * Time.deltaTime;
        }
    }

    /// <summary>检测落地：从空中 → 着地</summary>
    private void HandleLandingDetection()
    {
        // 刚落地
        if (!_wasGrounded && _isGrounded && _verticalVelocity < 0f)
        {
            _isLanding = true;
            _verticalVelocity = -2f;
            _jumpsUsed = 0;          // 落地重置跳跃次数，允许再次跳跃

            if (_currentCharacter.Animator != null)
            {
                _currentCharacter.Animator.SetTrigger(JumpLandParam);
                // 清掉残留的起跳 trigger：二段跳时状态机在 JumpLoop 无过渡消费 JumpStart，
                // 若残留会被落地后 Walk/Run/Idle → JumpStart 过渡误消费，导致落地后再次跳起悬浮动画
                _currentCharacter.Animator.ResetTrigger(JumpStartParam);
            }

            // 落地动画播完后解除标记
            Invoke(nameof(FinishLanding), landingDuration);
        }
    }

    /// <summary>落地动画播放完毕</summary>
    private void FinishLanding()
    {
        _isLanding = false;
    }

    /// <summary>处理移动输入（WASD / 方向键 / 左摇杆）</summary>
    private void HandleMovement()
    {
        var cc = _currentCharacter.Controller;

        // 攻击中移动处理（分层攻击，按是否按着移动键分两种）：
        // - 站立攻击（没按移动键）：锁定移动，等攻击动画播完（跟随攻击动画的跨步位移，脚不滑动）
        // - 移动攻击（按着移动键）：不锁定，继续移动——下半身照常跑步，上半身由 UpperBody 层播挥砍
        if (_currentCharacter is MeleeCharacter meleeChar && meleeChar.IsAttacking)
        {
            bool movingInput = Mathf.Abs(Input.GetAxis("Horizontal")) > 0.1f ||
                               Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f;
            if (!movingInput)
            {
                // Speed 归 0：站立攻击/移动攻击松开移动键时下半身立即回 Idle（避免原地跑步）
                if (_currentCharacter.Animator != null)
                    _currentCharacter.Animator.SetFloat(SpeedParam, 0f);

                // 水平跟随攻击动画的跨步位移（脚不滑动），垂直照常应用重力速度——跳跃中攻击也会正常下落/落地，不会悬浮
                var dp = _currentCharacter.Animator != null ? _currentCharacter.Animator.deltaPosition : Vector3.zero;
                cc.Move(new Vector3(dp.x, 0f, dp.z) + Vector3.up * _verticalVelocity * Time.deltaTime);
                return;
            }
            // 移动攻击：继续走下方移动逻辑（Speed 参数照常设置，下半身播跑步动画）
        }

        // Shift 切换冲刺模式（按一下开，再按一下关）
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            _isSprinting = !_isSprinting;
            Debug.Log($"[PlayerController] 冲刺{(_isSprinting ? "开启" : "关闭")}");
        }

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        _moveDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (_moveDirection.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(_moveDirection.x, _moveDirection.z) * Mathf.Rad2Deg;
            if (cameraTransform != null)
                targetAngle += cameraTransform.eulerAngles.y;

            // 远程角色瞄准时：角色朝向 = 相机朝向（枪口对准镜头方向），不随移动方向转
            bool isAiming = _currentCharacter is RangedCharacter && _isAiming;
            if (isAiming && cameraTransform != null)
            {
                _currentCharacter.transform.rotation =
                    Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
            }
            else
            {
                float angle = Mathf.SmoothDampAngle(
                    _currentCharacter.transform.eulerAngles.y,
                    targetAngle,
                    ref _currentRotationVelocity,
                    rotationSmoothTime
                );
                _currentCharacter.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }

            // 冲刺速度 = 角色移动速度 × 1.5；格挡中强制走路（不能冲刺）
            bool blocking = _currentCharacter.IsBlocking;
            float currentSpeed = (_isSprinting && !blocking)
                ? _currentCharacter.MoveSpeed * SprintMultiplier
                : _currentCharacter.MoveSpeed;

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            Vector3 motion = moveDir * currentSpeed + Vector3.up * _verticalVelocity;
            cc.Move(motion * Time.deltaTime);

            // 动画参数：走路 0.4（Walk 状态），冲刺 1.0（Run 状态，阈值 >0.5）；格挡中走路
            if (_currentCharacter.Animator != null)
            {
                _currentCharacter.Animator.SetFloat(SpeedParam, (_isSprinting && !blocking) ? 1f : WalkAnimSpeed);

                // 动画播放速度随实际移动速度变化：走路 1.0 倍 → 冲刺 sprintAnimSpeed 倍（线性插值）
                float sprintSpeed = _currentCharacter.MoveSpeed * SprintMultiplier;
                float animSpeed = Mathf.Lerp(1f, sprintAnimSpeed,
                    Mathf.InverseLerp(_currentCharacter.MoveSpeed, sprintSpeed, currentSpeed));
                _currentCharacter.Animator.SetFloat(AnimSpeedParam, animSpeed);

                // 远程角色瞄准移动方向（AimMove Blend Tree 用）
                if (_currentCharacter is RangedCharacter)
                {
                    _currentCharacter.Animator.SetFloat(AimXParam, horizontal);
                    _currentCharacter.Animator.SetFloat(AimZParam, vertical);
                }
            }
        }
        else
        {
            cc.Move(Vector3.up * _verticalVelocity * Time.deltaTime);

            if (_currentCharacter.Animator != null)
                _currentCharacter.Animator.SetFloat(SpeedParam, 0f);
        }
    }

    /// <summary>攻击（鼠标左键 / Ctrl）——按住自动连续攻击</summary>
    private void HandleAttack()
    {
        // 按住触发：点按 = 单发；按住 = 连续攻击
        // 近战：当前段动画播完（IsAttacking 解锁）后下一帧自动接下一段，直到松开
        // 远程：按攻击冷却自动连射
        if (!Input.GetMouseButton(0) && !Input.GetKey(KeyCode.LeftControl)) return;
        if (_currentCharacter == null || _currentCharacter.IsDead) return;
        if (!_currentCharacter.CanAttack) return;

        // 近战攻击锁：段动画播完前不触发下一段（解锁后按住状态会自动续段）
        if (_currentCharacter is MeleeCharacter melee && melee.IsAttacking) return;

        // 攻击前转向鼠标方向（连击时每段开始都面向鼠标）
        FaceMouseDirection();

        _currentCharacter.PerformAttack();
    }

    /// <summary>让角色面向鼠标在屏幕上的方向</summary>
    private void FaceMouseDirection()
    {
        if (cameraTransform == null) return;
        var cam = cameraTransform.GetComponent<Camera>();
        if (cam == null) return;

        var ray = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(Vector3.up, _currentCharacter.transform.position);
        if (plane.Raycast(ray, out float dist))
        {
            var hitPoint = ray.GetPoint(dist);
            var dir = hitPoint - _currentCharacter.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                var targetRot = Quaternion.LookRotation(dir.normalized);
                _currentCharacter.transform.rotation = targetRot;
            }
        }
    }

    /// <summary>格挡（按住鼠标右键）</summary>
    private void HandleBlock()
    {
        if (_currentCharacter == null) return;

        bool holding = Input.GetMouseButton(1);

        // 远程角色：右键瞄准（放大镜头 + 瞄准动画），不做格挡
        if (_currentCharacter is RangedCharacter)
        {
            _isAiming = holding;
            _currentCharacter.IsBlocking = false;
            if (cameraTransform != null)
            {
                var cam = cameraTransform.GetComponent<ThirdPersonCamera>();
                if (cam != null) cam.SetAiming(holding);
            }
            if (_currentCharacter.Animator != null)
                _currentCharacter.Animator.SetBool(IsAimingParam, holding);
            return;
        }

        // 近战角色：按住右键 = 格挡，松开 = 解除
        _currentCharacter.IsBlocking = holding;

        if (_currentCharacter.Animator != null)
            _currentCharacter.Animator.SetBool(IsBlockingParam, holding);
    }

    /// <summary>跳跃（Space，支持二段跳）</summary>
    private void HandleJump()
    {
        if (!Input.GetKeyDown(KeyCode.Space)) return;
        if (_currentCharacter == null) return;
        if (_isLanding) return;                       // 落地动画中不能跳
        if (_jumpsUsed == 0 && !_isGrounded) return;  // 第一跳必须在地面
        if (_jumpsUsed >= maxJumps) return;           // 跳跃次数用完（空中不能再跳）

        // 根据目标高度计算初速度: v = sqrt(2 * |g| * h)
        _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
        _jumpsUsed++;

        // 触发起跳动画（二段跳复用 JumpStart）
        if (_currentCharacter.Animator != null)
        {
            _currentCharacter.Animator.SetTrigger(JumpStartParam);
            _currentCharacter.Animator.SetBool(IsGroundedParam, false);
        }

        Debug.Log($"[PlayerController] 第{_jumpsUsed}段起跳！高度: {jumpHeight}m，滞空约 {2f * _verticalVelocity / Mathf.Abs(gravity):F1} 秒");
    }
}

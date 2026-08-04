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
    [SerializeField] private float jumpHeight = 3.5f;          // 跳跃高度（米），调大 = 空中更久
    [SerializeField] private float gravity = -12f;             // 重力加速度，调小 = 下落更慢
    [SerializeField] private float groundedCheckOffset = 0.1f; // 着地检测微小偏移
    [SerializeField] private LayerMask groundLayer = ~0;       // 地面层
    [SerializeField] private float landingDuration = 0.4f;     // 落地动画预估时长（秒）

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

    // Animator 参数 ID 缓存（比字符串快）
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
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
        Debug.Log($"[PlayerController] 操控角色切换为: {(character != null ? character.name : "null")}");
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
        HandleJump();

        _wasGrounded = _isGrounded;
    }

    /// <summary>检测是否着地</summary>
    private void CheckGrounded()
    {
        var cc = _currentCharacter.Controller;
        _isGrounded = cc.isGrounded;

        // 补充射线检测
        if (!_isGrounded)
        {
            Vector3 bottom = _currentCharacter.transform.position + cc.center
                - Vector3.up * (cc.height / 2f - cc.radius);
            float checkDist = cc.skinWidth + groundedCheckOffset;
            _isGrounded = Physics.Raycast(bottom, Vector3.down, checkDist, groundLayer);
        }

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

            if (_currentCharacter.Animator != null)
            {
                _currentCharacter.Animator.SetTrigger(JumpLandParam);
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

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        _moveDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (_moveDirection.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(_moveDirection.x, _moveDirection.z) * Mathf.Rad2Deg;
            if (cameraTransform != null)
                targetAngle += cameraTransform.eulerAngles.y;

            float angle = Mathf.SmoothDampAngle(
                _currentCharacter.transform.eulerAngles.y,
                targetAngle,
                ref _currentRotationVelocity,
                rotationSmoothTime
            );
            _currentCharacter.transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            Vector3 motion = moveDir * _currentCharacter.MoveSpeed + Vector3.up * _verticalVelocity;
            cc.Move(motion * Time.deltaTime);

            if (_currentCharacter.Animator != null)
                _currentCharacter.Animator.SetFloat(SpeedParam, _moveDirection.magnitude);
        }
        else
        {
            cc.Move(Vector3.up * _verticalVelocity * Time.deltaTime);

            if (_currentCharacter.Animator != null)
                _currentCharacter.Animator.SetFloat(SpeedParam, 0f);
        }
    }

    /// <summary>攻击（鼠标左键 / Ctrl）</summary>
    private void HandleAttack()
    {
        if (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.LeftControl)) return;
        if (_currentCharacter == null || _currentCharacter.IsDead) return;
        if (!_currentCharacter.CanAttack) return;

        _currentCharacter.PerformAttack();
    }

    /// <summary>跳跃（Space）</summary>
    private void HandleJump()
    {
        if (!Input.GetKeyDown(KeyCode.Space)) return;
        if (_currentCharacter == null) return;
        if (!_isGrounded) return;    // 空中不能跳
        if (_isLanding) return;      // 落地动画中不能跳

        // 根据目标高度计算初速度: v = sqrt(2 * |g| * h)
        _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);

        // 触发起跳动画
        if (_currentCharacter.Animator != null)
        {
            _currentCharacter.Animator.SetTrigger(JumpStartParam);
            _currentCharacter.Animator.SetBool(IsGroundedParam, false);
        }

        Debug.Log($"[PlayerController] 起跳！高度: {jumpHeight}m，滞空约 {2f * _verticalVelocity / Mathf.Abs(gravity):F1} 秒");
    }
}

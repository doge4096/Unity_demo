using UnityEngine;

/// <summary>
/// 第三人称相机（雨中冒险2风格）— 鼠标移动控制视角，始终跟随角色
/// 不依赖 Cinemachine，开箱即用
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("跟随目标")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Transform lookAtTarget;

    [Header("相机参数")]
    [SerializeField] private float distance = 4f;           // 相机距离
    [SerializeField] private float heightOffset = 2f;       // 高度偏移
    [SerializeField] private float rotationSpeed = 5f;      // 鼠标旋转灵敏度
    [SerializeField] private float minPitch = -30f;         // 最低俯角
    [SerializeField] private float maxPitch = 70f;          // 最高仰角

    [Header("碰撞检测")]
    [SerializeField] private LayerMask obstacleMask = ~0;   // 障碍物层
    [SerializeField] private float minDistance = 1.2f;      // 碰撞后的最小距离
    [SerializeField] private float radius = 0.3f;           // 球形检测半径

    [Header("平滑")]
    [SerializeField] private float positionSmoothTime = 0.05f;   // 位置跟随速度（小=紧跟，防角色偏出画面）
    [SerializeField] private float rotationSmoothTime = 0.1f;

    [Header("瞄准（远程角色右键）")]
    [SerializeField] private float aimFov = 40f;          // 瞄准时的视野角度（越小越放大）
    [SerializeField] private float aimDistance = 2f;      // 瞄准时的相机距离（拉近）

    // 内部状态
    private float _yaw;         // 水平旋转角
    private float _pitch;       // 垂直旋转角
    private bool _isAiming;     // 是否瞄准中
    private float _baseFov;     // 基准视野角度
    private Vector3 _currentVelocity;
    private float _targetDistance;

    private void Start()
    {
        // 初始角度对齐到跟随目标
        if (followTarget != null)
        {
            _yaw = followTarget.eulerAngles.y;
        }
        _targetDistance = distance;

        // 记录基准视野（瞄准时在此基础缩小）
        var cam = GetComponent<Camera>();
        if (cam != null) _baseFov = cam.fieldOfView;

        // 锁定鼠标
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>设置瞄准状态（远程角色按住右键时调用）</summary>
    public void SetAiming(bool aiming)
    {
        _isAiming = aiming;
    }

    private void LateUpdate()
    {
        // 始终跟随当前活跃角色（近战/远程切换时自动更新绑定）
        var playerCtrl = FindObjectOfType<PlayerController>();
        if (playerCtrl?.CurrentCharacter != null)
        {
            var target = playerCtrl.CurrentCharacter.transform;
            if (followTarget != target)
            {
                followTarget = target;
                lookAtTarget = target;
                _yaw = target.eulerAngles.y;
            }
        }

        if (followTarget == null) return;

        HandleInput();
        UpdateCameraPosition();
    }

    /// <summary>处理鼠标输入（移动鼠标直接转视角，RoR2 风格）</summary>
    private void HandleInput()
    {
        // 鼠标移动控制水平/垂直视角
        _yaw += Input.GetAxis("Mouse X") * rotationSpeed;
        _pitch -= Input.GetAxis("Mouse Y") * rotationSpeed;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

        // 滚轮缩放
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        _targetDistance -= scroll * 2f;
        _targetDistance = Mathf.Clamp(_targetDistance, minDistance, distance * 1.5f);
    }

    /// <summary>更新相机位置和朝向</summary>
    private void UpdateCameraPosition()
    {
        // 目标位置
        Vector3 targetPos = lookAtTarget != null ? lookAtTarget.position : followTarget.position;
        targetPos.y += heightOffset * 0.5f;

        // 瞄准时拉近相机 + 缩小视野（放大效果）
        float useDistance = _isAiming ? aimDistance : _targetDistance;
        var camComp = GetComponent<Camera>();
        if (camComp != null)
        {
            float targetFov = _isAiming ? aimFov : _baseFov;
            camComp.fieldOfView = Mathf.Lerp(camComp.fieldOfView, targetFov, Time.deltaTime * 8f);
        }

        // 计算理想相机位置
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 desiredPosition = targetPos - rotation * Vector3.forward * useDistance;

        // 障碍物检测：如果相机和角色之间有障碍物，拉近相机
        Vector3 direction = desiredPosition - targetPos;
        float checkDistance = direction.magnitude;

        if (Physics.SphereCast(targetPos, radius, direction.normalized, out RaycastHit hit,
            checkDistance, obstacleMask))
        {
            desiredPosition = targetPos + direction.normalized * (hit.distance - radius);
            desiredPosition = Vector3.ClampMagnitude(desiredPosition - targetPos, checkDistance) + targetPos;
        }

        // 平滑移动
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref _currentVelocity,
            positionSmoothTime
        );

        // 平滑旋转
        Quaternion targetRotation = Quaternion.LookRotation(targetPos - transform.position);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime / rotationSmoothTime
        );
    }

    /// <summary>切换跟随目标（角色切换时调用）</summary>
    public void SetFollowTarget(Transform newTarget)
    {
        followTarget = newTarget;
        lookAtTarget = newTarget;
    }
}

using UnityEngine;

/// <summary>
/// 轻量级第三人称相机 — 跟随目标并响应鼠标旋转
/// 不依赖 Cinemachine，开箱即用
/// 后续若需要更复杂镜头效果可替换为 Cinemachine FreeLook
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("跟随目标")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Transform lookAtTarget;

    [Header("相机参数")]
    [SerializeField] private float distance = 6f;           // 相机距离
    [SerializeField] private float heightOffset = 2f;       // 高度偏移
    [SerializeField] private float rotationSpeed = 3f;      // 旋转灵敏度
    [SerializeField] private float minPitch = -20f;         // 最低俯角
    [SerializeField] private float maxPitch = 60f;          // 最高仰角

    [Header("碰撞检测")]
    [SerializeField] private LayerMask obstacleMask = ~0;   // 障碍物层
    [SerializeField] private float minDistance = 1.5f;      // 碰撞后的最小距离
    [SerializeField] private float radius = 0.3f;           // 球形检测半径

    [Header("平滑")]
    [SerializeField] private float positionSmoothTime = 0.15f;
    [SerializeField] private float rotationSmoothTime = 0.1f;

    // 内部状态
    private float _yaw;         // 水平旋转角
    private float _pitch;       // 垂直旋转角
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

        // 锁定鼠标
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        // 自动查找活跃角色（选人后动态绑定）
        if (followTarget == null)
        {
            var playerCtrl = FindObjectOfType<PlayerController>();
            if (playerCtrl?.CurrentCharacter != null)
            {
                followTarget = playerCtrl.CurrentCharacter.transform;
                lookAtTarget = followTarget;
                _yaw = followTarget.eulerAngles.y;
            }
        }

        if (followTarget == null) return;

        HandleInput();
        UpdateCameraPosition();
    }

    /// <summary>处理鼠标输入</summary>
    private void HandleInput()
    {
        // 右键按住旋转视角（也可改为一直旋转）
        if (Input.GetMouseButton(1))
        {
            _yaw += Input.GetAxis("Mouse X") * rotationSpeed;
            _pitch -= Input.GetAxis("Mouse Y") * rotationSpeed;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

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

        // 计算理想相机位置
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 desiredPosition = targetPos - rotation * Vector3.forward * _targetDistance;

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

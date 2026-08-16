using UnityEngine;

/// <summary>
/// 远程角色 — 发射弹道攻击，低血量高输出
/// </summary>
public class RangedCharacter : CharacterBase
{
    [Header("远程专属属性")]
    [SerializeField] private GameObject projectilePrefab;        // 弹道预制体
    [SerializeField] private Transform firePoint;                // 发射点（枪口/弓弦位置）
    [SerializeField] private int projectilePoolSize = 15;
    [SerializeField] private float projectileSpeed = 20f;
    [SerializeField] private float projectileLifetime = 3f;

    [Header("瞄准")]
    [SerializeField] private LayerMask aimLayerMask = ~0;        // 射线瞄准检测层
    [SerializeField] private float maxAimDistance = 100f;

    private ObjectPool _projectilePool;

    protected override void Awake()
    {
        base.Awake();

        // 初始化弹道对象池
        if (projectilePrefab != null)
        {
            _projectilePool = new ObjectPool(projectilePrefab, projectilePoolSize);
        }
    }

    /// <summary>远程攻击：从对象池取出弹道并发射</summary>
    public override void PerformAttack()
    {
        if (!CanAttack) return;
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[RangedCharacter] 未设置弹道预制体");
            return;
        }

        ResetAttackCooldown();

        // 从对象池获取弹道
        GameObject projectileObj = _projectilePool.Get();

        // 设置发射位置和方向
        if (firePoint != null)
        {
            projectileObj.transform.position = firePoint.position;
            projectileObj.transform.rotation = firePoint.rotation;
        }
        else
        {
            projectileObj.transform.position = transform.position + transform.forward * 1f + Vector3.up * 1f;
            projectileObj.transform.rotation = transform.rotation;
        }

        // 初始化弹道
        var projectile = projectileObj.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.Init(attackDamage, projectileSpeed, projectileLifetime, gameObject);
        }
        else
        {
            // 预制体上没有 Projectile 组件 → 临时添加并初始化
            var tempProj = projectileObj.AddComponent<Projectile>();
            tempProj.Init(attackDamage, projectileSpeed, projectileLifetime, gameObject);
        }

        // 动画：FemaleAnimator 控制器的射击参数名是 Shoot（旧 RangedAnimator 才用 Attack）
        if (Animator != null)
        {
            Animator.SetTrigger("Shoot");
        }

        Debug.Log($"[RangedCharacter] 发射弹道 — 伤害: {attackDamage}");
    }

    /// <summary>获取屏幕中心瞄准的世界坐标（用于第三方辅助瞄准）</summary>
    public Vector3 GetAimTarget()
    {
        // 从相机中心发出射线
        var cam = Camera.main;
        if (cam == null) return transform.position + transform.forward * maxAimDistance;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimLayerMask))
        {
            return hit.point;
        }
        return ray.GetPoint(maxAimDistance);
    }

    private void OnDestroy()
    {
        _projectilePool?.Clear();
    }
}

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 近战武器 — 挂载到武器 GameObject 上（如剑、斧）
/// Swing() 执行扇形碰撞检测，在角色前方造成 AOE 伤害
/// 集成攻击特效（挥砍弧线 + 命中粒子）和攻击范围指示器
/// </summary>
public class MeleeWeapon : MonoBehaviour
{
    [Header("攻击参数")]
    [SerializeField] private float attackAngle = 60f;      // 扇形角度
    [SerializeField] private LayerMask targetLayerMask = ~0; // 攻击目标层

    [Header("特效引用")]
    [SerializeField] private MeleeAttackVFX attackVFX;      // 攻击特效管理器
    [SerializeField] private AttackRangeIndicator rangeIndicator; // 攻击范围指示器

    [Header("调试")]
    [SerializeField] private bool showGizmos = true;

    /// <summary>公开角度和范围，供外部读取</summary>
    public float AttackAngle => attackAngle;
    public AttackRangeIndicator RangeIndicator => rangeIndicator;

    private void Awake()
    {
        // 自动查找同 GameObject 上的组件
        if (attackVFX == null)
            attackVFX = GetComponent<MeleeAttackVFX>();
        if (rangeIndicator == null)
            rangeIndicator = GetComponent<AttackRangeIndicator>();
    }

    /// <summary>执行一次挥砍</summary>
    /// <param name="damage">基础伤害值</param>
    /// <param name="range">攻击半径</param>
    /// <param name="comboIndex">当前连击数（1/2/3）</param>
    public void Swing(int damage, float range, int comboIndex = 1)
    {
        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;

        // 播放挥砍特效
        if (attackVFX != null)
        {
            attackVFX.PlaySlashVFX(range, comboIndex);
        }

        // 球形范围检测（粗筛）
        Collider[] hits = Physics.OverlapSphere(origin, range, targetLayerMask);

        int hitCount = 0;
        // 记录已经打中的根对象，避免重复命中
        HashSet<Transform> hitRoots = new HashSet<Transform>();

        foreach (var hit in hits)
        {
            // 跳过自己和同一个根对象
            if (hit.transform.root == transform.root) continue;
            if (hitRoots.Contains(hit.transform.root)) continue;

            // 角度判定（精确筛选）——只看水平方向：
            // 武器挂点与目标存在高度差时（武器悬空/目标高低起伏），垂直夹角会把角度撑大导致打不中，
            // 地面战斗的扇形挥砍只关心水平朝向
            Vector3 dirToTarget = (hit.transform.position - origin).normalized;
            dirToTarget.y = 0f;
            float angle = Vector3.Angle(forward, dirToTarget);

            if (angle <= attackAngle / 2f)
            {
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    damageable.TakeDamage(damage, transform.root.gameObject);
                    hitRoots.Add(hit.transform.root);
                    hitCount++;

                    // 在命中位置播放命中特效
                    if (attackVFX != null)
                    {
                        attackVFX.PlayHitVFX(hit.transform.position, comboIndex);
                    }

                    // 击退效果
                    Rigidbody rb = hit.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        Vector3 knockbackDir = (hit.transform.position - origin).normalized;
                        knockbackDir.y = 0.1f;
                        rb.AddForce(knockbackDir * 5f, ForceMode.Impulse);
                    }
                }
            }
        }

        if (hitCount > 0)
        {
            // 通知准星等 UI 做命中反馈（箭头向外弹开）
            EventBus.Emit(EventBus.ON_MELEE_HIT, hitCount);
            Debug.Log($"[MeleeWeapon] 挥砍命中 {hitCount} 个目标，伤害: {damage}，连击: {comboIndex}");
        }
    }

    /// <summary>显示攻击范围指示器</summary>
    public void ShowRange()
    {
        if (rangeIndicator != null)
            rangeIndicator.Show();
    }

    /// <summary>隐藏攻击范围指示器</summary>
    public void HideRange()
    {
        if (rangeIndicator != null)
            rangeIndicator.Hide();
    }

    /// <summary>更新范围指示器的角度和半径</summary>
    public void UpdateRangeIndicator(float angle, float radius)
    {
        attackAngle = angle;
        if (rangeIndicator != null)
            rangeIndicator.UpdateArc(angle, radius);
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // 绘制攻击扇形（仅编辑器可见）
        Gizmos.color = new Color(1f, 0f, 0.5f, 0.3f);
        Vector3 forward = transform.forward;
        Vector3 origin = transform.position;

        float halfAngle = attackAngle / 2f;
        float range = 2f; // 默认显示范围

        // 用多条线段近似扇形
        int segments = 20;
        Vector3 prevPoint = origin + Quaternion.Euler(0f, -halfAngle, 0f) * forward * range;
        for (int i = 1; i <= segments; i++)
        {
            float angle = -halfAngle + (attackAngle / segments) * i;
            Vector3 nextPoint = origin + Quaternion.Euler(0f, angle, 0f) * forward * range;
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
        Gizmos.DrawLine(origin, origin + Quaternion.Euler(0f, -halfAngle, 0f) * forward * range);
        Gizmos.DrawLine(origin, origin + Quaternion.Euler(0f, halfAngle, 0f) * forward * range);
    }
}

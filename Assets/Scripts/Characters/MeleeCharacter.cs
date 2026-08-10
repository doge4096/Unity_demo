using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 近战角色 — 扇形范围攻击，高血量高防御
/// 攻击检测以角色自身为原点（武器即角色模型自带，不依赖单独的武器组件）
/// </summary>
public class MeleeCharacter : CharacterBase
{
    [Header("近战专属属性")]
    [SerializeField] private float attackAngle = 60f;    // 攻击扇形角度
    [SerializeField] private int comboMax = 3;           // 最大连击数
    [SerializeField] private MeleeAttackVFX attackVFX;   // 攻击特效（白色刀光 + 命中火花）

    private int _currentCombo = 0;
    private float _comboResetTime = 1.5f;               // 连击重置时间
    private float _lastComboTime;

    /// <summary>攻击中（锁定移动，等动画播完再动）</summary>
    public bool IsAttacking { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        // 特效组件自动获取：场景未挂载时直接创建到角色身上（刀光以角色为原点发出）
        if (attackVFX == null)
            attackVFX = GetComponent<MeleeAttackVFX>();
        if (attackVFX == null)
            attackVFX = gameObject.AddComponent<MeleeAttackVFX>();
    }

    /// <summary>近战攻击：扇形挥砍</summary>
    public override void PerformAttack()
    {
        // 攻击中不能再次攻击（第一段没播完不能触发下一段，连击不取消当前段）
        if (IsAttacking) return;
        if (!CanAttack) return;
        ResetAttackCooldown();

        // 连击计数
        if (Time.time - _lastComboTime > _comboResetTime)
            _currentCombo = 0;

        _currentCombo = (_currentCombo % comboMax) + 1;
        _lastComboTime = Time.time;

        // 连击伤害递增：第1击 100%、第2击 120%、第3击 150%
        float comboMultiplier = 1f + (_currentCombo - 1) * 0.2f;
        int finalDamage = Mathf.RoundToInt(attackDamage * comboMultiplier);

        // 命中检测延迟到刀光出现时刻再判定（与刀光视觉同步：先亮刀光、后结算伤害，避免判定早于刀光）
        float hitDelay = attackVFX != null ? attackVFX.GetSlashDelay(_currentCombo) : 0f;
        StartCoroutine(DelayedMeleeHit(finalDamage, _currentCombo, hitDelay));

        // 白色刀光特效：大小随攻击范围缩放（攻击范围道具改 AttackRange 后刀光自动变大）
        if (attackVFX != null)
            attackVFX.PlaySlashVFX(attackRange, _currentCombo);

        // 动画（每段攻击速度单独配置：第1/2/3段 × 攻速倍率）
        if (Animator != null)
        {
            RefreshAttackSpeed();
            Animator.SetInteger("Combo", _currentCombo);
            Animator.SetTrigger("Attack");
        }

        // 攻击锁定：动画播放期间不能移动（连击会重新锁定）
        IsAttacking = true;
        if (gameObject.activeInHierarchy)
            StartCoroutine(UnlockAttack());

        Debug.Log($"[MeleeCharacter] 第{_currentCombo}击！伤害: {finalDamage}");
    }

    /// <summary>
    /// 攻击动画播完后解锁移动和连击（跟随动画实际时长，不硬编码）
    /// 流程：等状态切入攻击段 → 等动画播完（状态切走或 normalizedTime 到 100%）→ 过渡收尾
    /// 注意：站立攻击走 Base Layer（Attack1/2/3），移动攻击走 UpperBody 层（UAttack1/2/3），两层都要查
    /// </summary>
    private System.Collections.IEnumerator UnlockAttack()
    {
        var anim = Animator;

        // 没有 Animator 时退回保守等待
        if (anim == null)
        {
            yield return new WaitForSeconds(1.0f);
            IsAttacking = false;
            yield break;
        }

        // 阶段1：等 Base 层或 UpperBody 层切入攻击段（SetTrigger 后状态切换有过渡延迟）
        float t = Time.time;
        while (!IsAttackState(anim.GetCurrentAnimatorStateInfo(0)) &&
               !IsAttackState(anim.GetCurrentAnimatorStateInfo(1)))
        {
            if (Time.time - t > 0.5f) break;   // 超时兜底：没切进去就保守解锁
            yield return null;
        }

        // 阶段2：等攻击段播完（状态切走，或攻击动画播到 100%）
        // 用 normalizedTime 判定：非循环攻击动画播完后停在最后帧，状态可能不切走（UpperBody 层无回退过渡时）
        t = Time.time;
        while (IsAttackPlaying(anim.GetCurrentAnimatorStateInfo(0)) ||
               IsAttackPlaying(anim.GetCurrentAnimatorStateInfo(1)))
        {
            if (Time.time - t > 2.5f) break;   // 超时兜底：防状态卡死（如动画被打断）
            yield return null;
        }

        // 阶段3：状态过渡收尾，避免下一段叠在上段尾巴上
        yield return new WaitForSeconds(0.1f);
        IsAttacking = false;
    }

    /// <summary>按当前连击段设置攻击动画速度（第1/2/3段单独速度 × 攻速倍率）</summary>
    public override void RefreshAttackSpeed()
    {
        if (Animator != null && attackAnimSpeeds.Length > 0)
        {
            int idx = Mathf.Clamp(_currentCombo - 1, 0, attackAnimSpeeds.Length - 1);
            Animator.SetFloat("AttackSpeed", attackAnimSpeeds[idx] * AttackSpeedMultiplier);
        }
    }

    /// <summary>该层当前是否处于近战攻击段（站姿 Attack1/2/3，移动攻击 UAttack1/2/3）</summary>
    private static bool IsAttackState(AnimatorStateInfo info)
    {
        return info.IsName("Attack1") || info.IsName("Attack2") || info.IsName("Attack3") ||
               info.IsName("UAttack1") || info.IsName("UAttack2") || info.IsName("UAttack3");
    }

    /// <summary>该层是否正在播放攻击段且尚未播完（normalizedTime < 100%）</summary>
    private static bool IsAttackPlaying(AnimatorStateInfo info)
    {
        return IsAttackState(info) && info.normalizedTime < 1f;
    }

    /// <summary>等待刀光延迟后执行命中判定（判定与刀光出现同时刻；第三段两下刀光各判定一次）</summary>
    private System.Collections.IEnumerator DelayedMeleeHit(int damage, int comboIndex, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        // 延迟期间角色可能被销毁（场景切换等），先做空引用保护
        if (this == null || !gameObject.activeInHierarchy) yield break;

        // 第一下刀光的判定
        PerformBasicMelee(damage, comboIndex);

        // 第三段左右两下横砍：第二下刀光出现时再判定一次（伤害结算两段）
        if (comboIndex == 3 && attackVFX != null)
        {
            float secondDelay = attackVFX.GetSlashDelay3Second();
            if (secondDelay > 0f)
                yield return new WaitForSeconds(secondDelay);
            if (this == null || !gameObject.activeInHierarchy) yield break;
            PerformBasicMelee(damage, comboIndex);
        }
    }

    /// <summary>
    /// 近战命中检测：判定几何与刀光完全一致——以角色为中心、半径 = 刀光弧心距离、
    /// 张角 = 刀光张角的扇形。「角色中心到刀光的这段范围内」全部命中，贴脸也必中
    /// </summary>
    private void PerformBasicMelee(int damage, int comboIndex)
    {
        // 判定半径 = 刀光弧心到角色的距离（攻击范围道具改 AttackRange 后，判定与刀光同步变大）
        float slashRadius = attackVFX != null ? attackVFX.GetSlashRadius(attackRange) : attackRange;
        // 判定张角 = 刀光张角（当前 140°，左右各 70°）
        float halfAngle = attackVFX != null ? attackVFX.GetSlashArcAngle() / 2f : attackAngle / 2f;

        Collider[] hits = Physics.OverlapSphere(transform.position, slashRadius);

        int hitCount = 0;
        // 记录已命中的根对象，避免重复命中同一目标
        HashSet<Transform> hitRoots = new HashSet<Transform>();

        foreach (var hit in hits)
        {
            // 跳过自己
            if (hit.transform.root == transform.root) continue;
            if (hitRoots.Contains(hit.transform.root)) continue;

            // 只看水平距离和水平夹角（高低差不影响命中——刀光横扫高度范围内都算）
            Vector3 flat = hit.transform.position - transform.position;
            flat.y = 0f;
            float flatDist = flat.magnitude;
            if (flatDist > slashRadius) continue;  // 超出刀光距离不命中
            if (flatDist > 0.001f && Vector3.Angle(transform.forward, flat.normalized) > halfAngle) continue;
            // flatDist ≈ 0（完全贴脸）：方向向量退化，直接命中

            var damageable = hit.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.TakeDamage(damage, gameObject);
                hitRoots.Add(hit.transform.root);
                hitCount++;

                // 在命中位置播放火花特效
                if (attackVFX != null)
                    attackVFX.PlayHitVFX(hit.transform.position, comboIndex);
            }
        }

        if (hitCount > 0)
        {
            // 通知准星等 UI 做命中反馈（箭头向外弹开）
            EventBus.Emit(EventBus.ON_MELEE_HIT, hitCount);
            Debug.Log($"[MeleeCharacter] 命中 {hitCount} 个目标，伤害: {damage}");
        }
    }

    /// <summary>用 ScriptableObject 数据初始化</summary>
    public override void InitFromData(CharacterData data)
    {
        base.InitFromData(data);
    }
}

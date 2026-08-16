using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 角色基类 — 玩家和敌人的公共抽象
/// 定义血量、速度、攻击等核心属性，子类实现具体攻击逻辑
/// </summary>
public abstract class CharacterBase : MonoBehaviour, IDamageable
{
    [Header("基础属性")]
    [SerializeField] protected int maxHealth = 100;
    [SerializeField] protected int attackDamage = 15;
    [SerializeField] protected float moveSpeed = 2f; // 走路移动速度（匹配走路动画步速 ~2 m/s，脚不滑；冲刺 × 2.4）
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float attackCooldown = 0.5f;
    [SerializeField] protected float defense = 0f;

    [Header("攻速(道具/Buff 可修改)")]
    [SerializeField] protected float attackSpeedMultiplier = 1f;   // 攻速倍率:1=原始速度,2=两倍速(道具/技能加成)
    [SerializeField] protected float[] attackAnimSpeeds = new float[3] { 1f, 1f, 1f };  // 第1/2/3段攻击动画基础速度(PlayerController 配置,乘倍率后写入 AttackSpeed 参数)

    [Header("运行时状态")]
    [SerializeField] protected int currentHealth;
    protected float lastAttackTime = -999f;

    [Header("引用")]
    public Animator Animator;       // 等导入模型后拖入，当前可为 null
    public CharacterController Controller;

    [Header("格挡")]
    public bool IsBlocking;         // 格挡状态（PlayerController 按住右键控制），格挡时减免 90% 伤害

    // 公开属性（Buff 系统需要读写）
    public int MaxHealth
    {
        get => maxHealth;
        set => maxHealth = Mathf.Max(1, value);
    }
    public int AttackDamage
    {
        get => attackDamage;
        set => attackDamage = Mathf.Max(0, value);
    }
    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0.1f, value);
    }
    public float AttackRange
    {
        get => attackRange;
        set => attackRange = Mathf.Max(0.1f, value);
    }
    public float AttackCooldown
    {
        get => attackCooldown;
        set => attackCooldown = Mathf.Max(0.1f, value);
    }
    public float Defense
    {
        get => defense;
        set => defense = Mathf.Max(0f, value);
    }
    public float AttackSpeedMultiplier
    {
        get => attackSpeedMultiplier;
        set => attackSpeedMultiplier = Mathf.Max(0.1f, value);
    }
    /// <summary>第1/2/3段攻击动画基础速度（PlayerController 配置，乘攻速倍率后写入 AttackSpeed 参数）</summary>
    public float[] AttackAnimSpeeds
    {
        get => attackAnimSpeeds;
        set => attackAnimSpeeds = value;
    }

    /// <summary>实际攻击冷却 = 基础冷却 ÷ 攻速倍率（攻速越快，攻击间隔越短）</summary>
    public float EffectiveAttackCooldown => attackCooldown / Mathf.Max(0.1f, attackSpeedMultiplier);

    /// <summary>攻速倍率变化后调用：重算 AttackSpeed 动画参数（动画播放速度跟随攻速）。默认按第1段速度，子类可覆写按当前段设置</summary>
    public virtual void RefreshAttackSpeed()
    {
        if (Animator == null || attackAnimSpeeds.Length == 0) return;
        // 控制器没有 AttackSpeed 参数时跳过（远程角色 FemaleAnimator 用的是 AnimSpeed，不写 AttackSpeed 防刷警告）
        if (HasAnimatorParameter(Animator, "AttackSpeed"))
            Animator.SetFloat("AttackSpeed", attackAnimSpeeds[0] * attackSpeedMultiplier);
    }

    /// <summary>检查 Animator 是否存在指定参数（避免 SetFloat 不存在的参数刷警告）</summary>
    private static bool HasAnimatorParameter(Animator anim, string paramName)
    {
        foreach (var p in anim.parameters)
        {
            if (p.name == paramName) return true;
        }
        return false;
    }

    public int CurrentHealth => currentHealth;
    public float HealthPercent => (float)currentHealth / maxHealth;
    public bool IsDead => currentHealth <= 0;
    public bool CanAttack => Time.time - lastAttackTime >= EffectiveAttackCooldown;

    // 事件（可用 EventBus 代替，保留 UnityEvent 便于编辑器连线）
    public UnityEvent OnDamaged;
    public UnityEvent OnDied;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;

        if (Controller == null)
            Controller = GetComponent<CharacterController>();

        if (Animator == null)
            Animator = GetComponentInChildren<Animator>();

        // 强制关闭 root motion：动画不驱动角色位移（位移由 PlayerController 控制），
        // 避免角色被动画带着跑、相机跟着异常移动
        if (Animator != null)
            Animator.applyRootMotion = false;
    }

    /// <summary>受到伤害（IDamageable 实现）</summary>
    public virtual void TakeDamage(int amount, GameObject source = null)
    {
        if (IsDead) return;

        // 格挡：减免 90% 伤害，播放格挡受击动画（Block 状态内触发 Hit → BlockHit）
        if (IsBlocking)
        {
            int blockedDamage = Mathf.Max(1, Mathf.RoundToInt(amount * 0.1f));
            currentHealth -= blockedDamage;
            OnDamaged?.Invoke();

            if (Animator != null)
                Animator.SetTrigger("Hit");

            Debug.Log($"[{gameObject.name}] 格挡！受到 {blockedDamage} 点伤害（减免90%）HP: {currentHealth}/{maxHealth}");

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Die();
            }
            return;
        }

        // 防御减伤：每点防御减少 0.5% 伤害，最多减免 75%
        float reduction = Mathf.Clamp(defense * 0.005f, 0f, 0.75f);
        int finalDamage = Mathf.RoundToInt(amount * (1f - reduction));
        finalDamage = Mathf.Max(1, finalDamage);

        currentHealth -= finalDamage;
        OnDamaged?.Invoke();

        // 受击动画
        if (Animator != null)
        {
            Animator.SetTrigger("Hit");
        }

        // 受击红色闪烁效果
        StartCoroutine(HitFlash());

        Debug.Log($"[{gameObject.name}] 受到 {finalDamage} 点伤害 (原始 {amount}), HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    /// <summary>受击红色闪烁协程</summary>
    private System.Collections.IEnumerator HitFlash()
    {
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer == null) yield break;

        Color originalColor = renderer.material.color;
        Color flashColor = Color.red;

        // 闪红两次
        for (int i = 0; i < 2; i++)
        {
            renderer.material.color = flashColor;
            yield return new WaitForSeconds(0.08f);
            renderer.material.color = originalColor;
            yield return new WaitForSeconds(0.08f);
        }
    }

    /// <summary>治疗</summary>
    public virtual void Heal(int amount)
    {
        if (IsDead) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        Debug.Log($"[{gameObject.name}] 恢复 {amount} 点生命, HP: {currentHealth}/{maxHealth}");
    }

    /// <summary>死亡处理</summary>
    protected virtual void Die()
    {
        OnDied?.Invoke();
        Debug.Log($"[{gameObject.name}] 已死亡");

        // 播放死亡动画
        if (Animator != null)
        {
            Animator.SetBool("IsDead", true);
            Animator.SetTrigger("Die");
        }
    }

    /// <summary>执行攻击（子类必须实现）</summary>
    public abstract void PerformAttack();

    /// <summary>重置攻击冷却计时器</summary>
    protected void ResetAttackCooldown()
    {
        lastAttackTime = Time.time;
    }

    /// <summary>用 ScriptableObject 数据初始化角色属性</summary>
    public virtual void InitFromData(CharacterData data)
    {
        maxHealth = data.maxHealth;
        attackDamage = data.attackDamage;
        moveSpeed = data.moveSpeed;
        attackRange = data.attackRange;
        attackCooldown = data.attackCooldown;
        defense = data.defense;
        currentHealth = maxHealth;
    }
}

using UnityEngine;

/// <summary>
/// 近战角色 — 扇形范围攻击，高血量高防御
/// 按住 Tab 键显示攻击范围指示器
/// </summary>
public class MeleeCharacter : CharacterBase
{
    [Header("近战专属属性")]
    [SerializeField] private MeleeWeapon weapon;
    [SerializeField] private int comboMax = 3;          // 最大连击数

    [Header("范围显示")]
    [SerializeField] private KeyCode rangeShowKey = KeyCode.Tab;  // 按住显示范围的热键
    [SerializeField] private bool alwaysShowRange = false;        // 是否始终显示范围

    private int _currentCombo = 0;
    private float _comboResetTime = 1.5f;               // 连击重置时间
    private float _lastComboTime;

    protected override void Awake()
    {
        base.Awake();

        if (weapon == null)
            weapon = GetComponentInChildren<MeleeWeapon>();
    }

    private void Start()
    {
        // 同步武器范围指示器参数
        if (weapon != null)
        {
            weapon.UpdateRangeIndicator(weapon.AttackAngle, attackRange);
        }
    }

    private void Update()
    {
        // 范围指示器显示控制
        if (weapon != null)
        {
            bool shouldShow = alwaysShowRange || Input.GetKey(rangeShowKey);
            if (shouldShow)
                weapon.ShowRange();
            else
                weapon.HideRange();
        }
    }

    /// <summary>近战攻击：扇形挥砍</summary>
    public override void PerformAttack()
    {
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

        // 执行武器挥砍（传入连击数）
        if (weapon != null)
        {
            weapon.Swing(finalDamage, attackRange, _currentCombo);
        }
        else
        {
            // 无武器时用简单的 OverlapSphere 做兜底
            PerformBasicMelee(finalDamage);
        }

        // 动画
        if (Animator != null)
        {
            Animator.SetInteger("Combo", _currentCombo);
            Animator.SetTrigger("Attack");
        }

        Debug.Log($"[MeleeCharacter] 第{_currentCombo}击！伤害: {finalDamage}");
    }

    /// <summary>简易近战检测（没有 MeleeWeapon 组件时的兜底方案）</summary>
    private void PerformBasicMelee(int damage)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * attackRange * 0.5f,
            attackRange);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue; // 不打自己

            var damageable = hit.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.TakeDamage(damage, gameObject);
            }
        }
    }

    /// <summary>用 ScriptableObject 数据初始化</summary>
    public override void InitFromData(CharacterData data)
    {
        base.InitFromData(data);

        // 同步范围指示器
        if (weapon != null)
        {
            weapon.UpdateRangeIndicator(weapon.AttackAngle, attackRange);
        }
    }
}

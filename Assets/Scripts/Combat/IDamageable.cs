using UnityEngine;

/// <summary>
/// 可受伤接口 — 玩家、敌人、可破坏物均实现此接口
/// </summary>
public interface IDamageable
{
    /// <summary>受到伤害</summary>
    /// <param name="amount">伤害数值</param>
    /// <param name="source">伤害来源（可选，用于击杀统计等）</param>
    void TakeDamage(int amount, GameObject source = null);

    /// <summary>是否已死亡</summary>
    bool IsDead { get; }
}

using UnityEngine;

/// <summary>
/// 技能基类（ScriptableObject）— 定义技能的冷却、伤害倍率、消耗等
/// 右键 → Create → Roguelike → Skill Data 创建具体技能
/// </summary>
[CreateAssetMenu(menuName = "Roguelike/Skill Data", fileName = "NewSkill")]
public class SkillBase : ScriptableObject
{
    [Header("基本信息")]
    public string skillName = "新技能";
    [TextArea(2, 4)]
    public string description = "技能描述";
    public Sprite icon;

    [Header("数值")]
    public float cooldown = 3f;           // 冷却时间（秒）
    public float damageMultiplier = 1f;   // 伤害倍率
    public int manaCost = 0;              // 魔法消耗
    public float rangeBonus = 0f;         // 范围加成

    [Header("特殊效果")]
    public bool hasKnockback;             // 是否击退
    public float knockbackForce = 5f;
    public bool hasStun;                  // 是否眩晕
    public float stunDuration = 1f;
    public GameObject visualEffectPrefab; // 技能特效预制体（可选）

    /// <summary>执行技能</summary>
    /// <param name="user">施法者</param>
    /// <param name="targetPosition">目标位置（远程瞄准点）</param>
    public virtual void Execute(CharacterBase user, Vector3 targetPosition)
    {
        if (user == null)
        {
            Debug.LogWarning($"[SkillBase] 技能 {skillName} 执行失败：施法者为空");
            return;
        }

        Debug.Log($"[SkillBase] {user.name} 释放技能: {skillName}");

        // 播放特效
        if (visualEffectPrefab != null)
        {
            var effect = Instantiate(visualEffectPrefab, user.transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }

        // 子类可重写此方法实现具体逻辑
    }

    /// <summary>获取技能实际伤害</summary>
    public int GetDamage(int baseDamage)
    {
        return Mathf.RoundToInt(baseDamage * damageMultiplier);
    }
}

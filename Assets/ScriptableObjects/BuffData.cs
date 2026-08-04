using UnityEngine;

/// <summary>
/// Buff 类型枚举
/// </summary>
public enum BuffType
{
    AttackUp,     // 攻击力提升
    DefenseUp,    // 防御力提升
    SpeedUp,      // 移动速度提升
    MaxHPUp,      // 最大血量提升
    LifeSteal,    // 吸血
    CooldownReduction, // 冷却缩减
    RangeUp,      // 攻击范围提升
}

/// <summary>
/// Buff 数据配置（ScriptableObject）— 定义肉鸽奖励选项
/// 右键 → Create → Roguelike → Buff Data 创建
/// </summary>
[CreateAssetMenu(menuName = "Roguelike/Buff Data", fileName = "NewBuff")]
public class BuffData : ScriptableObject
{
    [Header("基本信息")]
    public string buffName = "新 Buff";
    [TextArea(2, 4)]
    public string description = "Buff 效果描述";
    public Sprite icon;
    public BuffType type = BuffType.AttackUp;

    [Header("数值")]
    public float value = 10f;             // 数值（具体含义由 type 决定）
    public bool isStackable = true;       // 是否可叠加

    [Header("稀有度")]
    public Rarity rarity = Rarity.Common;

    /// <summary>稀有度</summary>
    public enum Rarity
    {
        Common,     // 白（60% 概率）
        Rare,       // 蓝（25% 概率）
        Epic,       // 紫（12% 概率）
        Legendary   // 金（3% 概率）
    }

    /// <summary>获取整数值（AttackUp、DefenseUp 等需要取整）</summary>
    public int GetValueAsInt()
    {
        return Mathf.RoundToInt(value);
    }

    /// <summary>生成格式化的描述文本</summary>
    public string GetFormattedDescription()
    {
        string prefix = type switch
        {
            BuffType.AttackUp => $"攻击力 +{GetValueAsInt()}",
            BuffType.DefenseUp => $"防御力 +{GetValueAsInt()}",
            BuffType.SpeedUp => $"移动速度 +{value:F1}",
            BuffType.MaxHPUp => $"最大生命 +{GetValueAsInt()}",
            BuffType.LifeSteal => $"吸血 {value:P0}",
            BuffType.CooldownReduction => $"冷却缩减 {value:P0}",
            BuffType.RangeUp => $"攻击范围 +{value:P0}",
            _ => $"{buffName}"
        };
        return isStackable ? prefix : $"{prefix} (不可叠加)";
    }
}

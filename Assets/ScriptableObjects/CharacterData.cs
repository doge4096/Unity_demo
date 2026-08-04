using UnityEngine;

/// <summary>
/// 角色属性配置（ScriptableObject）— 定义近战/远程角色的初始数值
/// 右键 → Create → Roguelike → Character Data 创建
/// </summary>
[CreateAssetMenu(menuName = "Roguelike/Character Data", fileName = "CharacterData")]
public class CharacterData : ScriptableObject
{
    [Header("基本信息")]
    public string characterName = "新角色";
    public CharacterType characterType = CharacterType.Melee;

    [Header("展示信息（选人界面）")]
    public Sprite portrait;                          // 角色立绘/头像
    [TextArea(3, 5)]
    public string description = "角色简介，描述战斗风格和特点";

    [Header("绑定武器")]
    public string weaponName = "武器名称";             // 武器名
    [TextArea(2, 3)]
    public string weaponDescription = "武器简介";       // 武器描述（悬停时显示）

    [Header("战斗属性")]
    public int maxHealth = 100;
    public int attackDamage = 15;
    public float attackRange = 2f;
    public float attackCooldown = 0.5f;
    public float defense = 0f;

    [Header("移动")]
    public float moveSpeed = 5f;

    /// <summary>角色类型枚举</summary>
    public enum CharacterType
    {
        Melee,   // 近战
        Ranged   // 远程
    }
}

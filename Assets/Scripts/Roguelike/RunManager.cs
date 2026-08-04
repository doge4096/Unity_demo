using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单局流程管理器 — 控制房间推进、难度递增、奖励触发
/// 挂载到 GameManager 同一 GameObject 上
/// </summary>
public class RunManager : MonoBehaviour
{
    [Header("难度设置")]
    [SerializeField] private float difficultyScalePerRoom = 0.05f; // 每过一个房间，敌人属性 +5%
    [SerializeField] private int roomsPerBoss = 10;                // 每 N 个房间出现 Boss

    [Header("引用")]
    [SerializeField] private DungeonGenerator dungeonGenerator;
    [SerializeField] private RewardSelector rewardSelector;

    // 运行时状态
    private List<RoomNode> _roomQueue = new();      // 剩余房间队列
    private int _currentRoomIndex = 0;
    private int _enemiesRemainingInRoom = 0;
    private float _difficultyMultiplier = 1f;
    private List<BuffData> _selectedBuffs = new();  // 本轮已选 Buff

    // 属性
    public int CurrentRoomIndex => _currentRoomIndex;
    public int TotalRooms => _roomQueue.Count;
    public float DifficultyMultiplier => _difficultyMultiplier;
    public List<BuffData> SelectedBuffs => _selectedBuffs;
    public bool IsRunActive { get; private set; }

    private void Awake()
    {
        if (dungeonGenerator == null)
            dungeonGenerator = GetComponent<DungeonGenerator>();

        if (rewardSelector == null)
            rewardSelector = GetComponent<RewardSelector>();
    }

    /// <summary>开始一次新的 Run</summary>
    /// <param name="seed">地牢随机种子</param>
    public void StartRun(int seed = -1)
    {
        if (seed < 0) seed = Random.Range(0, int.MaxValue);

        // 重置状态
        _currentRoomIndex = 0;
        _difficultyMultiplier = 1f;
        _selectedBuffs.Clear();
        IsRunActive = true;

        // 生成地牢
        dungeonGenerator.Generate(seed);
        _roomQueue = new List<RoomNode>(dungeonGenerator.AllRooms);
        ShuffleRooms();

        Debug.Log($"[RunManager] 新 Run 开始 — Seed: {seed}, 共 {_roomQueue.Count} 个房间");

        // 进入第一个房间
        EnterRoom(0);
    }

    /// <summary>进入指定索引的房间</summary>
    public void EnterRoom(int index)
    {
        if (index >= _roomQueue.Count)
        {
            // 所有房间清完 → 胜利
            Debug.Log("[RunManager] 所有房间已清空！通关！");
            GameManager.Instance.GameOver(true);
            return;
        }

        _currentRoomIndex = index;
        var room = _roomQueue[index];

        Debug.Log($"[RunManager] 进入房间 {index + 1}/{_roomQueue.Count} — 难度倍率: {_difficultyMultiplier:F2}");

        // 在该房间生成敌人（由 DungeonGenerator 负责具体内容生成）
        SpawnEnemiesInRoom(room);

        // 通知 UI
        EventBus.Emit(EventBus.ON_RUN_STARTED, _currentRoomIndex);
    }

    /// <summary>在房间内生成敌人</summary>
    private void SpawnEnemiesInRoom(RoomNode room)
    {
        // 根据难度决定敌人数
        int baseEnemies = Random.Range(1, 5);
        int count = Mathf.RoundToInt(baseEnemies * _difficultyMultiplier);
        count = Mathf.Clamp(count, 1, 8);

        _enemiesRemainingInRoom = count;

        // TODO: 从敌人预制体池中随机生成
        // 当前为框架代码，后续接入敌人系统
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = room.GetWorldPosition() +
                new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            Debug.Log($"[RunManager] 生成敌人 {i + 1}/{count} 于 {spawnPos}");
            // Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        }
    }

    /// <summary>敌人被击杀时调用</summary>
    public void OnEnemyKilled(GameObject enemy)
    {
        _enemiesRemainingInRoom--;

        if (_enemiesRemainingInRoom <= 0)
        {
            OnRoomCleared();
        }
    }

    /// <summary>房间清空时调用</summary>
    private void OnRoomCleared()
    {
        Debug.Log($"[RunManager] 房间 {_currentRoomIndex + 1} 已清空！");

        // 触发奖励选择
        EventBus.Emit(EventBus.ON_ROOM_CLEARED, _currentRoomIndex);

        // 奖励选择（由 RewardSelector 处理UI + 选择逻辑）
        rewardSelector?.ShowRewards();
    }

    /// <summary>玩家选择奖励后 → 进入下一房间</summary>
    public void OnRewardSelected(BuffData selectedBuff)
    {
        if (selectedBuff != null)
        {
            ApplyBuff(selectedBuff);
        }

        // 提高难度
        _difficultyMultiplier += difficultyScalePerRoom;

        // 进入下一房间
        EnterRoom(_currentRoomIndex + 1);
    }

    /// <summary>应用 Buff 效果到当前角色</summary>
    private void ApplyBuff(BuffData buff)
    {
        _selectedBuffs.Add(buff);

        // 找到当前操控的角色并应用 Buff
        var player = FindObjectOfType<PlayerController>();
        if (player == null) return;

        var activeChar = player.CurrentCharacter;
        if (activeChar == null) return;

        ApplyBuffToCharacter(activeChar, buff);

        EventBus.Emit(EventBus.ON_BUFF_SELECTED, buff);
        Debug.Log($"[RunManager] 应用 Buff: {buff.buffName} (当前共 {_selectedBuffs.Count} 个)");
    }

    /// <summary>将 Buff 效果应用到指定角色</summary>
    private void ApplyBuffToCharacter(CharacterBase character, BuffData buff)
    {
        if (character == null) return;

        switch (buff.type)
        {
            case BuffType.AttackUp:
                character.AttackDamage += buff.GetValueAsInt();
                break;
            case BuffType.DefenseUp:
                character.Defense += buff.GetValueAsInt();
                break;
            case BuffType.SpeedUp:
                character.MoveSpeed += buff.value;
                break;
            case BuffType.MaxHPUp:
                character.MaxHealth += buff.GetValueAsInt();
                character.Heal(buff.GetValueAsInt()); // 提升上限时同时回复
                break;
            case BuffType.LifeSteal:
                // 吸血效果需要配合 DamageSystem 处理，这里只记录
                Debug.Log($"[RunManager] {character.name} 获得 {buff.value:P0} 吸血");
                break;
        }
    }

    /// <summary>随机打乱房间顺序（起点固定为第一个，Boss 固定为最后一个）</summary>
    private void ShuffleRooms()
    {
        if (_roomQueue.Count <= 2) return;

        var (start, boss) = dungeonGenerator.GetStartAndBoss();

        // 移除起点和 Boss
        _roomQueue.Remove(start);
        _roomQueue.Remove(boss);

        // 打乱中间房间
        for (int i = _roomQueue.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (_roomQueue[i], _roomQueue[j]) = (_roomQueue[j], _roomQueue[i]);
        }

        // 起点放第一，Boss 放最后
        _roomQueue.Insert(0, start);
        _roomQueue.Add(boss);
    }
}

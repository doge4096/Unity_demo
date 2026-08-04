using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 三选一奖励选择器 — 房间清空后展示 3 个随机 Buff 供玩家选择
/// 挂载到 GameManager 同一 GameObject 上
/// </summary>
public class RewardSelector : MonoBehaviour
{
    [Header("Buff 池")]
    [SerializeField] private List<BuffData> buffPool = new();  // 所有可能出现的 Buff

    [Header("选择设置")]
    [SerializeField] private int choicesCount = 3;              // 每次可选数量

    private List<BuffData> _currentChoices = new();             // 当前可选的 3 个 Buff
    private bool _isShowing;

    public List<BuffData> CurrentChoices => _currentChoices;
    public bool IsShowing => _isShowing;

    /// <summary>展示奖励选择（由 RunManager 在房间清空后调用）</summary>
    public void ShowRewards()
    {
        if (buffPool.Count == 0)
        {
            Debug.LogWarning("[RewardSelector] Buff 池为空，无法展示奖励");
            SkipReward();
            return;
        }

        _isShowing = true;

        // 从池中随机抽取 N 个不重复的 Buff
        _currentChoices = GetRandomBuffs(choicesCount);

        // 暂停时间等待选择
        Time.timeScale = 0f;

        Debug.Log($"[RewardSelector] 展示 {_currentChoices.Count} 个奖励选项");

        // TODO: 通过 EventBus 通知 UI 显示面板
        // 当前为框架代码，后续接入 UI 系统
        // RewardPanel.Show(_currentChoices);

        // 模拟选择（没有 UI 时自动选第一个）
        if (FindObjectOfType<RewardPanel>() == null)
        {
            Debug.Log("[RewardSelector] 未找到 RewardPanel，自动选择第一个");
            SelectReward(0);
        }
    }

    /// <summary>玩家选择第 index 个奖励</summary>
    public void SelectReward(int index)
    {
        if (!_isShowing) return;
        if (index < 0 || index >= _currentChoices.Count) return;

        BuffData selected = _currentChoices[index];
        _isShowing = false;

        // 恢复时间
        Time.timeScale = 1f;

        Debug.Log($"[RewardSelector] 玩家选择: {selected.buffName}");

        // 通知 RunManager 应用奖励
        var runManager = GetComponent<RunManager>();
        runManager?.OnRewardSelected(selected);
    }

    /// <summary>跳过奖励（池为空时）</summary>
    public void SkipReward()
    {
        _isShowing = false;
        _currentChoices.Clear();
        Time.timeScale = 1f;

        var runManager = GetComponent<RunManager>();
        runManager?.OnRewardSelected(null);
    }

    /// <summary>从池中随机取 N 个不重复 Buff</summary>
    private List<BuffData> GetRandomBuffs(int count)
    {
        var result = new List<BuffData>();
        var available = new List<BuffData>(buffPool);

        for (int i = 0; i < count && available.Count > 0; i++)
        {
            int idx = Random.Range(0, available.Count);
            result.Add(available[idx]);
            available.RemoveAt(idx);
        }

        return result;
    }

    /// <summary>添加 Buff 到池中（编辑器或外部调用）</summary>
    public void AddToPool(BuffData buff)
    {
        if (!buffPool.Contains(buff))
        {
            buffPool.Add(buff);
        }
    }
}

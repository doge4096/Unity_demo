using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局事件总线 — 解耦各系统之间的通信
/// 用法：EventBus.On("EnemyKilled", (data) => { ... });
///       EventBus.Emit("EnemyKilled", enemyData);
/// </summary>
public static class EventBus
{
    // 事件名 → 回调列表
    private static readonly Dictionary<string, List<Action<object>>> _listeners = new();

    /// <summary>注册事件监听</summary>
    public static void On(string eventName, Action<object> callback)
    {
        if (!_listeners.ContainsKey(eventName))
        {
            _listeners[eventName] = new List<Action<object>>();
        }

        if (!_listeners[eventName].Contains(callback))
        {
            _listeners[eventName].Add(callback);
        }
    }

    /// <summary>移除事件监听</summary>
    public static void Off(string eventName, Action<object> callback)
    {
        if (_listeners.TryGetValue(eventName, out var list))
        {
            list.Remove(callback);
        }
    }

    /// <summary>触发事件（无参数版本）</summary>
    public static void Emit(string eventName, object data = null)
    {
        if (_listeners.TryGetValue(eventName, out var list))
        {
            // 复制一份再遍历，防止回调中修改列表
            var copy = new List<Action<object>>(list);
            foreach (var callback in copy)
            {
                try
                {
                    callback?.Invoke(data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EventBus] 事件 {eventName} 回调异常: {e}");
                }
            }
        }
    }

    /// <summary>清空所有事件（切换场景时调用）</summary>
    public static void Clear()
    {
        _listeners.Clear();
    }

    // ===== 常用事件名常量（避免拼写错误）=====
    public const string ON_ENEMY_KILLED    = "OnEnemyKilled";
    public const string ON_ROOM_CLEARED    = "OnRoomCleared";
    public const string ON_PLAYER_DAMAGED  = "OnPlayerDamaged";
    public const string ON_PLAYER_DIED     = "OnPlayerDied";
    public const string ON_BUFF_SELECTED   = "OnBuffSelected";
    public const string ON_RUN_STARTED     = "OnRunStarted";
    public const string ON_RUN_ENDED       = "OnRunEnded";
    public const string ON_CHARACTER_SWITCHED = "OnCharacterSwitched";
    public const string ON_HEALTH_CHANGED  = "OnHealthChanged";
    public const string ON_MELEE_HIT       = "OnMeleeHit"; // 近战武器命中（准星扩张反馈用）
}

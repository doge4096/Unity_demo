using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 测试"射击后瞄准保持"逻辑：直接设置 PlayerController 的 _aimHoldTimer（模拟刚射击），
/// 观察 IsAiming 参数是否在计时器期间保持 true、归零后恢复 false
/// 菜单：工具/测试射击瞄准保持（英文别名 Tools/TestAimHold，参数 seconds=保持秒数）
/// </summary>
public static class TestAimHold
{
    private const float DefaultSeconds = 3f;

    [MenuItem("工具/测试射击瞄准保持", false, 1006)]
    [MenuItem("Tools/TestAimHold", false, 1006)]
    public static void Test()
    {
        Test(DefaultSeconds);
    }

    /// <summary>模拟玩家在选人界面选择远程角色（激活 RangedPlayer + SetCharacter + Running 状态）</summary>
    [MenuItem("工具/模拟选择远程角色", false, 1006)]
    [MenuItem("Tools/TestSelectRanged", false, 1006)]
    public static void SelectRanged()
    {
        var panel = Object.FindFirstObjectByType<CharacterSelectPanel>(FindObjectsInactive.Include);
        if (panel == null) { Debug.LogError("[测试] 找不到选人面板 CharacterSelectPanel"); return; }
        var field = typeof(CharacterSelectPanel).GetField("rangedData",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var data = field?.GetValue(panel) as CharacterData;
        if (data == null) { Debug.LogError("[测试] 取不到 rangedData（面板未配置？）"); return; }
        GameManager.Instance.OnCharacterSelected(data);
        Debug.Log($"[测试] 已模拟选择远程角色: {data.characterName}（GameManager → Running，PlayerController 现在控制远程角色）");
    }

    public static void Test(float seconds)
    {
        // 场景可能同时存在多个 PlayerController（近战/远程各一），必须全部设置，
        // 否则 FindFirstObjectByType 只会命中第一个 active 的实例（如 MeleePlayer），
        // 实际操控的 RangedPlayer 实例没被设置 → 保持失效
        var pcs = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (pcs == null || pcs.Length == 0)
        {
            Debug.LogError("[保持] 找不到任何 PlayerController（场景无角色？）");
            return;
        }
        var field = typeof(PlayerController).GetField("_aimHoldTimer",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            Debug.LogError("[保持] 反射获取 _aimHoldTimer 失败");
            return;
        }
        foreach (var pc in pcs)
        {
            field.SetValue(pc, seconds);
            Debug.Log($"[保持] {pc.gameObject.name}（activeSelf={pc.gameObject.activeSelf}）已设置瞄准保持计时器 = {seconds} 秒");
        }
    }
}

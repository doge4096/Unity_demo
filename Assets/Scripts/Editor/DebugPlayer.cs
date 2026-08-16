using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 诊断 PlayerController 是否在正常驱动角色：
/// 打印 GameManager 状态、所有 PlayerController 及其 _currentCharacter / _aimHoldTimer
/// 菜单：工具/诊断玩家控制器（英文别名 Tools/DebugPlayer）
/// </summary>
public static class DebugPlayer
{
    [MenuItem("工具/诊断玩家控制器", false, 1007)]
    [MenuItem("Tools/DebugPlayer", false, 1007)]
    public static void Diagnose()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[诊断] ===== 玩家控制链诊断 =====");

        // 1. GameManager 状态（Paused 时 PlayerController.Update 会被跳过）
        var gm = Object.FindFirstObjectByType<GameManager>();
        sb.AppendLine($"[诊断] GameManager: {(gm != null ? gm.CurrentState.ToString() : "null")}");

        // 2. 所有 PlayerController
        var pcs = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"[诊断] PlayerController 数量: {pcs.Length}");
        foreach (var pc in pcs)
        {
            var ccField = typeof(PlayerController).GetField("_currentCharacter",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var timerField = typeof(PlayerController).GetField("_aimHoldTimer",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var cc = ccField?.GetValue(pc) as CharacterBase;
            float timer = timerField != null ? (float)timerField.GetValue(pc) : -1f;
            string ccDesc = cc == null
                ? "null（未选人！）"
                : $"{cc.name} [{cc.GetType().Name}] activeInHierarchy={cc.gameObject.activeInHierarchy}";
            sb.AppendLine($"[诊断] PlayerController: {pc.gameObject.name} activeSelf={pc.gameObject.activeSelf} | _aimHoldTimer={timer:F2} | _currentCharacter={ccDesc}");
        }

        // 3. 角色 Animator 上的参数当前值（直接读 Animator，绕过 PlayerController）
        var female = GameObject.Find("Female");
        if (female != null)
        {
            var anim = female.GetComponent<Animator>();
            if (anim != null)
                sb.AppendLine($"[诊断] Female.Animator IsAiming={anim.GetBool("IsAiming")} Speed={anim.GetFloat("Speed"):F2}");
            else
                sb.AppendLine("[诊断] Female 上没有 Animator");
        }
        else
        {
            sb.AppendLine("[诊断] 找不到 Female（inactive 或不存在）");
        }

        Debug.Log(sb.ToString());
        try { System.IO.File.AppendAllText("D:/tmp/debug_player.txt", sb.ToString() + "\n"); } catch { }
    }
}

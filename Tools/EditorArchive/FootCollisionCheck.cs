using UnityEditor;
using UnityEngine;
using System.Text;
using System.IO;

/// <summary>
/// 脚部碰撞检查：对比 CharacterController 胶囊体底部与脚骨骼的世界 Y
/// 若胶囊体底部远高于/低于脚底 → 走路时脚被碰撞托起或穿地 → 脚部抽搐
/// 菜单：Tools/Check Foot Collision（英文）
/// </summary>
public static class FootCollisionCheck
{
    [MenuItem("Tools/Check Foot Collision")]
    public static void Run()
    {
        var sb = new StringBuilder();
        foreach (var cc in Object.FindObjectsOfType<CharacterController>(true))
        {
            var go = cc.gameObject;
            // 胶囊体底部世界 Y（无旋转假设）
            float bottomY = go.transform.position.y + cc.center.y - cc.height * 0.5f;
            float topY = go.transform.position.y + cc.center.y + cc.height * 0.5f;
            sb.AppendLine($"== {go.name} ==");
            sb.AppendLine($"  胶囊体: center={cc.center} height={cc.height} radius={cc.radius} stepOffset={cc.stepOffset}");
            sb.AppendLine($"  底部世界Y={bottomY:F3} 顶部世界Y={topY:F3} root世界Y={go.transform.position.y:F3}");

            // 找脚/脚趾骨骼
            bool foundFoot = false;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("ToeBase") || (t.name.Contains("Foot") && !t.name.Contains("Footstep")))
                {
                    sb.AppendLine($"  脚骨骼 {t.name}: 世界Y={t.position.y:F3} 相对胶囊底部={t.position.y - bottomY:F3}");
                    foundFoot = true;
                }
            }
            if (!foundFoot) sb.AppendLine("  未找到脚骨骼（模型未挂在此对象下？）");
            sb.AppendLine($"  子物体: {go.transform.childCount} 个");
        }

        var outPath = "Assets/Screenshots/foot_collision.txt";
        System.IO.Directory.CreateDirectory("Assets/Screenshots");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[FootCollision] 完成，结果: " + outPath);
    }
}

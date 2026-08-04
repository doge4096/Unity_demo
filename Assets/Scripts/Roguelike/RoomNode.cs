using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// BSP 二叉树节点 — 用于随机地牢生成
/// 每个叶子节点代表一个房间，非叶子节点代表一次空间分割
/// </summary>
[System.Serializable]
public class RoomNode
{
    // 节点在空间中的矩形区域
    public Rect Bounds;

    // BSP 子节点（左右分割）
    public RoomNode Left;
    public RoomNode Right;

    // 房间（仅叶子节点有效）
    public Rect RoomRect;
    public bool HasRoom;

    // 走廊（从当前节点房间通往父节点房间的路径）
    public List<Vector2Int> Corridor;

    // 便捷属性
    public bool IsLeaf => Left == null && Right == null;
    public Vector2 Center => Bounds.center;
    public Vector2 RoomCenter => RoomRect.center;

    public RoomNode(Rect bounds)
    {
        Bounds = bounds;
    }

    /// <summary>获取房间的世界坐标中心（Y=0，XZ 平面）</summary>
    public Vector3 GetWorldPosition(float y = 0f)
    {
        return new Vector3(RoomCenter.x, y, RoomCenter.y);
    }
}

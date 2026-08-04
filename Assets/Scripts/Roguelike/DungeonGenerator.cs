using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 随机地牢生成器 — 使用 BSP 二叉树分割算法
/// 生成流程：递归分割 → 放置房间 → 连接走廊 → 生成内容
/// </summary>
public class DungeonGenerator : MonoBehaviour
{
    [Header("地牢尺寸")]
    [SerializeField] private int dungeonWidth = 120;
    [SerializeField] private int dungeonHeight = 120;

    [Header("分割参数")]
    [SerializeField] private int minRoomSize = 6;       // 房间最小边长
    [SerializeField] private int maxDepth = 4;          // BSP 最大深度
    [SerializeField] private float splitRandomness = 0.3f; // 分割位置随机度

    [Header("房间参数")]
    [SerializeField] private int roomPadding = 1;       // 房间与边界的内边距
    [SerializeField] private int corridorWidth = 2;     // 走廊宽度

    [Header("内容生成")]
    [SerializeField] private int maxEnemiesPerRoom = 5;
    [SerializeField] private int minEnemiesPerRoom = 1;

    [Header("调试")]
    [SerializeField] private bool showDebugGizmos = true;

    // 生成结果
    private List<RoomNode> _allRooms = new();           // 所有房间节点
    private RoomNode _rootNode;
    private int _currentSeed;

    public List<RoomNode> AllRooms => _allRooms;
    public int CurrentSeed => _currentSeed;

    /// <summary>生成地牢</summary>
    /// <param name="seed">随机种子（相同种子生成相同地图）</param>
    public void Generate(int seed)
    {
        _currentSeed = seed;
        Random.InitState(seed);

        _allRooms.Clear();

        // 1. 创建根节点
        _rootNode = new RoomNode(new Rect(0, 0, dungeonWidth, dungeonHeight));

        // 2. 递归分割
        SplitRecursive(_rootNode, 0);

        // 3. 在叶子节点中创建房间
        CreateRooms(_rootNode);

        // 4. 连接相邻房间的走廊
        ConnectRooms(_rootNode);

        // 5. 收集所有房间
        CollectRooms(_rootNode);

        Debug.Log($"[DungeonGenerator] 生成完成 — Seed: {seed}, 房间数: {_allRooms.Count}");
    }

    /// <summary>递归分割空间（BSP 算法核心）</summary>
    private void SplitRecursive(RoomNode node, int depth)
    {
        if (depth >= maxDepth) return;

        // 判断还能否分割
        bool canSplitH = node.Bounds.width >= minRoomSize * 2 + roomPadding * 2;
        bool canSplitV = node.Bounds.height >= minRoomSize * 2 + roomPadding * 2;

        if (!canSplitH && !canSplitV) return;

        // 随机选择分割方向（优先选择更宽的方向）
        bool splitHorizontal;
        if (canSplitH && canSplitV)
        {
            splitHorizontal = node.Bounds.width > node.Bounds.height
                ? Random.value > 0.3f // 宽 > 高：大概率横向切
                : Random.value < 0.3f; // 高 > 宽：小概率横向切
        }
        else
        {
            splitHorizontal = canSplitH;
        }

        float splitPos;
        Rect leftRect, rightRect;

        if (splitHorizontal)
        {
            // 横向分割（x 轴切一刀，分成左右）
            float minSplit = node.Bounds.x + minRoomSize + roomPadding;
            float maxSplit = node.Bounds.x + node.Bounds.width - minRoomSize - roomPadding;
            float center = (minSplit + maxSplit) * 0.5f;
            float randOffset = (maxSplit - minSplit) * splitRandomness * (Random.value - 0.5f);
            splitPos = Mathf.Clamp(center + randOffset, minSplit, maxSplit);

            leftRect = new Rect(node.Bounds.x, node.Bounds.y,
                splitPos - node.Bounds.x, node.Bounds.height);
            rightRect = new Rect(splitPos, node.Bounds.y,
                node.Bounds.x + node.Bounds.width - splitPos, node.Bounds.height);
        }
        else
        {
            // 纵向分割（y 轴切一刀，分成上下）
            float minSplit = node.Bounds.y + minRoomSize + roomPadding;
            float maxSplit = node.Bounds.y + node.Bounds.height - minRoomSize - roomPadding;
            float center = (minSplit + maxSplit) * 0.5f;
            float randOffset = (maxSplit - minSplit) * splitRandomness * (Random.value - 0.5f);
            splitPos = Mathf.Clamp(center + randOffset, minSplit, maxSplit);

            leftRect = new Rect(node.Bounds.x, node.Bounds.y,
                node.Bounds.width, splitPos - node.Bounds.y);
            rightRect = new Rect(node.Bounds.x, splitPos,
                node.Bounds.width, node.Bounds.y + node.Bounds.height - splitPos);
        }

        node.Left = new RoomNode(leftRect);
        node.Right = new RoomNode(rightRect);

        // 递归分割子节点
        SplitRecursive(node.Left, depth + 1);
        SplitRecursive(node.Right, depth + 1);
    }

    /// <summary>在叶子节点中创建房间</summary>
    private void CreateRooms(RoomNode node)
    {
        if (node.IsLeaf)
        {
            // 在边界内随机生成矩形房间
            float roomX = node.Bounds.x + roomPadding + Random.Range(0f, 0.5f) *
                (node.Bounds.width - roomPadding * 2 - minRoomSize);
            float roomY = node.Bounds.y + roomPadding + Random.Range(0f, 0.5f) *
                (node.Bounds.height - roomPadding * 2 - minRoomSize);
            float roomW = Random.Range(minRoomSize,
                node.Bounds.x + node.Bounds.width - roomX - roomPadding);
            float roomH = Random.Range(minRoomSize,
                node.Bounds.y + node.Bounds.height - roomY - roomPadding);

            node.RoomRect = new Rect(roomX, roomY, roomW, roomH);
            node.HasRoom = true;
        }
        else
        {
            CreateRooms(node.Left);
            CreateRooms(node.Right);
        }
    }

    /// <summary>连接相邻节点的房间（生成走廊）</summary>
    private void ConnectRooms(RoomNode node)
    {
        if (node.IsLeaf) return;

        ConnectRooms(node.Left);
        ConnectRooms(node.Right);

        // 找到左右子树中距离最近的两个房间
        var leftRooms = new List<RoomNode>();
        var rightRooms = new List<RoomNode>();
        CollectRooms(node.Left, leftRooms);
        CollectRooms(node.Right, rightRooms);

        // 找最近的一对
        RoomNode closestA = null, closestB = null;
        float closestDist = float.MaxValue;

        foreach (var a in leftRooms)
        {
            foreach (var b in rightRooms)
            {
                float dist = Vector2.Distance(a.RoomCenter, b.RoomCenter);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestA = a;
                    closestB = b;
                }
            }
        }

        // 生成 L 形走廊
        if (closestA != null && closestB != null)
        {
            closestA.Corridor = GenerateCorridor(closestA.RoomCenter, closestB.RoomCenter);
        }
    }

    /// <summary>生成两点之间的 L 形走廊路径</summary>
    private List<Vector2Int> GenerateCorridor(Vector2 from, Vector2 to)
    {
        var path = new List<Vector2Int>();
        Vector2Int current = Vector2Int.RoundToInt(from);
        Vector2Int target = Vector2Int.RoundToInt(to);

        // 随机决定先走 X 还是先走 Y
        bool goXFirst = Random.value > 0.5f;

        if (goXFirst)
        {
            while (current.x != target.x)
            {
                current.x += current.x < target.x ? 1 : -1;
                path.Add(current);
            }
            while (current.y != target.y)
            {
                current.y += current.y < target.y ? 1 : -1;
                path.Add(current);
            }
        }
        else
        {
            while (current.y != target.y)
            {
                current.y += current.y < target.y ? 1 : -1;
                path.Add(current);
            }
            while (current.x != target.x)
            {
                current.x += current.x < target.x ? 1 : -1;
                path.Add(current);
            }
        }

        return path;
    }

    /// <summary>收集所有叶子房间</summary>
    private void CollectRooms(RoomNode node, List<RoomNode> list = null)
    {
        if (list == null) list = _allRooms;

        if (node.IsLeaf && node.HasRoom)
        {
            list.Add(node);
        }
        else if (!node.IsLeaf)
        {
            CollectRooms(node.Left, list);
            CollectRooms(node.Right, list);
        }
    }

    /// <summary>标记起点房间和 Boss 房间</summary>
    public (RoomNode startRoom, RoomNode bossRoom) GetStartAndBoss()
    {
        if (_allRooms.Count == 0) return (null, null);

        // 起点：第一个房间；Boss：离起点最远的房间
        RoomNode startRoom = _allRooms[0];
        RoomNode bossRoom = _allRooms[0];
        float maxDist = 0f;

        foreach (var room in _allRooms)
        {
            float dist = Vector2.Distance(startRoom.RoomCenter, room.RoomCenter);
            if (dist > maxDist)
            {
                maxDist = dist;
                bossRoom = room;
            }
        }

        return (startRoom, bossRoom);
    }

    // ===== 编辑器可视化 =====

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || _allRooms == null || _allRooms.Count == 0) return;

        // 绘制房间
        foreach (var room in _allRooms)
        {
            Gizmos.color = new Color(0f, 0.8f, 0.4f, 0.3f);
            Vector3 center = room.GetWorldPosition();
            Vector3 size = new Vector3(room.RoomRect.width, 0.1f, room.RoomRect.height);
            Gizmos.DrawCube(center, size);

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(center, size);

            // 绘制走廊
            if (room.Corridor != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var tile in room.Corridor)
                {
                    Vector3 pos = new Vector3(tile.x + 0.5f, 0.05f, tile.y + 0.5f);
                    Gizmos.DrawCube(pos, new Vector3(0.8f, 0.05f, 0.8f));
                }
            }
        }

        // 高亮起点和 Boss 房间
        var (start, boss) = GetStartAndBoss();
        if (start != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(start.GetWorldPosition(0.2f),
                new Vector3(start.RoomRect.width + 1, 0.1f, start.RoomRect.height + 1));
        }
        if (boss != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(boss.GetWorldPosition(0.2f),
                new Vector3(boss.RoomRect.width + 1, 0.1f, boss.RoomRect.height + 1));
        }
    }
}

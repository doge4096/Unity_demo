using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 准星 UI — 根据当前角色类型切换准星样式：
/// - 远程角色：4 向十字准星（上下左右）
/// - 近战角色：左右箭头准星 &lt;·&gt;，命中目标时两箭头向外弹开再回缩
/// 挂在 HUDCanvas 下即可（所有元素由代码动态创建，锚点固定屏幕中心）
/// </summary>
public class Crosshair : MonoBehaviour
{
    // 远程十字准星条的方向偏移（上下左右）
    private static readonly Vector2[] Directions =
    {
        new Vector2(0f, 1f), new Vector2(0f, -1f),
        new Vector2(-1f, 0f), new Vector2(1f, 0f)
    };

    [Header("准星外观")]
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField] private float lineThickness = 2f;   // 准星条粗细
    [SerializeField] private float lineLength = 12f;     // 准星条长度
    [SerializeField] private float gap = 8f;             // 十字中心空隙

    [Header("近战箭头准星（<·>）")]
    [SerializeField] private float arrowArmLength = 12f;    // 箭头臂长
    [SerializeField] private float arrowAngle = 25f;        // 箭头张开角度（与水平线的夹角）
    [SerializeField] private float arrowGap = 16f;          // 箭头尖端与中心点的间距
    [SerializeField] private float centerDotSize = 5f;      // 中心点直径
    [SerializeField] private float hitSpread = 28f;         // 命中时箭头向外弹开的距离
    [SerializeField] private float spreadRecoverSpeed = 50f; // 弹开后回缩速度（像素/秒）

    private RectTransform[] _crossLines;  // 远程十字的 4 条线
    private GameObject _meleeRoot;        // 近战准星根节点（整体显隐）
    private RectTransform _leftArrow;     // 左箭头组
    private RectTransform _rightArrow;    // 右箭头组
    private float _spread;                // 当前扩张量（命中瞬间弹开，逐步回缩到 0）

    private void Start()
    {
        CreateCrossLines();
        CreateMeleeCrosshair();
        EventBus.On(EventBus.ON_MELEE_HIT, OnMeleeHit);
    }

    /// <summary>创建远程十字准星（4 条线）</summary>
    private void CreateCrossLines()
    {
        _crossLines = new RectTransform[Directions.Length];
        for (int i = 0; i < Directions.Length; i++)
        {
            var go = new GameObject($"CrossLine{i}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            var img = go.GetComponent<Image>();
            img.color = crosshairColor;

            var rt = go.GetComponent<RectTransform>();
            // 锚点固定屏幕中心，避免父画布拉伸时偏移
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(lineThickness, lineLength);
            rt.anchoredPosition = Directions[i] * gap;
            _crossLines[i] = rt;
        }
    }

    /// <summary>创建近战箭头准星（中心点 + 左右各 2 根斜线组成的箭头）</summary>
    private void CreateMeleeCrosshair()
    {
        _meleeRoot = new GameObject("MeleeCrosshair", typeof(RectTransform));
        _meleeRoot.transform.SetParent(transform, false);
        var rootRt = _meleeRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = Vector2.zero;

        // 中心点（白色小方块，视觉即 "·"）
        var dot = new GameObject("CenterDot", typeof(RectTransform), typeof(Image));
        dot.transform.SetParent(rootRt.transform, false);
        dot.GetComponent<Image>().color = crosshairColor;
        var dotRt = dot.GetComponent<RectTransform>();
        dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0.5f);
        dotRt.pivot = new Vector2(0.5f, 0.5f);
        dotRt.sizeDelta = new Vector2(centerDotSize, centerDotSize);
        dotRt.anchoredPosition = Vector2.zero;

        // 左右箭头
        _leftArrow = CreateArrow(rootRt.transform, -1f);
        _rightArrow = CreateArrow(rootRt.transform, 1f);
    }

    /// <summary>创建一侧箭头（2 根斜线，尖端朝中心），返回箭头组 RectTransform</summary>
    /// <param name="parent">父节点</param>
    /// <param name="side">-1 = 左箭头 &lt;，1 = 右箭头 &gt;</param>
    private RectTransform CreateArrow(Transform parent, float side)
    {
        var group = new GameObject(side < 0f ? "LeftArrow" : "RightArrow", typeof(RectTransform));
        group.transform.SetParent(parent, false);
        var gRt = group.GetComponent<RectTransform>();
        gRt.anchorMin = gRt.anchorMax = new Vector2(0.5f, 0.5f);
        gRt.pivot = new Vector2(0.5f, 0.5f);
        gRt.anchoredPosition = new Vector2(side * arrowGap, 0f);

        // 两臂方向：以水平线为基准上下张开（左箭头两臂朝左，右箭头两臂朝右）
        float baseAngle = side < 0f ? 180f : 0f;
        for (int i = 0; i < 2; i++)
        {
            float dirAngle = baseAngle + (i == 0 ? -arrowAngle : arrowAngle);

            var arm = new GameObject($"Arm{i}", typeof(RectTransform), typeof(Image));
            arm.transform.SetParent(group.transform, false);
            arm.GetComponent<Image>().color = crosshairColor;

            var rt = arm.GetComponent<RectTransform>();
            // pivot 放线左端 = 箭头尖端（贴中心一侧），线沿方向向外延伸
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(arrowArmLength, lineThickness);
            rt.rotation = Quaternion.Euler(0f, 0f, dirAngle);
            rt.anchoredPosition = Vector2.zero;
        }
        return gRt;
    }

    private void Update()
    {
        // 根据当前角色类型切换准星：远程 → 十字，近战 → 箭头，无角色 → 都隐藏
        var pc = FindObjectOfType<PlayerController>();
        bool isRanged = pc != null && pc.CurrentCharacter is RangedCharacter;
        bool isMelee = pc != null && pc.CurrentCharacter is MeleeCharacter;

        foreach (var line in _crossLines)
            line.gameObject.SetActive(isRanged);
        _meleeRoot.SetActive(isMelee);

        // 命中扩张：弹开后平滑回缩
        if (_spread > 0f)
            _spread = Mathf.MoveTowards(_spread, 0f, spreadRecoverSpeed * Time.deltaTime);

        if (_leftArrow != null)
            _leftArrow.anchoredPosition = new Vector2(-arrowGap - _spread, 0f);
        if (_rightArrow != null)
            _rightArrow.anchoredPosition = new Vector2(arrowGap + _spread, 0f);
    }

    /// <summary>近战命中事件：左右箭头向外弹开</summary>
    private void OnMeleeHit(object data)
    {
        _spread = hitSpread;
    }

    private void OnDestroy()
    {
        EventBus.Off(EventBus.ON_MELEE_HIT, OnMeleeHit);
    }
}

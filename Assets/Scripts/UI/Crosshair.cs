using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 准星 UI — 远程角色显示十字准星，近战角色隐藏
/// 挂在 HUDCanvas 下即可（动态创建 4 个准星条）
/// </summary>
public class Crosshair : MonoBehaviour
{
    // 准星条的方向偏移（上下左右）
    private static readonly Vector2[] Directions =
    {
        new Vector2(0f, 1f), new Vector2(0f, -1f),
        new Vector2(-1f, 0f), new Vector2(1f, 0f)
    };

    [Header("准星外观")]
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField] private float lineThickness = 2f;   // 准星条粗细
    [SerializeField] private float lineLength = 12f;     // 准星条长度
    [SerializeField] private float gap = 8f;             // 中心空隙

    private RectTransform[] _lines;

    private void Start()
    {
        // 动态创建 4 个准星条
        _lines = new RectTransform[Directions.Length];
        for (int i = 0; i < Directions.Length; i++)
        {
            var go = new GameObject($"CrossLine{i}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            var img = go.GetComponent<Image>();
            img.color = crosshairColor;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(lineThickness, lineLength);
            rt.anchoredPosition = Directions[i] * gap;
            _lines[i] = rt;
        }
    }

    private void Update()
    {
        // 远程角色显示准星，近战角色隐藏
        var pc = FindObjectOfType<PlayerController>();
        bool isRanged = pc != null && pc.CurrentCharacter is RangedCharacter;
        gameObject.SetActive(isRanged);
    }
}

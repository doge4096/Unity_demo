using UnityEngine;

/// <summary>
/// 攻击范围指示器 — 在地面绘制扇形攻击区域
/// 自动用射线检测地面高度，通过内部子对象贴合地面
/// 可安全挂载到任意 GameObject 上，不会影响父对象位置
/// </summary>
public class AttackRangeIndicator : MonoBehaviour
{
    [Header("扇形参数")]
    [SerializeField] private float arcAngle = 60f;
    [SerializeField] private float radius = 2.5f;
    [SerializeField] private int segments = 32;
    [SerializeField] private float heightOffset = 0.02f;

    [Header("地面检测")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float maxGroundDistance = 15f;

    [Header("颜色")]
    [SerializeField] private Color fillColor = new Color(1f, 0.3f, 0.05f, 0.35f);
    [SerializeField] private Color edgeColor = new Color(1f, 0.5f, 0.1f, 0.9f);

    [Header("动画")]
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float pulseAmount = 0.15f;
    [SerializeField] private bool enablePulse = true;

    // 内部：独立子对象承载网格
    private GameObject _meshObject;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _mesh;
    private Material _material;
    private bool _isVisible;
    private float _baseAlpha;

    private void Awake()
    {
        // 创建独立子对象，不受父对象位置影响
        _meshObject = new GameObject("RangeFanMesh");
        _meshObject.transform.SetParent(transform.parent); // 挂在祖父级（如 MeleePlayer）下，而非 WeaponSlot
        _meshObject.transform.localPosition = Vector3.zero;
        _meshObject.transform.localRotation = Quaternion.identity;

        _meshFilter = _meshObject.AddComponent<MeshFilter>();
        _meshRenderer = _meshObject.AddComponent<MeshRenderer>();

        Shader shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        _material = new Material(shader);
        _material.color = fillColor;
        _material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _material.SetInt("_ZWrite", 0);
        _material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
        _material.renderQueue = 3000;

        _meshRenderer.material = _material;
        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;

        _baseAlpha = fillColor.a;
        GenerateArcMesh();
        Hide();
    }

    public void UpdateArc(float newAngle, float newRadius)
    {
        arcAngle = newAngle;
        radius = newRadius;
        GenerateArcMesh();
    }

    public void Show()
    {
        _isVisible = true;
        if (_meshRenderer != null) _meshRenderer.enabled = true;
    }

    public void Hide()
    {
        _isVisible = false;
        if (_meshRenderer != null) _meshRenderer.enabled = false;
    }

    private void Update()
    {
        if (_meshObject == null) return;

        // 每帧：将扇形的 XZ 平面朝向与武器一致，Y 贴合地面
        SyncPositionAndRotation();

        if (_isVisible && enablePulse && _material != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            Color c = fillColor;
            c.a = Mathf.Clamp01(_baseAlpha * pulse);
            _material.color = c;
        }
    }

    /// <summary>同步：XZ 位置跟随武器、Y 贴合地面、朝向跟随武器</summary>
    private void SyncPositionAndRotation()
    {
        // 水平方向跟随武器位置
        Vector3 weaponPos = transform.position;
        Vector3 targetPos = weaponPos;

        // 垂直方向：从武器位置向下射线找地面
        Vector3 rayOrigin = weaponPos + Vector3.up * 0.5f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxGroundDistance, groundLayer))
        {
            targetPos.y = hit.point.y + heightOffset;
        }
        else
        {
            // 找不到地面就放在武器下方默认距离
            targetPos.y = weaponPos.y - 1.5f;
        }

        _meshObject.transform.position = targetPos;

        // 只跟随 Y 轴旋转（水平方向），保持扇形紧贴地面
        Vector3 euler = transform.eulerAngles;
        _meshObject.transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }

    private void GenerateArcMesh()
    {
        if (_mesh != null) Destroy(_mesh);
        _mesh = new Mesh { name = "AttackArc" };

        int vertCount = segments + 2;
        Vector3[] verts = new Vector3[vertCount];
        Color[] colors = new Color[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] tris = new int[segments * 3];

        // 圆心
        verts[0] = Vector3.zero;
        colors[0] = fillColor;
        uvs[0] = new Vector2(0.5f, 0f);

        float half = arcAngle / 2f;
        for (int i = 0; i <= segments; i++)
        {
            float angle = -half + (arcAngle / segments) * i;
            float rad = angle * Mathf.Deg2Rad;
            verts[i + 1] = new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);

            float t = (float)i / segments;
            colors[i + 1] = (i == 0 || i == segments) ? edgeColor : Color.Lerp(fillColor, edgeColor, 0.3f);
            uvs[i + 1] = new Vector2(t, 1f);
        }

        for (int i = 0; i < segments; i++)
        {
            tris[i * 3 + 0] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 2;
        }

        _mesh.vertices = verts;
        _mesh.triangles = tris;
        _mesh.colors = colors;
        _mesh.uv = uvs;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
        _meshFilter.mesh = _mesh;
    }

    public void SetColors(Color fill, Color edge)
    {
        fillColor = fill;
        edgeColor = edge;
        _baseAlpha = fillColor.a;
        if (_material != null) _material.color = fillColor;
        GenerateArcMesh();
    }

    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
        if (_material != null) Destroy(_material);
        if (_meshObject != null) Destroy(_meshObject);
    }
}

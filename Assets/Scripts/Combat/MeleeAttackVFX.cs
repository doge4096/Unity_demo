using UnityEngine;
using System.Collections;

/// <summary>
/// 近战攻击特效管理器 — 挥砍弧线 + 命中火花
/// 挂载到武器 GameObject（如 WeaponSlot）上
/// </summary>
public class MeleeAttackVFX : MonoBehaviour
{
    [Header("挥砍弧线")]
    [SerializeField] private float slashDuration = 0.3f;       // 弧线显示时长
    [SerializeField] private float slashWidth = 0.25f;         // 弧线宽度
    [SerializeField] private int slashArcSegments = 30;        // 弧线精度
    [SerializeField] private float arcDrawDistance = 0.9f;     // 弧线在攻击范围的比例位置（0=圆心,1=边缘）

    [Header("命中粒子")]
    [SerializeField] private int hitParticleCount = 10;       // 每次命中粒子数
    [SerializeField] private float hitParticleSize = 0.2f;    // 粒子大小
    [SerializeField] private float hitParticleSpeed = 4f;     // 粒子飞散速度
    [SerializeField] private float hitParticleLifetime = 0.6f; // 粒子存活时间

    [Header("连击颜色")]
    [SerializeField] private Color combo1Color = new Color(1f, 0.85f, 0.1f);
    [SerializeField] private Color combo2Color = new Color(1f, 0.5f, 0.05f);
    [SerializeField] private Color combo3Color = new Color(1f, 0.15f, 0.05f);

    [Header("引用")]
    [SerializeField] private MeleeWeapon weapon;

    // 挥砍弧线
    private LineRenderer _slashLine;
    private Material _slashMat;
    private Coroutine _slashRoutine;

    // 粒子对象池
    private Transform _particleRoot;
    private System.Collections.Generic.Queue<GameObject> _pool = new();
    private System.Collections.Generic.List<GameObject> _active = new();
    private Material _particleMat;

    private void Awake()
    {
        if (weapon == null) weapon = GetComponent<MeleeWeapon>();

        CreateSlashLine();
        CreateParticleSystem();
    }

    #region 挥砍弧线

    private void CreateSlashLine()
    {
        var go = new GameObject("SlashLine");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        _slashLine = go.AddComponent<LineRenderer>();
        _slashLine.useWorldSpace = true;
        _slashLine.positionCount = slashArcSegments;
        _slashLine.enabled = false;
        _slashLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _slashLine.receiveShadows = false;
        _slashLine.sortingOrder = 100; // 渲染在最上层
        _slashLine.numCapVertices = 3;
        _slashLine.numCornerVertices = 3;

        // 使用简单可靠的 Unlit/Color shader
        _slashMat = new Material(Shader.Find("Unlit/Color"));
        _slashMat.SetInt("_ZWrite", 0);
        _slashMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always); // 永远可见，不被遮挡
        _slashLine.material = _slashMat;
    }

    /// <summary>播放挥砍特效</summary>
    public void PlaySlashVFX(float range, float angle, int comboIndex)
    {
        if (_slashRoutine != null)
            StopCoroutine(_slashRoutine);
        _slashRoutine = StartCoroutine(SlashRoutine(range, angle, comboIndex));
    }

    private IEnumerator SlashRoutine(float range, float angle, int comboIndex)
    {
        _slashLine.enabled = true;

        Color c = GetComboColor(comboIndex);
        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;
        float halfAngle = angle / 2f;
        float arcRadius = range * arcDrawDistance; // 弧线半径

        // 预先计算弧线上所有点
        Vector3[] arcPoints = new Vector3[slashArcSegments];
        for (int i = 0; i < slashArcSegments; i++)
        {
            float t = (float)i / (slashArcSegments - 1);
            float a = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * forward;
            arcPoints[i] = origin + dir * arcRadius;
        }

        _slashLine.positionCount = slashArcSegments;
        for (int i = 0; i < slashArcSegments; i++)
            _slashLine.SetPosition(i, arcPoints[i]);

        // 动画：宽度从最大渐变为 0，颜色从亮渐变为透明
        float elapsed = 0f;
        while (elapsed < slashDuration)
        {
            float t = elapsed / slashDuration;

            // 宽度曲线：先快速展开再逐渐消失
            float widthT = t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f;
            float width = slashWidth * widthT;
            _slashLine.startWidth = width * 1.1f;
            _slashLine.endWidth = width * 0.5f;

            // 透明度：前 10% 最亮，之后逐渐透明
            float alpha = t < 0.1f ? 1f : 1f - (t - 0.1f) / 0.9f;
            c.a = alpha;
            _slashMat.color = c;

            elapsed += Time.deltaTime;
            yield return null;
        }

        _slashLine.enabled = false;
        _slashRoutine = null;
    }

    #endregion

    #region 命中粒子

    private void CreateParticleSystem()
    {
        _particleRoot = new GameObject("HitParticles").transform;
        _particleRoot.SetParent(transform);
        _particleRoot.localPosition = Vector3.zero;

        _particleMat = new Material(Shader.Find("Unlit/Color"));
        _particleMat.SetInt("_ZWrite", 0);
        _particleMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

        // 预创建对象池
        for (int i = 0; i < hitParticleCount * 3; i++)
            CreatePooledParticle();
    }

    private void CreatePooledParticle()
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Quad);
        p.name = "Spark";
        p.transform.SetParent(_particleRoot);
        p.SetActive(false);

        var r = p.GetComponent<Renderer>();
        r.material = _particleMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        // Billboard：始终面朝相机
        var bb = p.AddComponent<SimpleBillboard>();
        bb.Init(Camera.main?.transform);

        _pool.Enqueue(p);
    }

    /// <summary>在命中位置播放命中特效</summary>
    public void PlayHitVFX(Vector3 hitPoint, int comboIndex)
    {
        StartCoroutine(HitSparkRoutine(hitPoint, comboIndex));
    }

    private IEnumerator HitSparkRoutine(Vector3 hitPoint, int comboIndex)
    {
        Color c = GetComboColor(comboIndex);

        // 弹出粒子
        var particles = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < hitParticleCount; i++)
        {
            var p = GetFromPool();
            if (p == null) continue;

            p.transform.position = hitPoint + Random.insideUnitSphere * 0.3f;
            p.transform.localScale = Vector3.one * hitParticleSize * Random.Range(0.6f, 1.4f);
            p.GetComponent<Renderer>().material.color = c;
            p.SetActive(true);
            particles.Add(p);
        }

        // 等待粒子存活时间
        yield return new WaitForSeconds(hitParticleLifetime);

        // 回收
        foreach (var p in particles)
            ReturnToPool(p);
    }

    private GameObject GetFromPool()
    {
        if (_pool.Count == 0) CreatePooledParticle();
        if (_pool.Count == 0) return null;
        var p = _pool.Dequeue();
        _active.Add(p);
        return p;
    }

    private void ReturnToPool(GameObject p)
    {
        if (p == null) return;
        p.SetActive(false);
        _active.Remove(p);
        _pool.Enqueue(p);
    }

    #endregion

    private Color GetComboColor(int combo)
    {
        return combo switch
        {
            1 => combo1Color,
            2 => combo2Color,
            3 => combo3Color,
            _ => combo1Color,
        };
    }

    private void OnDestroy()
    {
        if (_slashMat != null) Destroy(_slashMat);
        if (_particleMat != null) Destroy(_particleMat);
    }
}

/// <summary>
/// 简易 Billboard：始终面朝指定 Transform（通常是主相机）
/// </summary>
public class SimpleBillboard : MonoBehaviour
{
    private Transform _target;

    public void Init(Transform target)
    {
        _target = target;
    }

    private void Start()
    {
        if (_target == null && Camera.main != null)
            _target = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (_target != null)
        {
            // 让 Quad 的正面始终朝向相机
            Vector3 toCamera = _target.position - transform.position;
            if (toCamera.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(-toCamera, Vector3.up);
        }
    }
}

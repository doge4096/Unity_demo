using UnityEngine;
using System.Collections;

/// <summary>
/// 近战攻击特效管理器 — 白色弧形刀光（原神式月牙光刃）+ 命中火花
/// 挂载到角色上，刀光以角色为原点发出
/// 三段刀光形状：第1段 横砍（水平弧）、第2段 竖劈（竖直弧）、第3段 左右两下横砍（两条弧）
/// </summary>
public class MeleeAttackVFX : MonoBehaviour
{
    [Header("刀光出现时机（延迟，秒）")]
    [SerializeField] private float slashDelay1 = 0.25f;       // 第1段（横砍）刀光出现延迟
    [SerializeField] private float slashDelay2 = 0.25f;       // 第2段（竖劈）刀光出现延迟
    [SerializeField] private float slashDelay3 = 0.2f;        // 第3段（左右横砍）第一下出现延迟
    [SerializeField] private float slashDelay3Second = 0.12f; // 第3段第二下出现延迟（两下间隔）

    [Header("刀光形状（弧形光刃）")]
    [SerializeField] private float slashDuration = 0.3f;       // 刀光显示时长
    [SerializeField] private float slashWidth = 0.35f;         // 刀光中间最大宽度
    [SerializeField] private int slashArcSegments = 24;        // 弧线精度（点越多越圆滑）
    [SerializeField] private float slashRadiusRatio = 0.55f;   // 弧线半径比例（相对攻击范围，1=判定范围边缘）——0.55 让刀光贴近角色身前
    [SerializeField] private float slashRadiusRatio3 = 0.35f;  // 第3段两下横砍的半径比例（单独拉近：第三段弧从正前方弯向一侧，视觉上容易显得远）
    [SerializeField] private float slashHeight = 0.5f;         // 横砍弧线高度（统一比角色中点低 0.5m，视觉重心更贴近腰部）
    [SerializeField] private float slashArcAngle = 140f;       // 横砍弧张开角度（挥砍横扫幅度）
    [SerializeField] private float slashVerticalTop = 1.3f;    // 竖劈弧顶端高度（比角色中点低 0.5m）
    [SerializeField] private float slashVerticalBottom = -0.3f; // 竖劈弧底端高度（比原位置低 0.5m）——上下对称，中点 y=0.5m
    [SerializeField] private float slashVerticalArc = 120f;    // 竖劈弧张开角度（从上往下的幅度）

    [Header("命中粒子")]
    [SerializeField] private int hitParticleCount = 10;       // 每次命中粒子数
    [SerializeField] private float hitParticleSize = 0.2f;    // 粒子大小
    [SerializeField] private float hitParticleSpeed = 4f;     // 粒子飞散速度
    [SerializeField] private float hitParticleLifetime = 0.6f; // 粒子存活时间

    [Header("刀光颜色（默认白色，可按连击段微调）")]
    [SerializeField] private Color combo1Color = Color.white;
    [SerializeField] private Color combo2Color = Color.white;
    [SerializeField] private Color combo3Color = Color.white;

    // 刀光
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
        CreateSlashLine();
        CreateParticleSystem();
    }

    #region 刀光（弧形光刃，原神式月牙）

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

        // 月牙形状：宽度中间粗、两端收尖（原神式光刃轮廓）
        _slashLine.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.3f, 1f),
            new Keyframe(0.7f, 1f),
            new Keyframe(1f, 0f));
        _slashLine.widthMultiplier = slashWidth;

        // 半透明发光材质（先找 Unlit/Transparent，找不到退回 Sprites/Default）
        var shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        _slashMat = new Material(shader);
        _slashMat.SetInt("_ZWrite", 0);
        _slashMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always); // 永远可见，不被遮挡
        _slashLine.material = _slashMat;
    }

    /// <summary>按连击段获取刀光出现延迟（供角色同步伤害判定时机：判定必须与刀光同时刻发生）</summary>
    public float GetSlashDelay(int comboIndex)
    {
        return comboIndex switch
        {
            1 => slashDelay1,
            2 => slashDelay2,
            3 => slashDelay3,
            _ => slashDelay1,
        };
    }

    /// <summary>第3段第二下刀光的延迟（两下之间的间隔，供角色第二次伤害判定同步）</summary>
    public float GetSlashDelay3Second() => slashDelay3Second;

    /// <summary>刀光弧心到角色的距离（供角色判定同步：判定范围 = 角色中心到刀光弧的这段区域）</summary>
    public float GetSlashRadius(float range) => range * slashRadiusRatio;

    /// <summary>刀光张角（判定角度与刀光同步）</summary>
    public float GetSlashArcAngle() => slashArcAngle;

    /// <summary>播放刀光特效（按连击段选择形状：1横砍弧 / 2竖劈弧 / 3左右两下弧）</summary>
    public void PlaySlashVFX(float range, int comboIndex)
    {
        if (_slashRoutine != null)
            StopCoroutine(_slashRoutine);
        _slashRoutine = StartCoroutine(PlaySlashLine(range, comboIndex));
    }

    private IEnumerator PlaySlashLine(float range, int comboIndex)
    {
        Color c = GetComboColor(comboIndex);
        float arcRadius = range * slashRadiusRatio;

        // 注意：origin/forward 必须在刀光出现的那一刻才取（而不是攻击瞬间），
        // 否则跑步攻击时角色已移动，刀光会锚在旧位置导致角色撞上刀光
        if (comboIndex == 3)
        {
            // === 第3段：左右两下横砍 ===
            // 第三段用独立半径（slashRadiusRatio3），弧心比第1段更贴近角色
            float arcRadius3 = range * slashRadiusRatio3;
            // 第一下：从左往右全幅横砍（张角 70°→140°，攻击范围比原版增大一倍）
            yield return new WaitForSeconds(slashDelay3);
            SetArc(BuildHorizontalArc(transform.position, GetForward(), arcRadius3, -slashArcAngle / 2f, slashArcAngle / 2f));
            yield return StartCoroutine(SlashFade(c));

            // 第二下：从右往左反向全幅横砍，间隔由 slashDelay3Second 控制
            yield return new WaitForSeconds(slashDelay3Second);
            SetArc(BuildHorizontalArc(transform.position, GetForward(), arcRadius3, slashArcAngle / 2f, -slashArcAngle / 2f));
            yield return StartCoroutine(SlashFade(c));
        }
        else if (comboIndex == 2)
        {
            // === 第2段：竖劈（从上往下的竖直弧，在角色正前方）===
            yield return new WaitForSeconds(slashDelay2);
            SetArc(BuildVerticalArc(transform.position, GetForward()));
            yield return StartCoroutine(SlashFade(c));
        }
        else
        {
            // === 第1段：横砍（左右横扫的水平弧）===
            yield return new WaitForSeconds(slashDelay1);
            SetArc(BuildHorizontalArc(transform.position, GetForward(), arcRadius, -slashArcAngle / 2f, slashArcAngle / 2f));
            yield return StartCoroutine(SlashFade(c));
        }

        _slashRoutine = null;
    }

    /// <summary>取角色当前水平朝向（刀光出现那一刻的动态朝向，随转身更新）</summary>
    private Vector3 GetForward()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        return f.normalized;
    }

    /// <summary>生成水平弧线点（横砍，绕 y 轴旋转）：fromAngle 到 toAngle 为横扫范围</summary>
    private Vector3[] BuildHorizontalArc(Vector3 origin, Vector3 forward, float radius, float fromAngle, float toAngle)
    {
        var pts = new Vector3[slashArcSegments];
        for (int i = 0; i < slashArcSegments; i++)
        {
            float t = (float)i / (slashArcSegments - 1);
            float a = Mathf.Lerp(fromAngle, toAngle, t);
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * forward;
            pts[i] = origin + dir * radius + Vector3.up * slashHeight;
        }
        return pts;
    }

    /// <summary>生成竖直弧线点（竖劈，从上往下，在角色正前方的垂直面内）</summary>
    private Vector3[] BuildVerticalArc(Vector3 origin, Vector3 forward)
    {
        float fwd = 0.3f; // 竖劈弧在角色前方的比例位置（贴近角色）
        float midHeight = (slashVerticalTop + slashVerticalBottom) / 2f;
        float vRadius = (slashVerticalTop - slashVerticalBottom) / 2f;
        Vector3 center = origin + forward * (fwd * 2f) + Vector3.up * midHeight;

        var pts = new Vector3[slashArcSegments];
        for (int i = 0; i < slashArcSegments; i++)
        {
            // a：从上(90°)经前(0°)到下(-90°)，弧在竖直面内
            float t = (float)i / (slashArcSegments - 1);
            float a = Mathf.Lerp(90f, -90f, t);
            Vector3 localDir = new Vector3(0f, Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad));
            Vector3 dir = Quaternion.LookRotation(forward) * localDir;
            pts[i] = center + dir * vRadius;
        }
        return pts;
    }

    /// <summary>把弧线点写入 LineRenderer</summary>
    private void SetArc(Vector3[] pts)
    {
        _slashLine.positionCount = pts.Length;
        for (int i = 0; i < pts.Length; i++)
            _slashLine.SetPosition(i, pts[i]);
    }

    /// <summary>刀光渐隐动画：出现瞬间最亮，随后快速衰减消失</summary>
    private IEnumerator SlashFade(Color color)
    {
        _slashLine.enabled = true;

        float elapsed = 0f;
        while (elapsed < slashDuration)
        {
            float t = elapsed / slashDuration;

            // 整体宽度：先快速展开再收窄（月牙轮廓由 widthCurve 决定）
            float widthT = t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f;
            _slashLine.widthMultiplier = slashWidth * widthT;

            // 透明度：前 10% 最亮，之后逐渐透明
            float alpha = t < 0.1f ? 1f : 1f - (t - 0.1f) / 0.9f;
            color.a = alpha;
            _slashMat.color = color;

            elapsed += Time.deltaTime;
            yield return null;
        }

        _slashLine.enabled = false;
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

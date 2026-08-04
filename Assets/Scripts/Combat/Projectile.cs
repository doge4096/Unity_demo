using UnityEngine;

/// <summary>
/// 弹道行为 — 挂载到弹道预制体上
/// 从对象池取出后 Init() 初始化，碰撞或超时自动回收
/// </summary>
[RequireComponent(typeof(Rigidbody))] // 使用 Rigidbody 做碰撞检测
public class Projectile : MonoBehaviour
{
    [Header("弹道参数")]
    [SerializeField] private GameObject hitEffectPrefab;   // 命中特效（可选）

    private int _damage;
    private float _speed;
    private float _lifetime;
    private float _timer;
    private GameObject _owner;          // 发射者（避免打到自己）
    private Rigidbody _rb;
    private bool _isInitialized;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // 确保 Trigger 模式（用 OnTriggerEnter 而非物理碰撞）
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    /// <summary>初始化弹道（由 RangedCharacter 调用）</summary>
    public void Init(int damage, float speed, float lifetime, GameObject owner)
    {
        _damage = damage;
        _speed = speed;
        _lifetime = lifetime;
        _timer = lifetime;
        _owner = owner;
        _isInitialized = true;
    }

    private void OnEnable()
    {
        // 从池中取出时重置计时器
        // 注意：Init() 会在 OnEnable 之后被调用，所以这里只做基础重置
        _timer = _lifetime > 0 ? _lifetime : 3f;
    }

    private void Update()
    {
        if (!_isInitialized) return;

        // 沿前方飞行
        _rb.velocity = transform.forward * _speed;

        // 超时回收
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            ReturnToPool();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isInitialized) return;

        // 不命中发射者自身
        if (_owner != null && other.transform.root.gameObject == _owner) return;

        // 尝试造成伤害
        var damageable = other.GetComponent<IDamageable>();
        if (damageable != null && !damageable.IsDead)
        {
            damageable.TakeDamage(_damage, _owner);
            Debug.Log($"[Projectile] 命中 {other.name}, 伤害: {_damage}");

            // 命中特效
            if (hitEffectPrefab != null)
            {
                var effect = Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
                Destroy(effect, 1f);
            }

            ReturnToPool();
        }
    }

    /// <summary>归还对象池</summary>
    private void ReturnToPool()
    {
        _isInitialized = false;
        _rb.velocity = Vector3.zero;

        // 通过父节点的 ObjectPool 回收
        // 这里用 SendMessage 或查找方式通知池
        var pool = GetComponentInParent<ObjectPoolBehaviour>();
        if (pool != null)
        {
            pool.ReturnToPool(gameObject);
        }
        else
        {
            // 兜底：直接禁用（由池管理器回收）
            gameObject.SetActive(false);
        }
    }
}

/// <summary>
/// ObjectPool 的 MonoBehaviour 包装器 — 挂载到池的根节点上
/// 方便 Projectile 通过父层级找到池
/// </summary>
public class ObjectPoolBehaviour : MonoBehaviour
{
    private ObjectPool _pool;

    public void SetPool(ObjectPool pool)
    {
        _pool = pool;
    }

    public void ReturnToPool(GameObject obj)
    {
        _pool?.Return(obj);
    }
}

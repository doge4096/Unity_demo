using System.Collections;
using UnityEngine;

/// <summary>
/// 模拟敌人（测试木桩）— 挂在圆柱体等物体上，实现 IDamageable 可被近战/远程命中
/// 受击闪红 + 日志输出，血量归零后变灰下沉消失
/// </summary>
public class TestDummyEnemy : MonoBehaviour, IDamageable
{
    [Header("木桩属性")]
    [SerializeField] private int maxHealth = 100;            // 最大血量
    [SerializeField] private float hitFlashDuration = 0.12f; // 受击闪红时长（秒）
    [SerializeField] private Color baseColor = new Color(0.8f, 0.8f, 0.8f, 1f); // 默认颜色

    private Renderer _renderer;
    private int _currentHealth;
    private Coroutine _flashCoroutine;

    /// <summary>当前血量</summary>
    public int CurrentHealth => _currentHealth;
    /// <summary>是否已死亡</summary>
    public bool IsDead { get; private set; }

    private void Start()
    {
        _currentHealth = maxHealth;
        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
        {
            _renderer.material.color = baseColor;
        }
    }

    /// <summary>受到伤害：扣血 + 闪红 + 日志，血量归零后死亡</summary>
    public void TakeDamage(int amount, GameObject source = null)
    {
        if (IsDead) return;

        _currentHealth -= amount;
        Debug.Log($"[TestDummy] {name} 受击 -{amount}，剩余 {Mathf.Max(_currentHealth, 0)}/{maxHealth}");

        // 受击闪红
        if (_renderer != null)
        {
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashRed());
        }

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>死亡：变灰后下沉消失</summary>
    private void Die()
    {
        IsDead = true;
        Debug.Log($"[TestDummy] {name} 已被击杀");
        if (_renderer != null)
            _renderer.material.color = new Color(0.2f, 0.2f, 0.2f, 1f); // 变灰
        StartCoroutine(SinkAndDestroy());
    }

    private IEnumerator FlashRed()
    {
        if (_renderer != null)
            _renderer.material.color = Color.red;
        yield return new WaitForSeconds(hitFlashDuration);
        if (_renderer != null && !IsDead)
            _renderer.material.color = baseColor;
    }

    /// <summary>下沉并销毁（模拟被击杀）</summary>
    private IEnumerator SinkAndDestroy()
    {
        float t = 0f;
        Vector3 start = transform.position;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            transform.position = start + Vector3.down * (2f * Mathf.Min(t, 1f));
            yield return null;
        }
        Destroy(gameObject);
    }
}

using UnityEngine;

/// <summary>
/// MonoBehaviour 泛型单例基类 — 全局唯一实例，自动查找或创建
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static readonly object _lock = new object();
    private static bool _isQuitting;

    public static T Instance
    {
        get
        {
            if (_isQuitting)
            {
                Debug.LogWarning($"[Singleton] {typeof(T).Name} 已销毁，返回 null");
                return null;
            }

            lock (_lock)
            {
                if (_instance == null)
                {
                    // 先尝试在场景中查找已有实例
                    _instance = FindObjectOfType<T>();

                    if (_instance == null)
                    {
                        // 场景中没有则自动创建
                        var go = new GameObject($"[Singleton] {typeof(T).Name}");
                        _instance = go.AddComponent<T>();
                        DontDestroyOnLoad(go);
                    }
                }

                return _instance;
            }
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            // 场景中存在第二个实例 → 销毁自身
            Destroy(gameObject);
        }
    }

    protected virtual void OnApplicationQuit()
    {
        _isQuitting = true;
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 缩放管理器 — 全局控制所有 Canvas 的缩放比例
/// 支持设置面板拖动条实时调整，自动保存到 PlayerPrefs
/// </summary>
public class UIScaleManager : Singleton<UIScaleManager>
{
    [Header("缩放范围")]
    [SerializeField] private float minScale = 0.5f;
    [SerializeField] private float maxScale = 2.0f;
    [SerializeField] private float defaultScale = 1.0f;

    [Header("要控制的 Canvas")]
    [SerializeField] private CanvasScaler[] canvasScalers;

    private float _currentScale = 1.0f;
    private const string SCALE_PREF_KEY = "UIScale";

    public float CurrentScale => _currentScale;
    public float MinScale => minScale;
    public float MaxScale => maxScale;

    protected override void Awake()
    {
        base.Awake();
        // 读取保存的缩放值
        _currentScale = PlayerPrefs.GetFloat(SCALE_PREF_KEY, defaultScale);
        ApplyScale();
    }

    /// <summary>设置 UI 缩放比例（由设置面板调用）</summary>
    public void SetScale(float scale)
    {
        _currentScale = Mathf.Clamp(scale, minScale, maxScale);
        ApplyScale();
        PlayerPrefs.SetFloat(SCALE_PREF_KEY, _currentScale);
        PlayerPrefs.Save();
    }

    /// <summary>应用到所有 Canvas</summary>
    private void ApplyScale()
    {
        if (canvasScalers == null || canvasScalers.Length == 0)
        {
            // 自动查找场景中所有 CanvasScaler
            canvasScalers = FindObjectsOfType<CanvasScaler>();
        }

        foreach (var scaler in canvasScalers)
        {
            if (scaler != null)
            {
                scaler.scaleFactor = _currentScale;
            }
        }

        Debug.Log($"[UIScaleManager] UI 缩放: {_currentScale:F2}x");
    }

    /// <summary>注册新的 CanvasScaler（动态创建的 Canvas）</summary>
    public void RegisterCanvas(CanvasScaler scaler)
    {
        if (scaler == null) return;
        scaler.scaleFactor = _currentScale;
    }
}

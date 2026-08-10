using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsPanel : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;

    [Header("UI 缩放")]
    [SerializeField] private Slider scaleSlider;
    [SerializeField] private TMP_Text scaleValueText;
    [SerializeField] private TMP_Text scaleLabelText;

    [Header("镜头速度")]
    [SerializeField] private Slider cameraSpeedSlider;           // 场景未绑定时会自动复制一份 scaleSlider
    [SerializeField] private TMP_Text cameraSpeedValueText;
    [SerializeField] private TMP_Text cameraSpeedLabelText;

    // 镜头速度存储键（PlayerPrefs 持久化，跨会话保留）
    private const string CameraSpeedPrefKey = "CameraSpeed";
    // 镜头速度范围（对应鼠标灵敏度）
    private const float CameraSpeedMin = 1f;
    private const float CameraSpeedMax = 20f;

    /// <summary>当前镜头速度（全局可读，ThirdPersonCamera 运行时取值；默认 8 比场景默认 5 更快）</summary>
    public static float CameraSpeedSetting
    {
        get => PlayerPrefs.GetFloat(CameraSpeedPrefKey, 8f);
        set => PlayerPrefs.SetFloat(CameraSpeedPrefKey, Mathf.Clamp(value, CameraSpeedMin, CameraSpeedMax));
    }

    [Header("按钮")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button menuButton;

    private bool _isOpen;

    private void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        var scaleManager = UIScaleManager.Instance;
        if (scaleSlider != null && scaleManager != null)
        {
            scaleSlider.minValue = scaleManager.MinScale;
            scaleSlider.maxValue = scaleManager.MaxScale;
            scaleSlider.value = scaleManager.CurrentScale;
            scaleSlider.onValueChanged.AddListener(OnScaleChanged);
        }

        // 镜头速度滑块：场景未绑定时自动复制 UI 缩放滑块一份（保证字体/样式一致），下移一行
        CreateCameraSpeedSlider();

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetScale);

        if (menuButton != null)
        {
            menuButton.onClick.AddListener(ReturnToMenu);
            SetButtonText(menuButton, "返回主菜单");
        }

        // 设置静态中文文本（绕过 API 编码问题）
        if (titleText != null) titleText.text = "设置";
        if (scaleLabelText != null) scaleLabelText.text = "UI 缩放";
        SetButtonText(closeButton, "关闭 (Esc)");
        SetButtonText(resetButton, "重置默认");

        RefreshScaleText();
    }

    private void SetButtonText(Button button, string text)
    {
        var tmp = button != null ? button.GetComponentInChildren<TMP_Text>() : null;
        if (tmp != null) tmp.text = text;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_isOpen) Close();
            else Open();
        }
    }

    public void Open()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(true);
        _isOpen = true;

        var scaleManager = UIScaleManager.Instance;
        if (scaleSlider != null && scaleManager != null)
            scaleSlider.value = scaleManager.CurrentScale;
        RefreshScaleText();

        // 打开面板时同步镜头速度滑块
        if (cameraSpeedSlider != null)
            cameraSpeedSlider.value = CameraSpeedSetting;
        RefreshCameraSpeedText();
    }

    public void Close()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(false);
        _isOpen = false;
    }

    /// <summary>
    /// 创建镜头速度滑块：场景里没有对应 UI 时，把「UI 缩放」滑块/数值/标签各复制一份下移一行，
    /// 复用同一套样式和字体，再改文字和回调——省去手工在场景里摆 UI
    /// </summary>
    private void CreateCameraSpeedSlider()
    {
        if (cameraSpeedSlider != null || scaleSlider == null) return;

        // 复制滑块（放在原滑块下方一行，保持同级）
        var sliderClone = Instantiate(scaleSlider.gameObject, scaleSlider.transform.parent);
        sliderClone.name = "CameraSpeedSlider";
        var sliderRt = sliderClone.GetComponent<RectTransform>();
        sliderRt.anchoredPosition -= new Vector2(0f, 60f);
        cameraSpeedSlider = sliderClone.GetComponent<Slider>();

        // 复制数值文本
        if (scaleValueText != null)
        {
            var valueClone = Instantiate(scaleValueText.gameObject, scaleValueText.transform.parent);
            valueClone.name = "CameraSpeedValue";
            var valueRt = valueClone.GetComponent<RectTransform>();
            valueRt.anchoredPosition -= new Vector2(0f, 60f);
            cameraSpeedValueText = valueClone.GetComponent<TMP_Text>();
        }

        // 复制标签文本并改中文
        if (scaleLabelText != null)
        {
            var labelClone = Instantiate(scaleLabelText.gameObject, scaleLabelText.transform.parent);
            labelClone.name = "CameraSpeedLabel";
            var labelRt = labelClone.GetComponent<RectTransform>();
            labelRt.anchoredPosition -= new Vector2(0f, 60f);
            cameraSpeedLabelText = labelClone.GetComponent<TMP_Text>();
            cameraSpeedLabelText.text = "镜头速度";
        }

        // 配置滑块范围 + 监听
        cameraSpeedSlider.minValue = CameraSpeedMin;
        cameraSpeedSlider.maxValue = CameraSpeedMax;
        cameraSpeedSlider.value = CameraSpeedSetting;
        cameraSpeedSlider.onValueChanged.AddListener(OnCameraSpeedChanged);
        RefreshCameraSpeedText();
    }

    /// <summary>镜头速度滑块变化：写入 PlayerPrefs（ThirdPersonCamera 实时读取）</summary>
    private void OnCameraSpeedChanged(float value)
    {
        CameraSpeedSetting = value;
        RefreshCameraSpeedText();
    }

    private void RefreshCameraSpeedText()
    {
        if (cameraSpeedValueText != null)
            cameraSpeedValueText.text = CameraSpeedSetting.ToString("F1");
    }

    /// <summary>UI 缩放滑块变化：应用到 UIScaleManager</summary>
    private void OnScaleChanged(float value)
    {
        UIScaleManager.Instance?.SetScale(value);
        RefreshScaleText();
    }

    private void ResetScale()
    {
        if (scaleSlider != null)
            scaleSlider.value = 1.0f;
        UIScaleManager.Instance?.SetScale(1.0f);
        RefreshScaleText();
    }

    private void RefreshScaleText()
    {
        if (scaleValueText != null)
        {
            float scale = UIScaleManager.Instance?.CurrentScale ?? 1f;
            scaleValueText.text = scale.ToString("F1") + "x";
        }
    }

    private void ReturnToMenu()
    {
        Close();
        GameManager.Instance?.ReturnToMenu();
        // 重新显示选人界面
        var selectPanel = FindObjectOfType<CharacterSelectPanel>(true);
        if (selectPanel != null)
        {
            var canvas = selectPanel.GetComponentInParent<Canvas>(true);
            if (canvas != null)
                canvas.gameObject.SetActive(true);
        }
    }
}

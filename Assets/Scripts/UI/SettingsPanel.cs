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
    }

    public void Close()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(false);
        _isOpen = false;
    }

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

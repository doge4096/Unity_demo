using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverPanel : MonoBehaviour
{
    [Header("面板根节点")]
    [SerializeField] private GameObject panelRoot;

    [Header("结算文本")]
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text roomsClearedText;
    [SerializeField] private TMP_Text buffsCollectedText;
    [SerializeField] private TMP_Text difficultyText;

    [Header("按钮")]
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton;

    [Header("颜色")]
    [SerializeField] private Color victoryColor = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] private Color defeatColor = new Color(0.9f, 0.2f, 0.2f);

    private void Start()
    {
        panelRoot.SetActive(false);

        EventBus.On(EventBus.ON_RUN_ENDED, data =>
        {
            bool isVictory = data is bool b && b;
            Show(isVictory);
        });

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestart);
            SetButtonText(restartButton, "重新开始");
        }
        if (menuButton != null)
        {
            menuButton.onClick.AddListener(OnReturnToMenu);
            SetButtonText(menuButton, "返回主菜单");
        }
    }

    private void SetButtonText(Button button, string text)
    {
        var tmp = button != null ? button.GetComponentInChildren<TMP_Text>() : null;
        if (tmp != null) tmp.text = text;
    }

    private void OnDestroy()
    {
        EventBus.Off(EventBus.ON_RUN_ENDED, _ => { });
    }

    public void Show(bool isVictory)
    {
        panelRoot.SetActive(true);
        var runManager = FindObjectOfType<RunManager>();

        if (resultTitleText != null)
        {
            resultTitleText.text = isVictory ? "通关！" : "失败！";
            resultTitleText.color = isVictory ? victoryColor : defeatColor;
        }

        if (roomsClearedText != null)
            roomsClearedText.text = "清空房间：" + (runManager?.CurrentRoomIndex ?? 0);

        if (buffsCollectedText != null)
            buffsCollectedText.text = "获得 Buff：" + (runManager?.SelectedBuffs.Count ?? 0);

        if (difficultyText != null)
            difficultyText.text = "最终难度：" + (runManager?.DifficultyMultiplier ?? 1f).ToString("F1") + "x";

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnRestart()
    {
        GameManager.Instance.StartRun();
        panelRoot.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnReturnToMenu()
    {
        GameManager.Instance.ReturnToMenu();
        panelRoot.SetActive(false);
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class CharacterSelectPanel : MonoBehaviour
{
    [Header("角色配置（拖入 ScriptableObject）")]
    [SerializeField] private CharacterData meleeData;
    [SerializeField] private CharacterData rangedData;

    [Header("近战角色卡片")]
    [SerializeField] private GameObject meleeCard;
    [SerializeField] private Image meleePortrait;
    [SerializeField] private TMP_Text meleeName;
    [SerializeField] private TMP_Text meleeDescription;
    [SerializeField] private TMP_Text meleeStats;
    [SerializeField] private Button meleeButton;

    [Header("远程角色卡片")]
    [SerializeField] private GameObject rangedCard;
    [SerializeField] private Image rangedPortrait;
    [SerializeField] private TMP_Text rangedName;
    [SerializeField] private TMP_Text rangedDescription;
    [SerializeField] private TMP_Text rangedStats;
    [SerializeField] private Button rangedButton;

    [Header("标题")]
    [SerializeField] private TMP_Text titleText;

    [Header("武器悬停提示")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TMP_Text tooltipText;

    [Header("面板")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Canvas parentCanvas;

    private void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(true);

        // 标题
        if (titleText != null) titleText.text = "选择你的角色";

        // 隐藏提示面板
        if (tooltipPanel != null) tooltipPanel.SetActive(false);

        if (meleeData != null) FillCard(
            meleePortrait, meleeName, meleeDescription, meleeStats, meleeData);

        if (rangedData != null) FillCard(
            rangedPortrait, rangedName, rangedDescription, rangedStats, rangedData);

        if (meleeButton != null)
        {
            meleeButton.onClick.AddListener(OnMeleeSelected);
            SetButtonText(meleeButton, "选择近战");
            // 悬停事件
            AddHoverEvents(meleeButton, meleeData);
        }

        if (rangedButton != null)
        {
            rangedButton.onClick.AddListener(OnRangedSelected);
            SetButtonText(rangedButton, "选择远程");
            AddHoverEvents(rangedButton, rangedData);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SetButtonText(Button button, string text)
    {
        var tmp = button.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.text = text;
    }

    private void FillCard(Image portrait, TMP_Text nameText, TMP_Text descText, TMP_Text statsText,
        CharacterData data)
    {
        if (portrait != null && data.portrait != null)
        {
            portrait.sprite = data.portrait;
            portrait.enabled = true;
        }

        if (nameText != null)
            nameText.text = data.characterName;

        if (descText != null)
            descText.text = data.description;

        if (statsText != null)
        {
            statsText.text =
                "生命: " + data.maxHealth + "\n" +
                "攻击: " + data.attackDamage + "\n" +
                "防御: " + data.defense + "\n" +
                "速度: " + data.moveSpeed.ToString("F1") + "\n" +
                "范围: " + data.attackRange.ToString("F1") + "\n" +
                "武器: " + data.weaponName;
        }
    }

    /// <summary>给按钮添加悬停事件</summary>
    private void AddHoverEvents(Button button, CharacterData data)
    {
        var trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        // 鼠标进入 → 显示武器简介
        var enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener(_ =>
        {
            if (tooltipPanel != null && tooltipText != null && data != null)
            {
                tooltipText.text = "【" + data.weaponName + "】\n" + data.weaponDescription;
                tooltipPanel.SetActive(true);
                // 提示框放在按钮下方，不遮挡鼠标
                RectTransform btnRect = button.GetComponent<RectTransform>();
                RectTransform tipRect = tooltipPanel.GetComponent<RectTransform>();
                if (btnRect != null && tipRect != null)
                {
                    tipRect.anchoredPosition = btnRect.anchoredPosition + new Vector2(0, -btnRect.sizeDelta.y / 2 - tipRect.sizeDelta.y / 2 - 10);
                }
            }
        });
        trigger.triggers.Add(enterEntry);

        // 鼠标离开 → 隐藏
        var exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener(_ =>
        {
            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);
        });
        trigger.triggers.Add(exitEntry);
    }

    private void OnMeleeSelected()
    {
        Debug.Log("[CharacterSelectPanel] 选择近战角色");
        GameManager.Instance.OnCharacterSelected(meleeData);
        HidePanel();
    }

    private void OnRangedSelected()
    {
        Debug.Log("[CharacterSelectPanel] 选择远程角色");
        GameManager.Instance.OnCharacterSelected(rangedData);
        HidePanel();
    }

    private void HidePanel()
    {
        // 隐藏整个选人 Canvas
        if (parentCanvas != null)
            parentCanvas.gameObject.SetActive(false);
        else if (panelRoot != null)
            panelRoot.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUD : MonoBehaviour
{
    [Header("玩家引用")]
    [SerializeField] private PlayerController playerController;

    [Header("血条")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private TMP_Text healthText;

    [Header("角色指示")]
    [SerializeField] private Image characterPortrait;
    [SerializeField] private TMP_Text characterNameText;

    [Header("房间进度")]
    [SerializeField] private TMP_Text roomProgressText;

    [Header("Buff 列表")]
    [SerializeField] private Transform buffListParent;
    [SerializeField] private GameObject buffIconPrefab;

    [Header("技能冷却")]
    [SerializeField] private Image skillCooldownFill;

    private void Start()
    {
        EventBus.On(EventBus.ON_HEALTH_CHANGED, _ => RefreshHealthBar());
        EventBus.On(EventBus.ON_BUFF_SELECTED, _ => RefreshBuffList());
        EventBus.On(EventBus.ON_RUN_STARTED, _ =>
        {
            RefreshRoomProgress();
            RefreshCharacterInfo();
        });
    }

    private void Update()
    {
        RefreshHealthBar();
        RefreshRoomProgress();
    }

    private void OnDestroy()
    {
        EventBus.Off(EventBus.ON_HEALTH_CHANGED, _ => RefreshHealthBar());
        EventBus.Off(EventBus.ON_BUFF_SELECTED, _ => RefreshBuffList());
    }

    private void RefreshHealthBar()
    {
        if (playerController == null) return;
        var activeChar = playerController.CurrentCharacter;
        if (activeChar == null) return;

        if (healthBar != null)
        {
            healthBar.maxValue = activeChar.MaxHealth;
            healthBar.value = activeChar.CurrentHealth;
        }
        if (healthText != null)
            healthText.text = activeChar.CurrentHealth + " / " + activeChar.MaxHealth;
    }

    private void RefreshCharacterInfo()
    {
        var data = GameManager.Instance?.SelectedCharacterData;
        if (data == null) return;

        if (characterNameText != null)
            characterNameText.text = data.characterName;

        if (characterPortrait != null && data.portrait != null)
        {
            characterPortrait.sprite = data.portrait;
            characterPortrait.enabled = true;
        }
    }

    private void RefreshBuffList()
    {
        var runManager = FindObjectOfType<RunManager>();
        if (runManager == null || buffListParent == null) return;

        foreach (Transform child in buffListParent)
            Destroy(child.gameObject);

        foreach (var buff in runManager.SelectedBuffs)
        {
            if (buffIconPrefab != null)
            {
                var icon = Instantiate(buffIconPrefab, buffListParent);
                var img = icon.GetComponent<Image>();
                if (img != null && buff.icon != null)
                    img.sprite = buff.icon;
            }
        }
    }

    private void RefreshRoomProgress()
    {
        var runManager = FindObjectOfType<RunManager>();
        if (runManager == null || roomProgressText == null) return;
        roomProgressText.text = "房间 " + (runManager.CurrentRoomIndex + 1) + " / " + runManager.TotalRooms;
    }
}

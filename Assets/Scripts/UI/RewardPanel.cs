using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 奖励选择面板 — 房间清空后弹出三选一界面
/// </summary>
public class RewardPanel : MonoBehaviour
{
    [Header("面板根节点")]
    [SerializeField] private GameObject panelRoot;

    [Header("三个奖励按钮")]
    [SerializeField] private Button[] rewardButtons = new Button[3];
    [SerializeField] private Text[] rewardNames = new Text[3];
    [SerializeField] private Text[] rewardDescriptions = new Text[3];
    [SerializeField] private Image[] rewardIcons = new Image[3];

    private RewardSelector _rewardSelector;

    private void Start()
    {
        panelRoot.SetActive(false);

        _rewardSelector = FindObjectOfType<RewardSelector>();

        // 监听房间清空事件
        EventBus.On(EventBus.ON_ROOM_CLEARED, _ => Show());

        // 绑定按钮点击
        for (int i = 0; i < rewardButtons.Length; i++)
        {
            int idx = i; // 闭包捕获
            rewardButtons[i].onClick.AddListener(() => OnRewardClicked(idx));
        }
    }

    private void OnDestroy()
    {
        EventBus.Off(EventBus.ON_ROOM_CLEARED, _ => Show());
    }

    /// <summary>显示奖励面板</summary>
    public void Show()
    {
        if (_rewardSelector == null || !_rewardSelector.IsShowing) return;

        var choices = _rewardSelector.CurrentChoices;
        if (choices == null || choices.Count == 0) return;

        panelRoot.SetActive(true);

        // 填充三个选项
        for (int i = 0; i < 3; i++)
        {
            if (i < choices.Count)
            {
                var buff = choices[i];
                rewardButtons[i].gameObject.SetActive(true);

                if (rewardNames[i] != null)
                    rewardNames[i].text = buff.buffName;

                if (rewardDescriptions[i] != null)
                    rewardDescriptions[i].text = buff.GetFormattedDescription();

                if (rewardIcons[i] != null && buff.icon != null)
                    rewardIcons[i].sprite = buff.icon;

                // 根据稀有度着色
                Color rarityColor = buff.rarity switch
                {
                    BuffData.Rarity.Common => Color.white,
                    BuffData.Rarity.Rare => Color.cyan,
                    BuffData.Rarity.Epic => new Color(0.7f, 0.3f, 1f),  // 紫色
                    BuffData.Rarity.Legendary => new Color(1f, 0.84f, 0f), // 金色
                    _ => Color.white
                };
                if (rewardNames[i] != null) rewardNames[i].color = rarityColor;
            }
            else
            {
                rewardButtons[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>点击某个奖励</summary>
    private void OnRewardClicked(int index)
    {
        _rewardSelector?.SelectReward(index);
        panelRoot.SetActive(false);
    }
}

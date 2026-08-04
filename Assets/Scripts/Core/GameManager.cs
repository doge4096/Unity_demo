using UnityEngine;

/// <summary>
/// 游戏全局状态机 — 控制 主菜单 → 选人 → 运行 → 暂停 → 结束 的切换
/// </summary>
public class GameManager : Singleton<GameManager>
{
    public enum GameState
    {
        MainMenu,
        CharacterSelect,   // 战前选人界面
        Running,
        Paused,
        GameOver
    }

    [Header("初始状态")]
    [SerializeField] private GameState _currentState = GameState.MainMenu;

    [Header("选人后要激活的玩家对象")]
    [SerializeField] private GameObject meleePlayerGo;    // 近战角色场景实例
    [SerializeField] private GameObject rangedPlayerGo;   // 远程角色场景实例
    [SerializeField] private PlayerController playerController;

    public GameState CurrentState => _currentState;
    public CharacterData SelectedCharacterData { get; private set; }
    public RunManager RunManager { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        RunManager = GetComponent<RunManager>();
        if (RunManager == null)
        {
            RunManager = gameObject.AddComponent<RunManager>();
        }

        // 开局隐藏两个角色，等选人后再激活
        if (meleePlayerGo != null) meleePlayerGo.SetActive(false);
        if (rangedPlayerGo != null) rangedPlayerGo.SetActive(false);
    }

    private void Start()
    {
        // 如果初始状态是选人，直接显示选人界面
        if (_currentState == GameState.CharacterSelect)
        {
            OpenCharacterSelect();
        }
    }

    /// <summary>进入选人界面</summary>
    public void OpenCharacterSelect()
    {
        _currentState = GameState.CharacterSelect;
        Debug.Log("[GameManager] 进入角色选择界面");
    }

    /// <summary>玩家选定角色后回调</summary>
    /// <param name="data">选择的角色配置数据</param>
    public void OnCharacterSelected(CharacterData data)
    {
        if (_currentState != GameState.CharacterSelect) return;
        if (data == null)
        {
            Debug.LogError("[GameManager] 选人数据为空");
            return;
        }

        SelectedCharacterData = data;

        // 激活对应角色，隐藏另一个
        if (data.characterType == CharacterData.CharacterType.Melee)
        {
            if (meleePlayerGo != null) meleePlayerGo.SetActive(true);
            if (rangedPlayerGo != null) rangedPlayerGo.SetActive(false);

            var meleeChar = meleePlayerGo?.GetComponent<MeleeCharacter>();
            if (meleeChar != null) meleeChar.InitFromData(data);
            playerController?.SetCharacter(meleeChar);
        }
        else
        {
            if (rangedPlayerGo != null) rangedPlayerGo.SetActive(true);
            if (meleePlayerGo != null) meleePlayerGo.SetActive(false);

            var rangedChar = rangedPlayerGo?.GetComponent<RangedCharacter>();
            if (rangedChar != null) rangedChar.InitFromData(data);
            playerController?.SetCharacter(rangedChar);
        }

        Debug.Log($"[GameManager] 玩家选择: {data.characterName}");

        // 开始游戏
        StartRun();
    }

    /// <summary>开始一次游戏</summary>
    public void StartRun()
    {
        _currentState = GameState.Running;
        EventBus.Emit(EventBus.ON_RUN_STARTED);
        RunManager.StartRun();
        Debug.Log("[GameManager] 游戏开始");
    }

    /// <summary>暂停游戏</summary>
    public void PauseGame()
    {
        if (_currentState != GameState.Running) return;
        _currentState = GameState.Paused;
        Time.timeScale = 0f;
        Debug.Log("[GameManager] 游戏暂停");
    }

    /// <summary>继续游戏</summary>
    public void ResumeGame()
    {
        if (_currentState != GameState.Paused) return;
        _currentState = GameState.Running;
        Time.timeScale = 1f;
        Debug.Log("[GameManager] 游戏继续");
    }

    /// <summary>游戏结束</summary>
    public void GameOver(bool isVictory)
    {
        _currentState = GameState.GameOver;
        Time.timeScale = 1f;
        EventBus.Emit(EventBus.ON_RUN_ENDED, isVictory);
        Debug.Log($"[GameManager] 游戏结束 — {(isVictory ? "胜利" : "失败")}");
    }

    /// <summary>返回主菜单</summary>
    public void ReturnToMenu()
    {
        _currentState = GameState.MainMenu;
        Time.timeScale = 1f;
        EventBus.Clear();

        // 隐藏所有角色
        if (meleePlayerGo != null) meleePlayerGo.SetActive(false);
        if (rangedPlayerGo != null) rangedPlayerGo.SetActive(false);
        if (playerController != null) playerController.SetCharacter(null);

        SelectedCharacterData = null;
        Debug.Log("[GameManager] 返回主菜单");
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}

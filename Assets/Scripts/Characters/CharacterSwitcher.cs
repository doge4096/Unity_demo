using UnityEngine;

/// <summary>
/// 双角色切换器 — 管理近战/远程角色的激活和跟随
/// </summary>
public class CharacterSwitcher : MonoBehaviour
{
    [Header("角色引用")]
    [SerializeField] private CharacterBase meleeCharacter;
    [SerializeField] private CharacterBase rangedCharacter;

    [Header("设置")]
    [SerializeField] private float followDistance = 2f;    // 非活跃角色跟随距离
    [SerializeField] private bool syncPositionOnSwitch = true; // 切换时是否原地互换

    private bool _isMeleeActive = true;
    private CharacterBase _currentCharacter;

    public CharacterBase CurrentCharacter => _currentCharacter;
    public bool IsMeleeActive => _isMeleeActive;
    public CharacterBase MeleeCharacter => meleeCharacter;
    public CharacterBase RangedCharacter => rangedCharacter;

    private void Start()
    {
        // 默认激活近战角色
        if (meleeCharacter == null)
        {
            Debug.LogWarning("[CharacterSwitcher] 未设置近战角色引用");
        }
        if (rangedCharacter == null)
        {
            Debug.LogWarning("[CharacterSwitcher] 未设置远程角色引用");
        }

        ActivateCharacter(meleeCharacter);
        DeactivateCharacter(rangedCharacter);
    }

    /// <summary>切换角色</summary>
    public void Switch()
    {
        if (meleeCharacter == null || rangedCharacter == null)
        {
            Debug.LogWarning("[CharacterSwitcher] 角色引用缺失，无法切换");
            return;
        }

        Vector3 prevPosition = _currentCharacter != null
            ? _currentCharacter.transform.position
            : transform.position;
        Quaternion prevRotation = _currentCharacter != null
            ? _currentCharacter.transform.rotation
            : transform.rotation;

        if (_isMeleeActive)
        {
            // 近战 → 远程
            DeactivateCharacter(meleeCharacter);
            ActivateCharacter(rangedCharacter);

            if (syncPositionOnSwitch)
            {
                rangedCharacter.transform.position = prevPosition;
                rangedCharacter.transform.rotation = prevRotation;
            }

            _isMeleeActive = false;
        }
        else
        {
            // 远程 → 近战
            DeactivateCharacter(rangedCharacter);
            ActivateCharacter(meleeCharacter);

            if (syncPositionOnSwitch)
            {
                meleeCharacter.transform.position = prevPosition;
                meleeCharacter.transform.rotation = prevRotation;
            }

            _isMeleeActive = true;
        }

        EventBus.Emit(EventBus.ON_CHARACTER_SWITCHED, _isMeleeActive);
        Debug.Log($"[CharacterSwitcher] 切换到 {(_isMeleeActive ? "近战" : "远程")} 角色");
    }

    private void Update()
    {
        // 非活跃角色跟随活跃角色
        HandleFollower();
    }

    /// <summary>让非活跃角色在后方跟随</summary>
    private void HandleFollower()
    {
        var active = _currentCharacter;
        var inactive = _isMeleeActive ? rangedCharacter : meleeCharacter;

        if (active == null || inactive == null) return;
        if (inactive.gameObject.activeSelf) return; // 已经是活跃状态则跳过

        // 保持在活跃角色身后
        Vector3 targetPos = active.transform.position - active.transform.forward * followDistance;
        targetPos.y = active.transform.position.y;

        inactive.transform.position = Vector3.Lerp(
            inactive.transform.position,
            targetPos,
            Time.deltaTime * 5f
        );
        inactive.transform.rotation = Quaternion.Lerp(
            inactive.transform.rotation,
            active.transform.rotation,
            Time.deltaTime * 5f
        );
    }

    private void ActivateCharacter(CharacterBase character)
    {
        if (character == null) return;
        character.gameObject.SetActive(true);
        _currentCharacter = character;
    }

    private void DeactivateCharacter(CharacterBase character)
    {
        if (character == null) return;
        character.gameObject.SetActive(false);
    }
}

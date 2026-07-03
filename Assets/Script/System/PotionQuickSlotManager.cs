using UnityEngine;
using System;

public class PotionQuickSlotManager : MonoBehaviour
{
    public static PotionQuickSlotManager instance;

    public event Action OnHpSlotChanged;
    public event Action OnMpSlotChanged;

    private ItemInstance _hpSlot             = null;
    private ItemInstance _mpSlot             = null;
    private float        _hpCooldownRemaining = 0f;
    private float        _mpCooldownRemaining = 0f;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        if (_hpCooldownRemaining > 0f) _hpCooldownRemaining -= Time.deltaTime;
        if (_mpCooldownRemaining > 0f) _mpCooldownRemaining -= Time.deltaTime;

        if (GameManager.instance == null || !GameManager.instance.isLive) return;
        if (MenuTabUI.IsOpen || ShopUI.IsOpen || DialogueUI.IsOpen || EnhancementUI.IsOpen) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) TryUsePotion(ConsumableType.HpPotion);
        if (Input.GetKeyDown(KeyCode.Alpha2)) TryUsePotion(ConsumableType.MpPotion);
    }

    // ─────────────────────────────────────────────────────────────────
    // 등록 / 해제
    // ─────────────────────────────────────────────────────────────────

    public void RegisterPotion(ItemInstance item)
    {
        if (item?.data is not ConsumableData cd) return;

        if (cd.consumableType == ConsumableType.HpPotion)
        {
            _hpSlot              = item;
            _hpCooldownRemaining = 0f;
            OnHpSlotChanged?.Invoke();
        }
        else if (cd.consumableType == ConsumableType.MpPotion)
        {
            _mpSlot              = item;
            _mpCooldownRemaining = 0f;
            OnMpSlotChanged?.Invoke();
        }
    }

    public void DeregisterPotion(ConsumableType type)
    {
        if (type == ConsumableType.HpPotion)
        {
            _hpSlot              = null;
            _hpCooldownRemaining = 0f;
            OnHpSlotChanged?.Invoke();
        }
        else if (type == ConsumableType.MpPotion)
        {
            _mpSlot              = null;
            _mpCooldownRemaining = 0f;
            OnMpSlotChanged?.Invoke();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 사용
    // ─────────────────────────────────────────────────────────────────

    private void TryUsePotion(ConsumableType type)
    {
        ItemInstance slot = GetSlot(type);
        if (slot?.data is not ConsumableData cd) return;
        if (GetCooldownRemaining(type) > 0f) return;

        var leader = PartyManager.instance?.currentLeader;
        if (leader == null) return;

        CharacterStat stat = leader.GetComponent<CharacterStat>();
        if (stat == null) return;

        if (DataManager.instance != null)
        {
            DataManager.instance.sharedInventory.ConsumeItem(slot, 1);
            if (slot.stackCount <= 0)
                DeregisterPotion(type);
            else if (type == ConsumableType.HpPotion)
                OnHpSlotChanged?.Invoke();
            else
                OnMpSlotChanged?.Invoke();
        }

        if (type == ConsumableType.HpPotion)
        {
            stat.HealHp(cd.healAmount);
            _hpCooldownRemaining = cd.cooldown;
        }
        else
        {
            stat.RecoverMp(cd.healAmount);
            _mpCooldownRemaining = cd.cooldown;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 쿼리
    // ─────────────────────────────────────────────────────────────────

    public ItemInstance GetSlot(ConsumableType type)
        => type == ConsumableType.HpPotion ? _hpSlot : _mpSlot;

    /// <summary>인벤토리 스택 변경 후 호출 — 등록된 포션이면 UI 갱신 이벤트 발생.</summary>
    public void RefreshIfRegistered(ItemData data)
    {
        if (_hpSlot?.data == data) OnHpSlotChanged?.Invoke();
        if (_mpSlot?.data == data) OnMpSlotChanged?.Invoke();
    }

    public float GetCooldownRemaining(ConsumableType type)
        => type == ConsumableType.HpPotion ? _hpCooldownRemaining : _mpCooldownRemaining;

    public float GetCooldownRatio(ConsumableType type)
    {
        ItemInstance slot = GetSlot(type);
        if (slot?.data is not ConsumableData cd || cd.cooldown <= 0f) return 0f;
        return Mathf.Clamp01(GetCooldownRemaining(type) / cd.cooldown);
    }
}

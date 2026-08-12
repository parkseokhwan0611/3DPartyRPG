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

    // 재동기화 시 이전에 구독했던 인벤토리(재시작/재로드로 이미 교체된 낡은 객체일 수 있음)를
    // 정확히 구분해서 해제하기 위한 캐시 — QuestManager.RefreshInventorySubscription과 동일한 패턴
    private Inventory _subscribedInventory;

    // DataManager 등 다른 매니저와 같은 오브젝트에 함께 배치되는 경우가 있어, 중복 감지 시
    // Destroy(gameObject)로 오브젝트 전체를 지우면 아직 초기화되지 않은 형제 컴포넌트까지
    // 함께 사라질 수 있음 — 이 컴포넌트 자신만 제거한다
    void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
    }

    // 원래 이 컴포넌트는 씬마다 파괴 후 재생성되는 씬 로컬 오브젝트로 설계됐지만, DataManager 등
    // DontDestroyOnLoad를 호출하는 매니저와 같은 오브젝트에 함께 배치되면 그 오브젝트 전체가
    // 씬 전환에도 의도치 않게 살아남을 수 있다. 그 경우 Start()는 최초 1회만 실행되므로, 슬롯
    // 등록·인벤토리 구독 로직을 DataManager.OnDataInitialized(새 게임/로드마다 발생)에도 걸어서
    // 이 오브젝트가 씬 로컬이든 우연히 계속 살아남든 항상 최신 데이터 기준으로 맞춰지게 한다
    void Start()
    {
        if (DataManager.instance != null)
            DataManager.instance.OnDataInitialized += SyncFromDataManager;

        SyncFromDataManager();
    }

    void OnDestroy()
    {
        if (DataManager.instance != null)
            DataManager.instance.OnDataInitialized -= SyncFromDataManager;

        if (_subscribedInventory != null)
            _subscribedInventory.OnChanged -= HandleInventoryChanged;
    }

    private void SyncFromDataManager()
    {
        if (DataManager.instance == null) return;

        // 이전 게임(또는 이전 씬)의 아이템을 가리키고 있을 수 있는 낡은 참조를 먼저 비운다 —
        // 비우지 않으면 AutoRegisterStartingPotions의 "슬롯이 비어있을 때만 등록" 조건이
        // 이전 게임의 등록 상태를 그대로 유지해버려 새 인벤토리를 반영하지 못한다
        _hpSlot              = null;
        _mpSlot              = null;
        _hpCooldownRemaining = 0f;
        _mpCooldownRemaining = 0f;

        // 새 게임으로 씬에 처음 진입한 경우에만 소비되는 1회성 플래그 —
        // 로드 게임은 SaveManager.RestorePotionSlots가 별도로 슬롯을 복원함
        if (DataManager.instance.pendingAutoRegisterPotions)
        {
            DataManager.instance.pendingAutoRegisterPotions = false;
            AutoRegisterStartingPotions();
        }
        else
        {
            RestoreSlotFromDataManager(DataManager.instance.hpPotionSlotItemId);
            RestoreSlotFromDataManager(DataManager.instance.mpPotionSlotItemId);
        }

        OnHpSlotChanged?.Invoke();
        OnMpSlotChanged?.Invoke();

        // 퀘스트 보상/몬스터 드랍처럼 ShopUI를 거치지 않고 인벤토리 스택이 바뀌는 경로에서도
        // 퀵슬롯 개수 표시가 갱신되도록, 인벤토리 변경 이벤트를 직접 구독 — sharedInventory는
        // 새 게임/로드마다 새 객체로 교체되므로 그때마다 다시 구독해야 한다
        if (_subscribedInventory != null)
            _subscribedInventory.OnChanged -= HandleInventoryChanged;
        _subscribedInventory = DataManager.instance.sharedInventory;
        _subscribedInventory.OnChanged += HandleInventoryChanged;
    }

    // 등록된 슬롯이 있으면 무조건 갱신 이벤트 발생 — 어떤 아이템이 바뀌었는지 몰라도
    // stackCount 텍스트만 다시 읽어오는 가벼운 작업이라 매번 갱신해도 무해함
    private void HandleInventoryChanged()
    {
        RelinkIfDepleted(ConsumableType.HpPotion);
        RelinkIfDepleted(ConsumableType.MpPotion);

        if (_hpSlot != null) OnHpSlotChanged?.Invoke();
        if (_mpSlot != null) OnMpSlotChanged?.Invoke();
    }

    // 슬롯에 등록된 포션이 소진(stackCount 0)된 상태에서 같은 종류 포션을 새로 얻으면,
    // 수동으로 다시 등록하지 않아도 그 아이템으로 자동 연결 — 쿨다운은 건드리지 않는다
    // (재고가 없어 쿨타임이 도는 중에 주웠다고 쿨타임이 리셋되면 어색함)
    private void RelinkIfDepleted(ConsumableType type)
    {
        ItemInstance slot = GetSlot(type);
        if (slot == null || slot.stackCount > 0) return;
        if (DataManager.instance == null) return;

        foreach (var item in DataManager.instance.sharedInventory.Items)
        {
            if (item?.data is not ConsumableData cd || cd.consumableType != type) continue;

            if (type == ConsumableType.HpPotion) _hpSlot = item;
            else                                  _mpSlot = item;

            if (DataManager.instance != null)
            {
                if (type == ConsumableType.HpPotion) DataManager.instance.hpPotionSlotItemId = item.data.itemId;
                else                                   DataManager.instance.mpPotionSlotItemId = item.data.itemId;
            }
            return;
        }
    }

    private void RestoreSlotFromDataManager(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || DataManager.instance == null) return;

        foreach (var item in DataManager.instance.sharedInventory.Items)
        {
            if (item?.data != null && item.data.itemId == itemId)
            {
                RegisterPotion(item);
                return;
            }
        }
    }

    private void AutoRegisterStartingPotions()
    {
        if (DataManager.instance == null) return;

        foreach (var item in DataManager.instance.sharedInventory.Items)
        {
            if (item?.data is not ConsumableData cd) continue;

            if (cd.consumableType == ConsumableType.HpPotion && _hpSlot == null)
                RegisterPotion(item);
            else if (cd.consumableType == ConsumableType.MpPotion && _mpSlot == null)
                RegisterPotion(item);
        }
    }

    void Update()
    {
        if (_hpCooldownRemaining > 0f) _hpCooldownRemaining -= Time.deltaTime;
        if (_mpCooldownRemaining > 0f) _mpCooldownRemaining -= Time.deltaTime;

        if (GameManager.instance == null || !GameManager.instance.isLive) return;
        if (MenuTabUI.IsOpen || ShopUI.IsOpen || DialogueUI.IsOpen || EnhancementUI.IsOpen) return;

        if (Input.GetKeyDown(KeyCode.S)) TryUsePotion(ConsumableType.HpPotion);
        if (Input.GetKeyDown(KeyCode.D)) TryUsePotion(ConsumableType.MpPotion);
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
            if (DataManager.instance != null) DataManager.instance.hpPotionSlotItemId = item.data.itemId;
            OnHpSlotChanged?.Invoke();
        }
        else if (cd.consumableType == ConsumableType.MpPotion)
        {
            _mpSlot              = item;
            _mpCooldownRemaining = 0f;
            if (DataManager.instance != null) DataManager.instance.mpPotionSlotItemId = item.data.itemId;
            OnMpSlotChanged?.Invoke();
        }
    }

    public void DeregisterPotion(ConsumableType type)
    {
        if (type == ConsumableType.HpPotion)
        {
            _hpSlot              = null;
            _hpCooldownRemaining = 0f;
            if (DataManager.instance != null) DataManager.instance.hpPotionSlotItemId = "";
            OnHpSlotChanged?.Invoke();
        }
        else if (type == ConsumableType.MpPotion)
        {
            _mpSlot              = null;
            _mpCooldownRemaining = 0f;
            if (DataManager.instance != null) DataManager.instance.mpPotionSlotItemId = "";
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
        if (slot.stackCount <= 0) return; // 재고 소진 — 등록은 유지하되(쿨다운 표시용) 사용은 불가
        if (GetCooldownRemaining(type) > 0f) return;

        if (PartyManager.instance == null || PartyManager.instance.partyMembers.Count == 0) return;

        if (DataManager.instance != null)
        {
            DataManager.instance.sharedInventory.ConsumeItem(slot, 1);
            // 0개가 돼도 슬롯 등록은 그대로 유지 — 쿨다운이 계속 보이고, 다시 등록하거나
            // 같은 포션을 얻으면(RelinkIfDepleted) 자동으로 다시 쓸 수 있게 함
            if (type == ConsumableType.HpPotion) OnHpSlotChanged?.Invoke();
            else                                  OnMpSlotChanged?.Invoke();
        }

        // 개인이 아닌 파티 전체 적용 — 생존한 모든 파티원이 동일하게 회복
        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member == null || member.CurrentState == PartyMemberScript.MemberState.Dead) continue;

            CharacterStat stat = member.GetComponent<CharacterStat>();
            if (stat == null) continue;

            if (type == ConsumableType.HpPotion) stat.HealHp(cd.healAmount);
            else                                  stat.RecoverMp(cd.healAmount);
        }

        if (type == ConsumableType.HpPotion) _hpCooldownRemaining = cd.cooldown;
        else                                  _mpCooldownRemaining = cd.cooldown;
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

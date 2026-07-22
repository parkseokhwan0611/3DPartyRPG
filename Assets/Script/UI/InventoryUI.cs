using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    // 인스펙터 참조
    // ─────────────────────────────────────────────────────────────────

    [Header("캐릭터 탭")]
    [SerializeField] Button[]     characterTabs;   // CharacterTabGroup 하위 버튼 (partyIndex 순)
    [SerializeField] GameObject[] displayModels;   // 전시 공간 캐릭터 모델 (partyIndex 순)

    [Header("장착 슬롯")]
    [SerializeField] Button slotWeapon;
    [SerializeField] Button slotHat;
    [SerializeField] Button slotChest;
    [SerializeField] Button slotGloves;
    [SerializeField] Button slotBoots;
    [SerializeField] Button slotNecklace;
    [SerializeField] Button slotRing1;
    [SerializeField] Button slotRing2;

    [Header("인벤토리")]
    [SerializeField] Transform  content;
    [SerializeField] GameObject itemSlotPrefab;
    [SerializeField] Button     sortButton;
    [SerializeField] TextMeshProUGUI goldText;

    [Header("DetailPopup")]
    [SerializeField] RectTransform detailPopup;
    [SerializeField] Image         itemGradeBG;
    [SerializeField] TextMeshProUGUI          nameText;
    [SerializeField] TextMeshProUGUI          gradeText;
    [SerializeField] TextMeshProUGUI          itemTypeText;
    [SerializeField] TextMeshProUGUI[]        mainOptionTexts;  // Main 0, 1, 2
    [SerializeField] TextMeshProUGUI[]        subOptionTexts;   // Sub 0, 1, 2, 3
    [SerializeField] TextMeshProUGUI          descriptionText;
    [SerializeField] TextMeshProUGUI          sellPriceText;
    [SerializeField] Button        equipButton;
    [SerializeField] Button        unequipButton;
    [SerializeField] Button        sellButton;
    [SerializeField] Button        registerPotionButton;
    [SerializeField] Button        deregisterPotionButton;

    [Header("판매 수량 패널 (소비·재료 판매 시)")]
    [SerializeField] GameObject     sellQuantityPanel;
    [SerializeField] TMP_InputField sellQuantityInput;
    [SerializeField] Slider         sellQuantitySlider;
    [SerializeField] TextMeshProUGUI sellQuantityPriceText;
    [SerializeField] Button         confirmSellButton;
    [SerializeField] Button         cancelSellButton;

    [Header("등급 스프라이트 (0:일반 ~ 4:신화)")]
    [SerializeField] Sprite[] gradeSprites;

    // ─────────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────────

    public static InventoryUI instance;

    private int          selectedCharIndex   = 0;
    private ItemInstance selectedItem        = null;
    private EquipSlot    selectedEquipSlot;
    private bool         isEquipSlotSelected = false;
    private bool         _sellSyncLock       = false;

    // 장착 슬롯 컴포넌트 캐시
    private Dictionary<EquipSlot, Button> equipButtons    = new Dictionary<EquipSlot, Button>();
    private Dictionary<EquipSlot, Image>  equipIcons      = new Dictionary<EquipSlot, Image>();
    private Dictionary<EquipSlot, Image>  equipEmptyIcons = new Dictionary<EquipSlot, Image>();
    private Dictionary<EquipSlot, TextMeshProUGUI>   equipEnhTexts   = new Dictionary<EquipSlot, TextMeshProUGUI>();

    // 인벤토리 슬롯 캐시 (Start에서 MaxSlots개 미리 생성)
    private struct SlotCache
    {
        public Image             icon;
        public TextMeshProUGUI   enhText;
        public TextMeshProUGUI   stackText;
        public Button            button;
        public InventoryDragItem dragItem;
    }
    private SlotCache[] slotCaches;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        instance = this;

        // 슬롯 딕셔너리 초기화
        equipButtons[EquipSlot.Weapon]   = slotWeapon;
        equipButtons[EquipSlot.Hat]      = slotHat;
        equipButtons[EquipSlot.Chest]    = slotChest;
        equipButtons[EquipSlot.Gloves]   = slotGloves;
        equipButtons[EquipSlot.Boots]    = slotBoots;
        equipButtons[EquipSlot.Necklace] = slotNecklace;
        equipButtons[EquipSlot.Ring1]    = slotRing1;
        equipButtons[EquipSlot.Ring2]    = slotRing2;

        // 슬롯 하위 컴포넌트 캐싱 + 드롭 타겟 추가
        foreach (var kvp in equipButtons)
        {
            Transform t = kvp.Value.transform;
            equipIcons[kvp.Key]      = t.Find("Icon")?      .GetComponent<Image>();
            equipEmptyIcons[kvp.Key] = t.Find("EmptyIcon")? .GetComponent<Image>();
            equipEnhTexts[kvp.Key]   = t.Find("EnhanceText")?.GetComponent<TextMeshProUGUI>();

            var drop = kvp.Value.gameObject.AddComponent<EquipSlotDropTarget>();
            drop.slot        = kvp.Key;
            drop.inventoryUI = this;
        }
    }

    void Start()
    {
        // 캐릭터 탭 버튼 이벤트
        for (int i = 0; i < characterTabs.Length; i++)
        {
            int idx = i;
            characterTabs[i].onClick.AddListener(() => SelectCharacter(idx));
        }

        // 장착 슬롯 버튼 이벤트
        foreach (var kvp in equipButtons)
        {
            EquipSlot slot = kvp.Key;
            kvp.Value.onClick.AddListener(() => OnEquipSlotClicked(slot));
        }

        sortButton   .onClick.AddListener(OnSortClicked);
        equipButton  .onClick.AddListener(OnEquipClicked);
        unequipButton.onClick.AddListener(OnUnequipClicked);
        sellButton   .onClick.AddListener(OnSellClicked);

        if (registerPotionButton   != null) registerPotionButton  .onClick.AddListener(OnRegisterPotionClicked);
        if (deregisterPotionButton != null) deregisterPotionButton.onClick.AddListener(OnDeregisterPotionClicked);

        // 판매 수량 슬라이더/인풋 연동
        if (sellQuantitySlider != null)
        {
            sellQuantitySlider.minValue     = 1;
            sellQuantitySlider.maxValue     = 99;
            sellQuantitySlider.wholeNumbers = true;
            sellQuantitySlider.onValueChanged.AddListener(OnSellSliderChanged);
        }
        if (sellQuantityInput != null)
        {
            sellQuantityInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            sellQuantityInput.onValueChanged.AddListener(OnSellInputValueChanged);
            sellQuantityInput.onEndEdit.AddListener(OnSellInputEndEdit);
        }
        confirmSellButton?.onClick.AddListener(OnConfirmSellClicked);
        cancelSellButton ?.onClick.AddListener(CloseSellQuantityPanel);
        sellQuantityPanel?.SetActive(false);

        // 인벤토리 슬롯 미리 생성 및 컴포넌트 캐싱
        slotCaches = new SlotCache[Inventory.MaxSlots];
        for (int i = 0; i < Inventory.MaxSlots; i++)
        {
            var slotObj = Instantiate(itemSlotPrefab, content);

            var drag = slotObj.AddComponent<InventoryDragItem>();
            drag.inventoryUI = this;

            slotCaches[i] = new SlotCache
            {
                icon      = slotObj.transform.Find("Icon")      ?.GetComponent<Image>(),
                enhText   = slotObj.transform.Find("EnhanceText")?.GetComponent<TextMeshProUGUI>(),
                stackText = slotObj.transform.Find("StackText")  ?.GetComponent<TextMeshProUGUI>(),
                button    = slotObj.GetComponent<Button>(),
                dragItem  = drag,
            };
        }

        detailPopup.gameObject.SetActive(false);

        // OnEnable이 Start보다 먼저 실행되므로 슬롯 생성 후 다시 갱신
        RefreshInventory();
    }

    void Update()
    {
        // 판매 수량 패널 열려있으면 외부 클릭 감지 비활성화
        bool sellQtyOpen = sellQuantityPanel != null && sellQuantityPanel.activeSelf;

        if (!sellQtyOpen && detailPopup.gameObject.activeSelf && Input.GetMouseButtonDown(0))
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(detailPopup, Input.mousePosition))
                CloseDetailPopup();
        }
    }

    void OnEnable()
    {
        if (DataManager.instance != null)
            DataManager.instance.OnGoldChanged += RefreshGold;

        // 열 때는 항상 현재 리더 캐릭터로 시작
        selectedCharIndex = GetLeaderPartyIndex();
        if (DataManager.instance != null)
            DataManager.instance.selectedPartyIndex = selectedCharIndex;

        SelectCharacter(selectedCharIndex);
        RefreshInventory();
        RefreshGold();
    }

    private int GetLeaderPartyIndex()
    {
        if (PartyManager.instance?.currentLeader == null) return 0;
        var stat = PartyManager.instance.currentLeader.GetComponent<CharacterStat>();
        return stat != null ? stat.partyIndex : 0;
    }

    void OnDisable()
    {
        if (DataManager.instance != null)
            DataManager.instance.OnGoldChanged -= RefreshGold;
    }

    // ─────────────────────────────────────────────────────────────────
    // 캐릭터 탭
    // ─────────────────────────────────────────────────────────────────

    void SelectCharacter(int index)
    {
        selectedCharIndex = index;
        if (DataManager.instance != null)
            DataManager.instance.selectedPartyIndex = index;

        for (int i = 0; i < displayModels.Length; i++)
            displayModels[i].SetActive(i == index);

        RefreshEquipSlots();
        CloseDetailPopup();
    }

    // ─────────────────────────────────────────────────────────────────
    // 장착 슬롯 갱신
    // ─────────────────────────────────────────────────────────────────

    void RefreshEquipSlots()
    {
        if (DataManager.instance == null) return;
        var equipment = DataManager.instance.partyEquipments[selectedCharIndex];

        foreach (var kvp in equipButtons)
        {
            ItemInstance item    = equipment.GetSlot(kvp.Key);
            bool         hasItem = item != null;

            // 아이콘 / EmptyIcon 전환
            if (equipIcons.TryGetValue(kvp.Key, out var icon))
            {
                icon.sprite  = hasItem ? item.data.icon : null;
                icon.enabled = hasItem;
            }
            if (equipEmptyIcons.TryGetValue(kvp.Key, out var empty))
                empty.enabled = !hasItem;

            // 강화 텍스트
            if (equipEnhTexts.TryGetValue(kvp.Key, out var enh))
                enh.text = hasItem && item.enhancementLevel > 0
                    ? $"+{item.enhancementLevel}" : "";
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 인벤토리 슬롯 갱신
    // ─────────────────────────────────────────────────────────────────

    void RefreshInventory()
    {
        if (slotCaches == null) return;

        var items = DataManager.instance != null
            ? DataManager.instance.sharedInventory.Items
            : (IReadOnlyList<ItemInstance>)System.Array.Empty<ItemInstance>();

        for (int i = 0; i < Inventory.MaxSlots; i++)
        {
            bool         hasItem  = i < items.Count;
            ItemInstance captured = hasItem ? items[i] : null;
            SlotCache    c        = slotCaches[i];

            if (c.icon != null)
            {
                c.icon.sprite  = hasItem ? captured.data.icon : null;
                c.icon.enabled = hasItem && captured.data.icon != null;
            }

            if (c.enhText != null)
                c.enhText.text = hasItem && captured.IsEquipment && captured.enhancementLevel > 0
                    ? $"+{captured.enhancementLevel}" : "";

            if (c.stackText != null)
                c.stackText.text = hasItem && !captured.IsEquipment && captured.stackCount > 1
                    ? $"x{captured.stackCount}" : "";

            if (c.button != null)
            {
                c.button.onClick.RemoveAllListeners();
                if (hasItem)
                    c.button.onClick.AddListener(() => OnInventorySlotClicked(captured));
            }

            if (c.dragItem != null) c.dragItem.item = captured;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 슬롯 클릭
    // ─────────────────────────────────────────────────────────────────

    void OnEquipSlotClicked(EquipSlot slot)
    {
        if (DataManager.instance == null) return;

        ItemInstance item = DataManager.instance
            .partyEquipments[selectedCharIndex].GetSlot(slot);

        if (item == null) { CloseDetailPopup(); return; }

        selectedItem        = item;
        selectedEquipSlot   = slot;
        isEquipSlotSelected = true;

        ShowDetailPopup(item, new Vector2(400f, -40f));
    }

    void OnInventorySlotClicked(ItemInstance item)
    {
        selectedItem        = item;
        isEquipSlotSelected = false;

        ShowDetailPopup(item, new Vector2(-400f, -40f));
    }

    // ─────────────────────────────────────────────────────────────────
    // DetailPopup 표시
    // ─────────────────────────────────────────────────────────────────

    void ShowDetailPopup(ItemInstance item, Vector2 position)
    {
        detailPopup.anchoredPosition = position;
        detailPopup.gameObject.SetActive(true);

        var equip = item.data as EquipItemData;

        // 등급 배경 스프라이트
        int gi = (int)item.data.grade;
        if (itemGradeBG != null && gradeSprites != null && gi < gradeSprites.Length)
            itemGradeBG.sprite = gradeSprites[gi];

        // 기본 정보 (강화 단계는 이름 앞에 표시)
        nameText.text     = item.enhancementLevel > 0
            ? $"+{item.enhancementLevel} {item.data.itemName}"
            : item.data.itemName;
        gradeText.text    = $"등급: {GetGradeName(item.data.grade)}";
        itemTypeText.text = GetItemTypeName(item, equip);

        // 메인 옵션 (무기: 1개 / 방어구: 3개 / 장신구: 0개)
        // Main 0~2 슬롯에 순서대로 채우고 남는 슬롯은 숨김
        var mainValues = equip != null
            ? new System.Collections.Generic.List<(MainOptionType, float)>(item.GetMainValues())
            : null;

        for (int i = 0; i < mainOptionTexts.Length; i++)
        {
            if (mainValues != null && i < mainValues.Count)
            {
                mainOptionTexts[i].gameObject.SetActive(true);
                mainOptionTexts[i].text =
                    $"{GetMainOptionName(mainValues[i].Item1)}: {mainValues[i].Item2:F0}";
            }
            else
            {
                mainOptionTexts[i].gameObject.SetActive(false);
            }
        }

        // 서브 옵션
        int maxSub = GetMaxSubCount(item.data.grade);
        for (int i = 0; i < subOptionTexts.Length; i++)
        {
            bool show = equip != null && i < equip.subOptions.Count && i < maxSub;
            subOptionTexts[i].gameObject.SetActive(show);
            if (show)
                subOptionTexts[i].text =
                    $"{GetSubOptionName(equip.subOptions[i].type)}: +{equip.subOptions[i].value}";
        }

        // 설명
        descriptionText.text = item.data.description;

        // 장착/해제 버튼 (같은 위치에서 하나만 활성화)
        bool isEquip = item.IsEquipment;
        equipButton  .gameObject.SetActive(isEquip && !isEquipSlotSelected);
        unequipButton.gameObject.SetActive(isEquip && isEquipSlotSelected);

        // 포션 등록/해제 버튼 (HP·MP 포션이고 인벤토리 슬롯일 때만, 등록 여부로 하나만 표시)
        var  cd           = item.data as ConsumableData;
        bool isPotionSlot = !isEquipSlotSelected
            && cd != null
            && (cd.consumableType == ConsumableType.HpPotion
                || cd.consumableType == ConsumableType.MpPotion);

        if (isPotionSlot)
        {
            bool isRegistered = PotionQuickSlotManager.instance != null
                && PotionQuickSlotManager.instance.GetSlot(cd.consumableType) == item;
            if (registerPotionButton   != null) registerPotionButton  .gameObject.SetActive(!isRegistered);
            if (deregisterPotionButton != null) deregisterPotionButton.gameObject.SetActive(isRegistered);
        }
        else
        {
            if (registerPotionButton   != null) registerPotionButton  .gameObject.SetActive(false);
            if (deregisterPotionButton != null) deregisterPotionButton.gameObject.SetActive(false);
        }

        // 판매 가격 (단가 표시, 실제 합산은 수량 확정 시)
        if (sellPriceText != null)
            sellPriceText.text = $"가격: {item.data.sellPrice:N0}원";

        // 판매 버튼 (항상 표시)
        sellButton.gameObject.SetActive(true);
        sellQuantityPanel?.SetActive(false);
    }

    /// <summary>ESC 우선순위 처리 — MenuTabUI에서 호출. 닫을 패널이 있으면 true 반환.</summary>
    public bool TryCloseSubPanel()
    {
        if (sellQuantityPanel != null && sellQuantityPanel.activeSelf) { CloseSellQuantityPanel(); return true; }
        if (detailPopup != null && detailPopup.gameObject.activeSelf)  { CloseDetailPopup();       return true; }
        return false;
    }

    void CloseDetailPopup()
    {
        sellQuantityPanel?.SetActive(false);
        detailPopup.gameObject.SetActive(false);
        selectedItem = null;
        if (registerPotionButton   != null) registerPotionButton  .gameObject.SetActive(false);
        if (deregisterPotionButton != null) deregisterPotionButton.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────
    // 장착 / 해제 / 정렬
    // ─────────────────────────────────────────────────────────────────

    void OnEquipClicked() => EquipItem(selectedItem);

    /// <summary>장비 장착 — 장착 버튼·더블클릭 공용. 슬롯은 자동 결정(반지는 1→2 순서).</summary>
    public void EquipItem(ItemInstance item)
    {
        if (item == null || !item.IsEquipment || DataManager.instance == null) return;

        var inv   = DataManager.instance.sharedInventory;
        var equip = DataManager.instance.partyEquipments[selectedCharIndex];
        var stat  = DataManager.instance.partyStatuses[selectedCharIndex];

        ItemInstance prev = equip.Equip(item);
        inv.Remove(item);
        if (prev != null) inv.TryAddItem(prev);
        equip.RecalculateStats(stat);

        AudioManager.instance?.PlaySFX("ItemEquip");

        CloseDetailPopup();
        RefreshEquipSlots();
        RefreshInventory();
    }

    /// <summary>드래그 앤 드롭으로 장착 — EquipSlotDropTarget에서 호출.</summary>
    public void EquipFromDrag(ItemInstance item, EquipSlot targetSlot)
    {
        if (item == null || DataManager.instance == null) return;

        var inv       = DataManager.instance.sharedInventory;
        var charEquip = DataManager.instance.partyEquipments[selectedCharIndex];
        var stat      = DataManager.instance.partyStatuses[selectedCharIndex];

        ItemInstance prev = charEquip.EquipToSlot(item, targetSlot);
        inv.Remove(item);
        if (prev != null) inv.TryAddItem(prev);
        charEquip.RecalculateStats(stat);

        AudioManager.instance?.PlaySFX("ItemEquip");

        CloseDetailPopup();
        RefreshEquipSlots();
        RefreshInventory();
    }

    void OnUnequipClicked() => UnequipSlot(selectedEquipSlot);

    /// <summary>장비 해제 — 해제 버튼·더블클릭 공용.</summary>
    public void UnequipSlot(EquipSlot slot)
    {
        if (DataManager.instance == null) return;

        var inv   = DataManager.instance.sharedInventory;
        var equip = DataManager.instance.partyEquipments[selectedCharIndex];
        var stat  = DataManager.instance.partyStatuses[selectedCharIndex];

        ItemInstance item = equip.Unequip(slot);
        if (item != null) inv.TryAddItem(item);
        equip.RecalculateStats(stat);

        AudioManager.instance?.PlaySFX("ItemUnequip");

        CloseDetailPopup();
        RefreshEquipSlots();
        RefreshInventory();
    }

    void RefreshGold()
    {
        if (goldText == null || DataManager.instance == null) return;
        goldText.text = $"{DataManager.instance.gold:N0}";
    }

    void OnSortClicked()
    {
        DataManager.instance?.sharedInventory.Sort();
        RefreshInventory();
    }

    void OnSellClicked()
    {
        if (selectedItem == null || DataManager.instance == null) return;
        if (isEquipSlotSelected) return;

        // 소비·재료는 수량 패널 열기
        if (!selectedItem.IsEquipment)
        {
            OpenSellQuantityPanel();
            return;
        }

        // 장비는 바로 판매
        ExecuteSell(1);
    }

    void OpenSellQuantityPanel()
    {
        if (selectedItem == null) return;
        int maxQty = Mathf.Clamp(selectedItem.stackCount, 1, 99);

        _sellSyncLock = true;
        if (sellQuantitySlider != null) { sellQuantitySlider.maxValue = maxQty; sellQuantitySlider.value = 1; }
        if (sellQuantityInput  != null) sellQuantityInput.text = "1";
        _sellSyncLock = false;
        UpdateSellQuantityPriceText(1);

        sellButton       ?.gameObject.SetActive(false);
        sellQuantityPanel?.SetActive(true);
    }

    // 선택한 수량 기준 총 판매가 표시
    void UpdateSellQuantityPriceText(int qty)
    {
        if (sellQuantityPriceText == null || selectedItem?.data == null) return;
        sellQuantityPriceText.text = $"가격: {selectedItem.data.sellPrice * qty:N0}원";
    }

    void CloseSellQuantityPanel()
    {
        sellQuantityPanel?.SetActive(false);
        sellButton?.gameObject.SetActive(true);
    }

    void OnConfirmSellClicked()
    {
        if (selectedItem == null || DataManager.instance == null) return;
        int qty = sellQuantityInput != null && int.TryParse(sellQuantityInput.text, out int v)
            ? Mathf.Clamp(v, 1, selectedItem.stackCount) : 1;
        ExecuteSell(qty);
    }

    void ExecuteSell(int qty)
    {
        if (selectedItem == null || DataManager.instance == null) return;
        var inv = DataManager.instance.sharedInventory;

        int totalPrice = selectedItem.data.sellPrice * qty;

        if (selectedItem.IsEquipment)
            inv.Remove(selectedItem);
        else
            inv.ConsumeItem(selectedItem, qty);

        DataManager.instance.AddGold(totalPrice);
        AudioManager.instance?.PlaySFX("Money");

        CloseDetailPopup();
        RefreshInventory();
    }

    // 판매 수량 슬라이더 ↔ 인풋 동기화
    void OnSellSliderChanged(float value)
    {
        UpdateSellQuantityPriceText((int)value);
        if (_sellSyncLock || sellQuantityInput == null) return;
        _sellSyncLock = true;
        sellQuantityInput.text = ((int)value).ToString();
        _sellSyncLock = false;
    }

    void OnSellInputValueChanged(string s)
    {
        if (int.TryParse(s, out int typed) && typed >= 1)
            UpdateSellQuantityPriceText(typed);

        if (_sellSyncLock || sellQuantitySlider == null || string.IsNullOrEmpty(s)) return;
        if (!int.TryParse(s, out int v) || v < 1) return;
        float clamped = Mathf.Clamp(v, sellQuantitySlider.minValue, sellQuantitySlider.maxValue);
        if (Mathf.Approximately(sellQuantitySlider.value, clamped)) return;
        _sellSyncLock = true;
        sellQuantitySlider.value = clamped;
        _sellSyncLock = false;
    }

    void OnSellInputEndEdit(string s)
    {
        if (_sellSyncLock) return;
        int max = sellQuantitySlider != null ? (int)sellQuantitySlider.maxValue : 99;
        int clamped = int.TryParse(s, out int v) ? Mathf.Clamp(v, 1, max) : 1;
        _sellSyncLock = true;
        if (sellQuantityInput  != null) sellQuantityInput.text   = clamped.ToString();
        if (sellQuantitySlider != null) sellQuantitySlider.value = clamped;
        _sellSyncLock = false;
        UpdateSellQuantityPriceText(clamped);
    }

    void OnRegisterPotionClicked()
    {
        if (selectedItem == null) return;
        PotionQuickSlotManager.instance?.RegisterPotion(selectedItem);
        CloseDetailPopup();
    }

    void OnDeregisterPotionClicked()
    {
        if (selectedItem?.data is not ConsumableData cd) return;
        PotionQuickSlotManager.instance?.DeregisterPotion(cd.consumableType);
        CloseDetailPopup();
    }

    // ─────────────────────────────────────────────────────────────────
    // 헬퍼
    // ─────────────────────────────────────────────────────────────────

    int GetMaxSubCount(ItemGrade grade) => grade switch
    {
        ItemGrade.Normal    => 1,
        ItemGrade.Advanced  => 2,
        ItemGrade.Elite     => 2,
        ItemGrade.Legendary => 3,
        ItemGrade.Mythic    => 4,
        _                   => 0
    };

    string GetGradeName(ItemGrade grade) => grade switch
    {
        ItemGrade.Normal    => "일반",
        ItemGrade.Advanced  => "고급",
        ItemGrade.Elite     => "영웅",
        ItemGrade.Legendary => "전설",
        ItemGrade.Mythic    => "신화",
        _                   => ""
    };

    string GetItemTypeName(ItemInstance item, EquipItemData equip)
    {
        if (equip != null) return equip.equipType switch
        {
            EquipType.Sword    => "검",
            EquipType.Staff    => "지팡이",
            EquipType.Hat      => "모자",
            EquipType.Chest    => "갑옷",
            EquipType.Gloves   => "장갑",
            EquipType.Boots    => "신발",
            EquipType.Necklace => "목걸이",
            EquipType.Ring     => "반지",
            _                  => ""
        };
        if (item.IsConsumable) return "소비 아이템";
        if (item.IsMaterial)   return "재료";
        return "";
    }

    string GetMainOptionName(MainOptionType type) => type switch
    {
        MainOptionType.PhysAtk  => "물리 공격력",
        MainOptionType.MagicAtk => "마법 공격력",
        MainOptionType.MaxHP    => "최대 체력",
        MainOptionType.PhysDef  => "방어력",
        MainOptionType.MagicRes => "마법 저항력",
        _                       => ""
    };

    string GetSubOptionName(SubOptionType type) => type switch
    {
        SubOptionType.STR           => "힘",
        SubOptionType.VIT           => "체력",
        SubOptionType.INT           => "지능",
        SubOptionType.FTH           => "신앙",
        SubOptionType.PhysDef       => "방어력",
        SubOptionType.MagicRes      => "마법 저항력",
        SubOptionType.CritRate      => "치명타 확률",
        SubOptionType.CritDmg       => "치명타 데미지",
        SubOptionType.SkillCDReduce => "스킬 쿨타임 감소",
        SubOptionType.MpCostReduce  => "마나 소모 감소",
        SubOptionType.PhysDmgBonus  => "물리 피해 증가",
        SubOptionType.MagicDmgBonus => "마법 피해 증가",
        _                           => ""
    };
}

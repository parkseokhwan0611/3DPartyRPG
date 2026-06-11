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

    [Header("DetailPopup")]
    [SerializeField] RectTransform detailPopup;
    [SerializeField] Image         itemGradeBG;
    [SerializeField] TextMeshProUGUI          nameText;
    [SerializeField] TextMeshProUGUI          gradeText;
    [SerializeField] TextMeshProUGUI          itemTypeText;
    [SerializeField] TextMeshProUGUI[]        mainOptionTexts;  // Main 0, 1, 2
    [SerializeField] TextMeshProUGUI[]        subOptionTexts;   // Sub 0, 1, 2, 3
    [SerializeField] TextMeshProUGUI          descriptionText;
    [SerializeField] Button        equipButton;
    [SerializeField] Button        unequipButton;
    [SerializeField] Button        sellButton;

    [Header("등급 스프라이트 (0:일반 ~ 4:신화)")]
    [SerializeField] Sprite[] gradeSprites;

    // ─────────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────────

    private int          selectedCharIndex   = 0;
    private ItemInstance selectedItem        = null;
    private EquipSlot    selectedEquipSlot;
    private bool         isEquipSlotSelected = false;

    // 장착 슬롯 컴포넌트 캐시
    private Dictionary<EquipSlot, Button> equipButtons    = new Dictionary<EquipSlot, Button>();
    private Dictionary<EquipSlot, Image>  equipIcons      = new Dictionary<EquipSlot, Image>();
    private Dictionary<EquipSlot, Image>  equipEmptyIcons = new Dictionary<EquipSlot, Image>();
    private Dictionary<EquipSlot, TextMeshProUGUI>   equipEnhTexts   = new Dictionary<EquipSlot, TextMeshProUGUI>();

    // 인벤토리 슬롯 캐시 (Start에서 MaxSlots개 미리 생성)
    private struct SlotCache
    {
        public Image           icon;
        public TextMeshProUGUI enhText;
        public TextMeshProUGUI stackText;
        public Button          button;
    }
    private SlotCache[] slotCaches;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        // 슬롯 딕셔너리 초기화
        equipButtons[EquipSlot.Weapon]   = slotWeapon;
        equipButtons[EquipSlot.Hat]      = slotHat;
        equipButtons[EquipSlot.Chest]    = slotChest;
        equipButtons[EquipSlot.Gloves]   = slotGloves;
        equipButtons[EquipSlot.Boots]    = slotBoots;
        equipButtons[EquipSlot.Necklace] = slotNecklace;
        equipButtons[EquipSlot.Ring1]    = slotRing1;
        equipButtons[EquipSlot.Ring2]    = slotRing2;

        // 슬롯 하위 컴포넌트 캐싱
        foreach (var kvp in equipButtons)
        {
            Transform t = kvp.Value.transform;
            equipIcons[kvp.Key]      = t.Find("Icon")?      .GetComponent<Image>();
            equipEmptyIcons[kvp.Key] = t.Find("EmptyIcon")? .GetComponent<Image>();
            equipEnhTexts[kvp.Key]   = t.Find("EnhanceText")?.GetComponent<TextMeshProUGUI>();
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

        // 인벤토리 슬롯 미리 생성 및 컴포넌트 캐싱
        slotCaches = new SlotCache[Inventory.MaxSlots];
        for (int i = 0; i < Inventory.MaxSlots; i++)
        {
            var slotObj = Instantiate(itemSlotPrefab, content);
            slotCaches[i] = new SlotCache
            {
                icon      = slotObj.transform.Find("Icon")      ?.GetComponent<Image>(),
                enhText   = slotObj.transform.Find("EnhanceText")?.GetComponent<TextMeshProUGUI>(),
                stackText = slotObj.transform.Find("StackText")  ?.GetComponent<TextMeshProUGUI>(),
                button    = slotObj.GetComponent<Button>(),
            };
        }

        detailPopup.gameObject.SetActive(false);
    }

    void Update()
    {
        if (detailPopup.gameObject.activeSelf && Input.GetMouseButtonDown(0))
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(detailPopup, Input.mousePosition))
                CloseDetailPopup();
        }
    }

    void OnEnable()
    {
        // 스탯창 등 다른 창에서 선택한 캐릭터 유지
        if (DataManager.instance != null)
            selectedCharIndex = DataManager.instance.selectedPartyIndex;

        SelectCharacter(selectedCharIndex);
        RefreshInventory();
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

        // 판매 버튼 (항상 표시)
        sellButton.gameObject.SetActive(true);
    }

    void CloseDetailPopup()
    {
        detailPopup.gameObject.SetActive(false);
        selectedItem = null;
    }

    // ─────────────────────────────────────────────────────────────────
    // 장착 / 해제 / 정렬
    // ─────────────────────────────────────────────────────────────────

    void OnEquipClicked()
    {
        if (selectedItem == null || DataManager.instance == null) return;

        var inv   = DataManager.instance.sharedInventory;
        var equip = DataManager.instance.partyEquipments[selectedCharIndex];
        var stat  = DataManager.instance.partyStatuses[selectedCharIndex];

        ItemInstance prev = equip.Equip(selectedItem);
        inv.Remove(selectedItem);
        if (prev != null) inv.TryAddItem(prev);
        equip.RecalculateStats(stat);

        CloseDetailPopup();
        RefreshEquipSlots();
        RefreshInventory();
    }

    void OnUnequipClicked()
    {
        if (DataManager.instance == null) return;

        var inv   = DataManager.instance.sharedInventory;
        var equip = DataManager.instance.partyEquipments[selectedCharIndex];
        var stat  = DataManager.instance.partyStatuses[selectedCharIndex];

        ItemInstance item = equip.Unequip(selectedEquipSlot);
        if (item != null) inv.TryAddItem(item);
        equip.RecalculateStats(stat);

        CloseDetailPopup();
        RefreshEquipSlots();
        RefreshInventory();
    }

    void OnSortClicked()
    {
        DataManager.instance?.sharedInventory.Sort();
        RefreshInventory();
    }

    void OnSellClicked()
    {
        // TODO: 판매 기능 구현
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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] Text          nameText;
    [SerializeField] Text          gradeText;
    [SerializeField] Text          itemTypeText;
    [SerializeField] Text[]        mainOptionTexts;  // Main 0, 1, 2
    [SerializeField] Text[]        subOptionTexts;   // Sub 0, 1, 2, 3
    [SerializeField] Text          descriptionText;
    [SerializeField] Button        equipButton;
    [SerializeField] Button        unequipButton;

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
    private Dictionary<EquipSlot, Text>   equipEnhTexts   = new Dictionary<EquipSlot, Text>();

    private List<GameObject> inventorySlotObjs = new List<GameObject>();

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
            equipEnhTexts[kvp.Key]   = t.Find("EnhanceText")?.GetComponent<Text>();
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

        sortButton  .onClick.AddListener(OnSortClicked);
        equipButton  .onClick.AddListener(OnEquipClicked);
        unequipButton.onClick.AddListener(OnUnequipClicked);

        detailPopup.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        SelectCharacter(selectedCharIndex);
        RefreshInventory();
    }

    // ─────────────────────────────────────────────────────────────────
    // 캐릭터 탭
    // ─────────────────────────────────────────────────────────────────

    void SelectCharacter(int index)
    {
        selectedCharIndex = index;

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
        // 기존 슬롯 제거
        foreach (var obj in inventorySlotObjs) Destroy(obj);
        inventorySlotObjs.Clear();

        if (DataManager.instance == null) return;
        var items = DataManager.instance.sharedInventory.Items;

        foreach (var item in items)
        {
            ItemInstance captured = item; // 클로저 캡처용
            GameObject   slotObj  = Instantiate(itemSlotPrefab, content);
            inventorySlotObjs.Add(slotObj);

            // 아이콘
            var icon = slotObj.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite  = captured.data.icon;
                icon.enabled = captured.data.icon != null;
            }

            // 강화 텍스트 (+N, 장비만)
            var enhText = slotObj.transform.Find("EnhanceText")?.GetComponent<Text>();
            if (enhText != null)
                enhText.text = captured.IsEquipment && captured.enhancementLevel > 0
                    ? $"+{captured.enhancementLevel}" : "";

            // 스택 텍스트 (x99, 소비·재료만)
            var stackText = slotObj.transform.Find("StackText")?.GetComponent<Text>();
            if (stackText != null)
                stackText.text = !captured.IsEquipment && captured.stackCount > 1
                    ? $"x{captured.stackCount}" : "";

            // 클릭 이벤트
            slotObj.GetComponent<Button>()?.onClick.AddListener(
                () => OnInventorySlotClicked(captured));
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

        ShowDetailPopup(item, new Vector2(-400f, 40f));
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

        // 기본 정보
        nameText.text     = item.data.itemName;
        gradeText.text    = $"등급: {GetGradeName(item.data.grade)}";
        itemTypeText.text = GetItemTypeName(item, equip);

        // 메인 옵션
        // Main 0: 강화 단계 (+N) — 강화 0이거나 장신구면 숨김
        // Main 1: 메인 옵션 이름 + 수치 — 장신구는 숨김
        // Main 2: 미사용 (항상 숨김)
        bool hasMainOption = equip != null && item.data.itemType != ItemType.Accessory;

        mainOptionTexts[0].gameObject.SetActive(equip != null && item.enhancementLevel > 0);
        mainOptionTexts[0].text = $"+{item.enhancementLevel}";

        mainOptionTexts[1].gameObject.SetActive(hasMainOption);
        mainOptionTexts[1].text = hasMainOption
            ? $"{GetMainOptionName(equip.mainOptionType)}: {item.GetMainValue():F0}" : "";

        mainOptionTexts[2].gameObject.SetActive(false);

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

        // 버튼 표시
        // 장착 버튼: 인벤토리에서 장비 아이템 선택 시만 표시
        // 해제 버튼: 장착 슬롯 선택 시만 표시
        equipButton  .gameObject.SetActive(!isEquipSlotSelected && item.IsEquipment);
        unequipButton.gameObject.SetActive(isEquipSlotSelected);
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

    string GetItemTypeName(ItemInstance item, EquipItemData equip) =>
        item.data.itemType switch
        {
            ItemType.Weapon    => equip?.weaponType switch
            {
                WeaponType.Sword  => "검",
                WeaponType.Staff  => "지팡이",
                _                 => "무기"
            },
            ItemType.Armor     => equip?.armorType switch
            {
                ArmorType.Hat    => "모자",
                ArmorType.Chest  => "갑옷",
                ArmorType.Gloves => "장갑",
                ArmorType.Boots  => "신발",
                _                => "방어구"
            },
            ItemType.Accessory => equip?.accessoryType switch
            {
                AccessoryType.Necklace => "목걸이",
                AccessoryType.Ring     => "반지",
                _                      => "장신구"
            },
            ItemType.Consumable => "소비 아이템",
            ItemType.Material   => "재료",
            _                   => ""
        };

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

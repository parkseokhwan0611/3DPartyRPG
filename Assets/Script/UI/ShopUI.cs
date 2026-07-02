using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// NPC 상점 UI. NpcInteractable.OnDialogueComplete에서 Open(npc) 호출.
/// 좌측: NPC 판매 목록 / 우측: 플레이어 인벤토리 / 공유 디테일 팝업.
/// </summary>
public class ShopUI : MonoBehaviour
{
    public static ShopUI instance;
    public static bool   IsOpen { get; private set; }

    // ─────────────────────────────────────────────────────────────────
    // Inspector 참조
    // ─────────────────────────────────────────────────────────────────

    [Header("패널")]
    [SerializeField] GameObject      panel;
    [SerializeField] TextMeshProUGUI goldText;
    [SerializeField] Button          closeButton;

    [Header("상점 그리드 (왼쪽)")]
    [SerializeField] Transform  shopContent;
    [SerializeField] GameObject shopSlotPrefab;

    [Header("인벤토리 그리드 (오른쪽)")]
    [SerializeField] Transform  inventoryContent;
    [SerializeField] GameObject inventorySlotPrefab;

    [Header("공유 디테일 팝업")]
    [SerializeField] RectTransform     detailPopup;
    [SerializeField] Image             itemGradeBG;
    [SerializeField] TextMeshProUGUI   nameText;
    [SerializeField] TextMeshProUGUI   gradeText;
    [SerializeField] TextMeshProUGUI   itemTypeText;
    [SerializeField] TextMeshProUGUI[] mainOptionTexts;
    [SerializeField] TextMeshProUGUI[] subOptionTexts;
    [SerializeField] TextMeshProUGUI   descriptionText;
    [SerializeField] TextMeshProUGUI   priceText;
    [Tooltip("장비 구매 / 판매 버튼 (수량 패널 미표시 시 사용)")]
    [SerializeField] Button            buyButton;
    [SerializeField] Button            sellButton;
    [Tooltip("디테일 팝업 취소 버튼 (장비 구매·판매 시 팝업 닫기)")]
    [SerializeField] Button            cancelButton;

    [Header("수량 선택 패널 (소비·재료 구매 시)")]
    [SerializeField] GameObject     quantityPanel;
    [SerializeField] TMP_InputField quantityInput;
    [SerializeField] Slider         quantitySlider;
    [Tooltip("수량 패널 구매 버튼")]
    [SerializeField] Button         buyButtonQuantity;
    [Tooltip("수량 패널 취소 버튼 — 팝업 닫고 그리드 조작 복원")]
    [SerializeField] Button         cancelButtonQuantity;


    [Header("등급 스프라이트 (0:일반 ~ 4:신화)")]
    [SerializeField] Sprite[] gradeSprites;

    // ─────────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────────

    private NpcInteractable _currentNpc;
    private int             _selectedShopIndex = -1;
    private ItemInstance    _selectedInvItem;
    private bool            _syncLock;          // 슬라이더↔인풋 동기화 무한루프 방지
    private CanvasGroup     _shopGridCG;
    private CanvasGroup     _invGridCG;

    // ─────────────────────────────────────────────────────────────────
    // 슬롯 캐시
    // ─────────────────────────────────────────────────────────────────

    private struct ShopSlotCache
    {
        public Image           icon;
        public TextMeshProUGUI stackText;   // 재고 수량 표시
        public Button          button;
        public CanvasGroup     cg;
    }
    private struct InvSlotCache
    {
        public Image           icon;
        public TextMeshProUGUI enhText;
        public TextMeshProUGUI stackText;
        public Button          button;
    }

    private List<ShopSlotCache> _shopSlots = new List<ShopSlotCache>();
    private InvSlotCache[]      _invSlots;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        panel?.SetActive(false);
    }

    void Start()
    {
        // 인벤토리 슬롯 사전 생성
        _invSlots = new InvSlotCache[Inventory.MaxSlots];
        for (int i = 0; i < Inventory.MaxSlots; i++)
        {
            var obj = Instantiate(inventorySlotPrefab, inventoryContent);
            _invSlots[i] = new InvSlotCache
            {
                icon      = obj.transform.Find("Icon")      ?.GetComponent<Image>(),
                enhText   = obj.transform.Find("EnhanceText")?.GetComponent<TextMeshProUGUI>(),
                stackText = obj.transform.Find("StackText")  ?.GetComponent<TextMeshProUGUI>(),
                button    = obj.GetComponent<Button>(),
            };
        }

        // 그리드 CanvasGroup 자동 생성 (Inspector 연결 불필요)
        if (shopContent != null)
        {
            _shopGridCG = shopContent.GetComponent<CanvasGroup>();
            if (_shopGridCG == null) _shopGridCG = shopContent.gameObject.AddComponent<CanvasGroup>();
        }
        if (inventoryContent != null)
        {
            _invGridCG = inventoryContent.GetComponent<CanvasGroup>();
            if (_invGridCG == null) _invGridCG = inventoryContent.gameObject.AddComponent<CanvasGroup>();
        }

        // 수량 슬라이더/인풋 연동
        if (quantitySlider != null)
        {
            quantitySlider.minValue     = 1;
            quantitySlider.maxValue     = 99;
            quantitySlider.wholeNumbers = true;
            quantitySlider.onValueChanged.AddListener(OnSliderChanged);
        }
        if (quantityInput != null)
        {
            quantityInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            quantityInput.onValueChanged.AddListener(OnInputValueChanged); // 실시간 슬라이더 동기화
            quantityInput.onEndEdit.AddListener(OnInputEndEdit);           // 범위 이탈 값 보정
        }

        buyButton           ?.onClick.AddListener(OnBuyClicked);
        buyButtonQuantity   ?.onClick.AddListener(OnBuyClicked);
        sellButton          ?.onClick.AddListener(OnSellClicked);
        cancelButton        ?.onClick.AddListener(CloseDetailPopup);
        cancelButtonQuantity?.onClick.AddListener(CloseDetailPopup);
        closeButton         ?.onClick.AddListener(Close);

        detailPopup?.gameObject.SetActive(false);
        quantityPanel?.SetActive(false);
    }

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        // ESC / Tab → 상점 닫기
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))
        {
            Close();
            return;
        }

        // 팝업 외부 클릭 시 닫기
        if (detailPopup != null && detailPopup.gameObject.activeSelf && Input.GetMouseButtonDown(0))
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(detailPopup, Input.mousePosition))
                CloseDetailPopup();
        }
    }

    void OnEnable()
    {
        if (DataManager.instance != null)
            DataManager.instance.OnGoldChanged += RefreshGold;
    }

    void OnDisable()
    {
        if (DataManager.instance != null)
            DataManager.instance.OnGoldChanged -= RefreshGold;
    }

    // ─────────────────────────────────────────────────────────────────
    // 공개 API
    // ─────────────────────────────────────────────────────────────────

    public void Open(NpcInteractable npc)
    {
        _currentNpc = npc;
        IsOpen = true;
        panel.SetActive(true);
        BuildShopSlots();
        RefreshInventory();
        RefreshGold();
        CloseDetailPopup();
    }

    public void Close()
    {
        CloseDetailPopup();
        IsOpen = false;
        panel?.SetActive(false);
        _currentNpc = null;
    }

    // ─────────────────────────────────────────────────────────────────
    // 슬롯 빌드 / 갱신
    // ─────────────────────────────────────────────────────────────────

    void BuildShopSlots()
    {
        foreach (Transform t in shopContent) Destroy(t.gameObject);
        _shopSlots.Clear();
        if (_currentNpc?.ShopData?.entries == null) return;

        for (int i = 0; i < _currentNpc.ShopData.entries.Count; i++)
        {
            int   idx   = i;
            var   entry = _currentNpc.ShopData.entries[i];
            if (entry?.item == null) continue;

            var obj = Instantiate(shopSlotPrefab, shopContent);
            var cg = obj.GetComponent<CanvasGroup>();
            if (cg == null) cg = obj.AddComponent<CanvasGroup>();

            var cache = new ShopSlotCache
            {
                icon      = obj.transform.Find("Icon")      ?.GetComponent<Image>(),
                stackText = obj.transform.Find("StackText")  ?.GetComponent<TextMeshProUGUI>(),
                button    = obj.GetComponent<Button>(),
                cg        = cg,
            };

            if (cache.icon != null)
            {
                cache.icon.sprite  = entry.item.icon;
                cache.icon.enabled = entry.item.icon != null;
            }

            _shopSlots.Add(cache);
            ApplyShopSlotState(idx, cache);
            cache.button?.onClick.AddListener(() => OnShopSlotClicked(idx));
        }
    }

    // 재고 상태 → 슬롯 알파·상호작용 갱신
    void ApplyShopSlotState(int idx, ShopSlotCache cache)
    {
        if (_currentNpc?.ShopData == null || idx >= _currentNpc.ShopData.entries.Count) return;
        var  entry      = _currentNpc.ShopData.entries[idx];
        int  remaining  = _currentNpc.GetRemainingStock(idx);
        bool outOfStock = entry.hasStockLimit && remaining <= 0;

        if (cache.stackText != null)
            cache.stackText.text = entry.hasStockLimit ? $"{remaining}" : "";

        cache.cg.alpha          = outOfStock ? 0.4f : 1f;
        cache.cg.interactable   = !outOfStock;
        cache.cg.blocksRaycasts = !outOfStock;
    }

    void RefreshAllShopSlots()
    {
        for (int i = 0; i < _shopSlots.Count; i++)
            ApplyShopSlotState(i, _shopSlots[i]);
    }

    void RefreshInventory()
    {
        if (_invSlots == null || DataManager.instance == null) return;
        var items = DataManager.instance.sharedInventory.Items;

        for (int i = 0; i < Inventory.MaxSlots; i++)
        {
            bool         has  = i < items.Count;
            ItemInstance item = has ? items[i] : null;
            var          c    = _invSlots[i];

            if (c.icon != null)
            {
                c.icon.sprite  = has ? item.data.icon : null;
                c.icon.enabled = has && item.data.icon != null;
            }
            if (c.enhText  != null)
                c.enhText.text  = has && item.IsEquipment && item.enhancementLevel > 0
                    ? $"+{item.enhancementLevel}" : "";
            if (c.stackText != null)
                c.stackText.text = has && !item.IsEquipment && item.stackCount > 1
                    ? $"x{item.stackCount}" : "";

            if (c.button != null)
            {
                c.button.onClick.RemoveAllListeners();
                if (has)
                {
                    ItemInstance captured = item;
                    c.button.onClick.AddListener(() => OnInventorySlotClicked(captured));
                }
            }
        }
    }

    void RefreshGold()
    {
        if (goldText == null || DataManager.instance == null) return;
        goldText.text = $"{DataManager.instance.gold:N0} G";
    }

    // ─────────────────────────────────────────────────────────────────
    // 슬롯 클릭
    // ─────────────────────────────────────────────────────────────────

    void OnShopSlotClicked(int entryIndex)
    {
        if (_currentNpc?.ShopData == null) return;
        var entry = _currentNpc.ShopData.entries[entryIndex];
        if (entry?.item == null) return;

        _selectedShopIndex = entryIndex;
        _selectedInvItem   = null;

        // 임시 인스턴스로 아이템 정보 표시 (강화 0)
        var inst = new ItemInstance(entry.item);
        ShowDetailPopup(inst, entry.item.buyPrice, isBuying: true);
    }

    void OnInventorySlotClicked(ItemInstance item)
    {
        _selectedShopIndex = -1;
        _selectedInvItem   = item;

        ShowDetailPopup(item, item.data.sellPrice, isBuying: false);
    }

    // ─────────────────────────────────────────────────────────────────
    // 디테일 팝업
    // ─────────────────────────────────────────────────────────────────

    void ShowDetailPopup(ItemInstance item, int price, bool isBuying)
    {
        detailPopup.gameObject.SetActive(true);
        // 구매(상점 클릭) → 팝업을 오른쪽에, 판매(인벤토리 클릭) → 왼쪽에
        detailPopup.anchoredPosition = isBuying
            ? new Vector2(400f, -40f)
            : new Vector2(-400f, -40f);

        var equip = item.data as EquipItemData;

        int gi = (int)item.data.grade;
        if (itemGradeBG != null && gradeSprites != null && gi < gradeSprites.Length)
            itemGradeBG.sprite = gradeSprites[gi];

        nameText.text     = item.enhancementLevel > 0
            ? $"+{item.enhancementLevel} {item.data.itemName}"
            : item.data.itemName;
        gradeText.text    = $"등급: {GetGradeName(item.data.grade)}";
        itemTypeText.text = GetItemTypeName(item, equip);

        var mainValues = equip != null
            ? new List<(MainOptionType, float)>(item.GetMainValues())
            : null;
        for (int i = 0; i < mainOptionTexts.Length; i++)
        {
            bool show = mainValues != null && i < mainValues.Count;
            mainOptionTexts[i].gameObject.SetActive(show);
            if (show)
                mainOptionTexts[i].text =
                    $"{GetMainOptionName(mainValues[i].Item1)}: {mainValues[i].Item2:F0}";
        }

        int maxSub = GetMaxSubCount(item.data.grade);
        for (int i = 0; i < subOptionTexts.Length; i++)
        {
            bool show = equip != null && i < equip.subOptions.Count && i < maxSub;
            subOptionTexts[i].gameObject.SetActive(show);
            if (show)
                subOptionTexts[i].text =
                    $"{GetSubOptionName(equip.subOptions[i].type)}: +{equip.subOptions[i].value}";
        }

        descriptionText.text = item.data.description;
        priceText.text = isBuying ? $"구매가: {price:N0} G" : $"판매가: {price:N0} G";

        // 수량 선택 패널: 소비·재료 구매 시만 표시
        bool showQty = isBuying && !(item.data is EquipItemData);

        // 수량 패널이 뜰 때는 일반 구매 버튼 숨기고 수량 패널 전용 버튼 사용
        buyButton ?.gameObject.SetActive(isBuying && !showQty);
        sellButton?.gameObject.SetActive(!isBuying);
        cancelButton?.gameObject.SetActive(!showQty);   // 수량 패널 없을 때만 일반 취소 버튼 표시

        quantityPanel?.SetActive(showQty);
        SetGridsInteractable(!showQty);                  // 수량 패널 표시 중 그리드 잠금

        if (showQty && DataManager.instance != null)
        {
            int stockMax = (_currentNpc.ShopData.entries[_selectedShopIndex].hasStockLimit)
                ? _currentNpc.GetRemainingStock(_selectedShopIndex)
                : 99;
            int goldMax = price > 0
                ? Mathf.FloorToInt((float)DataManager.instance.gold / price)
                : 99;
            int maxQty = Mathf.Clamp(Mathf.Min(stockMax, goldMax), 1, 99);

            _syncLock = true;
            if (quantitySlider != null) { quantitySlider.maxValue = maxQty; quantitySlider.value = 1; }
            if (quantityInput  != null) quantityInput.text = "1";
            _syncLock = false;
        }
    }

    void CloseDetailPopup()
    {
        detailPopup?.gameObject.SetActive(false);
        quantityPanel?.SetActive(false);
        SetGridsInteractable(true);
        _selectedShopIndex = -1;
        _selectedInvItem   = null;
    }

    void SetGridsInteractable(bool interactable)
    {
        if (_shopGridCG != null)
        {
            _shopGridCG.interactable   = interactable;
            _shopGridCG.blocksRaycasts = interactable;
        }
        if (_invGridCG != null)
        {
            _invGridCG.interactable   = interactable;
            _invGridCG.blocksRaycasts = interactable;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 구매 / 판매
    // ─────────────────────────────────────────────────────────────────

    void OnBuyClicked()
    {
        if (_currentNpc?.ShopData == null || _selectedShopIndex < 0) return;
        var entry = _currentNpc.ShopData.entries[_selectedShopIndex];
        if (entry?.item == null || DataManager.instance == null) return;

        bool isEquip = entry.item is EquipItemData;
        int  qty     = 1;
        if (!isEquip && quantityInput != null)
            qty = int.TryParse(quantityInput.text, out int v) ? Mathf.Clamp(v, 1, 99) : 1;

        int  totalCost = entry.item.buyPrice * qty;
        var  inv       = DataManager.instance.sharedInventory;

        // 사전 검증 (골드 / 재고 / 인벤토리 공간)
        if (DataManager.instance.gold < totalCost)
        {
            Debug.Log("[ShopUI] 골드 부족");
            return;
        }
        if (entry.hasStockLimit && _currentNpc.GetRemainingStock(_selectedShopIndex) < qty)
        {
            Debug.Log("[ShopUI] 재고 부족");
            return;
        }
        bool hasSpace = isEquip
            ? !inv.IsFull
            : inv.Items.Count < Inventory.MaxSlots ||
              HasExistingStack(inv, entry.item);
        if (!hasSpace)
        {
            Debug.Log("[ShopUI] 인벤토리 가득 참");
            return;
        }

        // 확정: 골드 차감 → 재고 차감 → 아이템 지급
        DataManager.instance.SpendGold(totalCost);
        _currentNpc.TryConsumeStock(_selectedShopIndex, qty);

        var newInst = isEquip
            ? new ItemInstance(entry.item)
            : new ItemInstance(entry.item, qty);
        inv.TryAddItem(newInst);

        RefreshAllShopSlots();
        RefreshInventory();
        CloseDetailPopup();
    }

    void OnSellClicked()
    {
        if (_selectedInvItem == null || DataManager.instance == null) return;

        int price = _selectedInvItem.data.sellPrice;
        DataManager.instance.sharedInventory.Remove(_selectedInvItem);
        DataManager.instance.AddGold(price);

        CloseDetailPopup();
        RefreshInventory();
    }

    // 같은 아이템의 기존 스택이 인벤토리에 있는지 (stackable 공간 체크용)
    static bool HasExistingStack(Inventory inv, ItemData data)
    {
        foreach (var item in inv.Items)
            if (item.data == data) return true;
        return false;
    }

    // ─────────────────────────────────────────────────────────────────
    // 수량 슬라이더 ↔ 인풋 동기화
    // ─────────────────────────────────────────────────────────────────

    // 슬라이더 → 텍스트 실시간 동기화
    void OnSliderChanged(float value)
    {
        if (_syncLock || quantityInput == null) return;
        _syncLock = true;
        quantityInput.text = ((int)value).ToString();
        _syncLock = false;
    }

    // 텍스트 → 슬라이더 실시간 동기화 (타이핑 중)
    void OnInputValueChanged(string s)
    {
        if (_syncLock || quantitySlider == null || string.IsNullOrEmpty(s)) return;
        if (!int.TryParse(s, out int v)) return;
        int max     = (int)quantitySlider.maxValue;
        int clamped = Mathf.Clamp(v, 1, max);
        _syncLock = true;
        quantitySlider.value = clamped;
        _syncLock = false;
    }

    // 텍스트 입력 완료 시 범위 이탈 값 보정
    void OnInputEndEdit(string s)
    {
        if (_syncLock) return;
        int max     = quantitySlider != null ? (int)quantitySlider.maxValue : 99;
        int clamped = int.TryParse(s, out int v) ? Mathf.Clamp(v, 1, max) : 1;
        _syncLock = true;
        if (quantityInput  != null) quantityInput.text   = clamped.ToString();
        if (quantitySlider != null) quantitySlider.value = clamped;
        _syncLock = false;
    }

    // ─────────────────────────────────────────────────────────────────
    // 텍스트 헬퍼 (InventoryUI와 동일)
    // ─────────────────────────────────────────────────────────────────

    static int GetMaxSubCount(ItemGrade grade) => grade switch
    {
        ItemGrade.Normal    => 1,
        ItemGrade.Advanced  => 2,
        ItemGrade.Elite     => 2,
        ItemGrade.Legendary => 3,
        ItemGrade.Mythic    => 4,
        _                   => 0
    };

    static string GetGradeName(ItemGrade grade) => grade switch
    {
        ItemGrade.Normal    => "일반",
        ItemGrade.Advanced  => "고급",
        ItemGrade.Elite     => "영웅",
        ItemGrade.Legendary => "전설",
        ItemGrade.Mythic    => "신화",
        _                   => ""
    };

    static string GetItemTypeName(ItemInstance item, EquipItemData equip)
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

    static string GetMainOptionName(MainOptionType type) => type switch
    {
        MainOptionType.PhysAtk  => "물리 공격력",
        MainOptionType.MagicAtk => "마법 공격력",
        MainOptionType.MaxHP    => "최대 체력",
        MainOptionType.PhysDef  => "방어력",
        MainOptionType.MagicRes => "마법 저항력",
        _                       => ""
    };

    static string GetSubOptionName(SubOptionType type) => type switch
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

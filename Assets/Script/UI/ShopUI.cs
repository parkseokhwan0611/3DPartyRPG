using System.Collections;
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
    [SerializeField] TextMeshProUGUI quantityPriceText;
    [Tooltip("수량 패널 구매 버튼")]
    [SerializeField] Button         buyButtonQuantity;
    [Tooltip("수량 패널 취소 버튼")]
    [SerializeField] Button         cancelButtonQuantity;

    [Header("판매 수량 패널 (소비·재료 판매 시)")]
    [SerializeField] GameObject     sellQuantityPanel;
    [SerializeField] TMP_InputField sellQuantityInput;
    [SerializeField] Slider         sellQuantitySlider;
    [SerializeField] TextMeshProUGUI sellQuantityPriceText;
    [SerializeField] Button         confirmSellButton;
    [SerializeField] Button         cancelSellButton;


    [Header("메시지 텍스트")]
    [SerializeField] TextMeshProUGUI messageText;

    [Header("등급 스프라이트 (0:일반 ~ 4:신화)")]
    [SerializeField] Sprite[] gradeSprites;

    // ─────────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────────

    private NpcInteractable _currentNpc;
    private int             _selectedShopIndex = -1;
    private ItemInstance    _selectedInvItem;
    private bool            _syncLock;           // 구매 슬라이더↔인풋 동기화 무한루프 방지
    private bool            _sellSyncLock;       // 판매 슬라이더↔인풋 동기화 무한루프 방지
    private Coroutine       _messageClearRoutine;

    // ─────────────────────────────────────────────────────────────────
    // 슬롯 캐시
    // ─────────────────────────────────────────────────────────────────

    private struct ShopSlotCache
    {
        public Image           icon;
        public TextMeshProUGUI enhText;    // 강화 텍스트 (상점에선 항상 빈 문자열)
        public TextMeshProUGUI stackText;  // 재고 수량 표시
        public Button          button;
        public Image           bg;         // 슬롯 배경 (빈 칸 반투명 표시용)
        public int             entryIndex; // ShopData.entries 내 원래 인덱스
    }
    private struct InvSlotCache
    {
        public Image           icon;
        public TextMeshProUGUI enhText;
        public TextMeshProUGUI stackText;
        public Button          button;
    }

    private ShopSlotCache[] _shopSlots;
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

        // 상점 슬롯 사전 생성 (열 때마다 파괴/재생성하지 않고 재사용)
        _shopSlots = new ShopSlotCache[SHOP_GRID_SIZE];
        for (int i = 0; i < SHOP_GRID_SIZE; i++)
        {
            var obj = Instantiate(shopSlotPrefab, shopContent);
            int slotIndex = i;
            _shopSlots[i] = new ShopSlotCache
            {
                icon       = obj.transform.Find("Icon")        ?.GetComponent<Image>(),
                enhText    = obj.transform.Find("EnhanceText") ?.GetComponent<TextMeshProUGUI>(),
                stackText  = obj.transform.Find("StackText")   ?.GetComponent<TextMeshProUGUI>(),
                button     = obj.GetComponent<Button>(),
                bg         = obj.GetComponent<Image>(),
                entryIndex = -1,
            };
            _shopSlots[i].button?.onClick.AddListener(() => OnShopSlotClicked(_shopSlots[slotIndex].entryIndex));
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
        buyButtonQuantity   ?.onClick.AddListener(OnBuyQuantityConfirmed);
        sellButton          ?.onClick.AddListener(OnSellClicked);
        cancelButton        ?.onClick.AddListener(CloseDetailPopup);
        cancelButtonQuantity?.onClick.AddListener(CloseQuantityPanel);
        closeButton         ?.onClick.AddListener(Close);

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

        detailPopup?.gameObject.SetActive(false);
        quantityPanel?.SetActive(false);
        sellQuantityPanel?.SetActive(false);
    }

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        // ESC 우선순위: 판매수량 → 구매수량 → 디테일팝업 → 상점 전체 닫기
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (sellQuantityPanel != null && sellQuantityPanel.activeSelf) { CloseSellQuantityPanel(); return; }
            if (quantityPanel     != null && quantityPanel.activeSelf)     { CloseQuantityPanel();     return; }
            if (detailPopup       != null && detailPopup.gameObject.activeSelf) { CloseDetailPopup(); return; }
            Close();
            return;
        }

        // 구매 또는 판매 수량 패널 열려있으면 외부 클릭 감지 비활성화
        bool qtyOpen = (quantityPanel     != null && quantityPanel.activeSelf)
                    || (sellQuantityPanel != null && sellQuantityPanel.activeSelf);

        // 팝업 외부 클릭 시 닫기 (수량 패널 닫혀있을 때만)
        if (!qtyOpen && detailPopup != null && detailPopup.gameObject.activeSelf && Input.GetMouseButtonDown(0))
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

    void OnDestroy()
    {
        IsOpen = false;
    }

    // ─────────────────────────────────────────────────────────────────
    // 공개 API
    // ─────────────────────────────────────────────────────────────────

    public NpcInteractable CurrentNpc => _currentNpc;

    public void Open(NpcInteractable npc)
    {
        _currentNpc = npc;
        IsOpen = true;
        panel.SetActive(true);
        BuildShopSlots();
        RefreshInventory();
        RefreshGold();
        ClearMessage();
        CloseDetailPopup();
        AudioManager.instance?.PlaySFX("UIOpen");
    }

    public void Close()
    {
        CloseDetailPopup();
        IsOpen = false;
        panel?.SetActive(false);
        _currentNpc = null;
        AudioManager.instance?.PlaySFX("UIClose");
    }

    // ─────────────────────────────────────────────────────────────────
    // 슬롯 빌드 / 갱신
    // ─────────────────────────────────────────────────────────────────

    // 상점 그리드는 항상 5×5 = 25칸으로 표시
    private const int SHOP_GRID_SIZE = 25;

    void BuildShopSlots()
    {
        var entries    = _currentNpc?.ShopData?.entries;
        int entryCount = entries != null ? entries.Count : 0;

        for (int i = 0; i < _shopSlots.Length; i++)
        {
            var cache = _shopSlots[i];
            var entry = i < entryCount ? entries[i] : null;

            if (entry?.item != null)
            {
                cache.entryIndex = i;

                if (cache.enhText != null) cache.enhText.text = "";
                if (cache.icon != null)
                {
                    cache.icon.sprite  = entry.item.icon;
                    cache.icon.enabled = entry.item.icon != null;
                    cache.icon.color   = Color.white;
                }

                // 빈 칸 배경 투명도 원복
                if (cache.bg != null) cache.bg.color = new Color(cache.bg.color.r, cache.bg.color.g, cache.bg.color.b, 1f);

                _shopSlots[i] = cache;
                ApplyShopSlotState(i, cache);
            }
            else
            {
                cache.entryIndex = -1;
                if (cache.icon      != null) { cache.icon.sprite = null; cache.icon.enabled = false; }
                if (cache.enhText   != null) cache.enhText.text   = "";
                if (cache.stackText != null) cache.stackText.text = "";

                // 슬롯 배경을 반투명으로 → 빈 칸 시각적 구분
                if (cache.bg != null) cache.bg.color = new Color(cache.bg.color.r, cache.bg.color.g, cache.bg.color.b, 0.25f);

                if (cache.button != null) cache.button.interactable = false;
                _shopSlots[i] = cache;
            }
        }
    }

    // 재고 상태 → 슬롯 아이콘 색상·버튼 활성 갱신
    void ApplyShopSlotState(int idx, ShopSlotCache cache)
    {
        if (_currentNpc?.ShopData == null || idx < 0 || idx >= _currentNpc.ShopData.entries.Count) return;
        var  entry      = _currentNpc.ShopData.entries[idx];
        int  remaining  = _currentNpc.GetRemainingStock(idx);
        bool outOfStock = entry.hasStockLimit && remaining <= 0;

        if (cache.stackText != null)
            cache.stackText.text = entry.hasStockLimit ? $"{remaining}" : "";

        // 품절 시 아이콘 반투명 + 버튼 비활성
        if (cache.icon   != null) cache.icon.color       = outOfStock ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        if (cache.button != null) cache.button.interactable = !outOfStock;
    }

    void RefreshAllShopSlots()
    {
        if (_shopSlots == null) return;
        for (int i = 0; i < _shopSlots.Length; i++)
            if (_shopSlots[i].entryIndex >= 0)
                ApplyShopSlotState(_shopSlots[i].entryIndex, _shopSlots[i]);
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

        // 팝업이 구매 상태로 열려있는 도중 골드가 바뀌면 버튼 활성 상태도 갱신
        if (buyButton != null && buyButton.gameObject.activeSelf
            && _selectedShopIndex >= 0 && _currentNpc?.ShopData != null
            && _selectedShopIndex < _currentNpc.ShopData.entries.Count)
        {
            int price = _currentNpc.ShopData.entries[_selectedShopIndex].item.buyPrice;
            buyButton.interactable = DataManager.instance.gold >= price;
        }
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

        // 구매 시: 구매 버튼 표시 / 판매 시: 판매 버튼 표시
        buyButton ?.gameObject.SetActive(isBuying);
        sellButton?.gameObject.SetActive(!isBuying);
        cancelButton?.gameObject.SetActive(true);

        // 하나도 살 수 없는 골드량이면 구매 버튼 자체를 비활성화
        if (isBuying && buyButton != null)
            buyButton.interactable = DataManager.instance != null && DataManager.instance.gold >= price;

        // 수량 패널은 구매 버튼 클릭 시 열리므로, 팝업 열 때는 항상 닫아둠
        quantityPanel?.SetActive(false);
        SetGridsInteractable(true);
    }

    void CloseDetailPopup()
    {
        detailPopup?.gameObject.SetActive(false);
        quantityPanel?.SetActive(false);
        sellQuantityPanel?.SetActive(false);
        SetGridsInteractable(true);
        _selectedShopIndex = -1;
        _selectedInvItem   = null;
    }

    void SetGridsInteractable(bool interactable)
    {
        // 인벤토리 그리드: 모든 버튼 일괄 토글
        if (inventoryContent != null)
            foreach (Transform child in inventoryContent)
            {
                var btn = child.GetComponent<Button>();
                if (btn != null) btn.interactable = interactable;
            }

        // 상점 그리드: 잠금은 일괄 비활성, 잠금 해제는 품절 상태 복원
        if (shopContent != null)
        {
            if (!interactable)
            {
                foreach (Transform child in shopContent)
                {
                    var btn = child.GetComponent<Button>();
                    if (btn != null) btn.interactable = false;
                }
            }
            else
            {
                RefreshAllShopSlots(); // 품절 여부 반영해서 복원
            }
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

        if (!isEquip)
        {
            // 소비·재료 → 수량 패널 열기
            OpenQuantityPanel(entry.item.buyPrice);
            return;
        }

        // 장비 → 바로 구매
        ExecutePurchase(entry, 1);
    }

    void OpenQuantityPanel(int unitPrice)
    {
        if (_currentNpc?.ShopData == null || _selectedShopIndex < 0) return;
        if (DataManager.instance == null) return;
        var entry = _currentNpc.ShopData.entries[_selectedShopIndex];

        // 재고 한도와 골드 한도 중 작은 값이 실제 구매 가능 수량
        int stockMax      = entry.hasStockLimit
            ? _currentNpc.GetRemainingStock(_selectedShopIndex)
            : 99;
        int maxAffordable = unitPrice > 0 ? DataManager.instance.gold / unitPrice : 99;
        int maxQty        = Mathf.Clamp(Mathf.Min(stockMax, maxAffordable), 0, 99);

        _syncLock = true;
        if (quantitySlider != null)
        {
            quantitySlider.maxValue = Mathf.Max(1, maxQty);
            quantitySlider.value    = 1;
        }
        // 살 수 없으면 0 표시, 슬라이더는 맨 왼쪽(min)
        if (quantityInput != null) quantityInput.text = maxQty > 0 ? "1" : "0";
        _syncLock = false;
        UpdateBuyQuantityPriceText(maxQty > 0 ? 1 : 0);

        if (maxQty <= 0) ShowMessage("골드가 부족합니다.");

        buyButton   ?.gameObject.SetActive(false);
        cancelButton?.gameObject.SetActive(false);
        quantityPanel?.SetActive(true);
        SetGridsInteractable(false);
    }

    void CloseQuantityPanel()
    {
        quantityPanel?.SetActive(false);
        buyButton   ?.gameObject.SetActive(true);
        cancelButton?.gameObject.SetActive(true);
        SetGridsInteractable(true);
    }

    // 선택한 수량 기준 총 구매가 표시
    void UpdateBuyQuantityPriceText(int qty)
    {
        if (quantityPriceText == null) return;
        if (_currentNpc?.ShopData == null || _selectedShopIndex < 0
            || _selectedShopIndex >= _currentNpc.ShopData.entries.Count)
        {
            quantityPriceText.text = "";
            return;
        }

        int unitPrice = _currentNpc.ShopData.entries[_selectedShopIndex].item.buyPrice;
        quantityPriceText.text = $"구매가: {unitPrice * qty:N0} G";
    }

    void OnBuyQuantityConfirmed()
    {
        if (_currentNpc?.ShopData == null || _selectedShopIndex < 0) return;
        var entry = _currentNpc.ShopData.entries[_selectedShopIndex];
        if (entry?.item == null || DataManager.instance == null) return;

        int qty = quantityInput != null && int.TryParse(quantityInput.text, out int v)
            ? Mathf.Clamp(v, 1, 99) : 1;
        ExecutePurchase(entry, qty);
    }

    void ExecutePurchase(ShopEntry entry, int qty)
    {
        if (DataManager.instance == null) return;
        bool isEquip   = entry.item is EquipItemData;
        int  totalCost = entry.item.buyPrice * qty;
        var  inv       = DataManager.instance.sharedInventory;

        // 사전 검증 (판매 가능 여부 / 골드 / 재고 / 인벤토리 공간)
        if (entry.item.buyPrice <= 0)
        {
            ShowMessage("판매하지 않는 아이템입니다.");
            return;
        }
        if (DataManager.instance.gold < totalCost)
        {
            ShowMessage("골드가 부족합니다.");
            return;
        }
        if (entry.hasStockLimit && _currentNpc.GetRemainingStock(_selectedShopIndex) < qty)
        {
            ShowMessage("재고가 부족합니다.");
            return;
        }
        bool hasSpace = isEquip
            ? !inv.IsFull
            : inv.Items.Count < Inventory.MaxSlots || HasExistingStack(inv, entry.item);
        if (!hasSpace)
        {
            ShowMessage("인벤토리가 가득 찼습니다.");
            return;
        }

        // 확정: 골드 차감 → 재고 차감 → 아이템 지급
        if (!DataManager.instance.SpendGold(totalCost)) return;
        _currentNpc.TryConsumeStock(_selectedShopIndex, qty);

        var newInst = isEquip
            ? new ItemInstance(entry.item)
            : new ItemInstance(entry.item, qty);
        inv.TryAddItem(newInst);

        // 퀵슬롯에 등록된 포션이면 전투 UI 수량 갱신
        PotionQuickSlotManager.instance?.RefreshIfRegistered(entry.item);

        AudioManager.instance?.PlaySFX("Money");

        RefreshAllShopSlots();
        RefreshInventory();
        CloseDetailPopup();
    }

    void OnSellClicked()
    {
        if (_selectedInvItem == null || DataManager.instance == null) return;

        // 소비·재료는 판매 수량 패널 열기
        if (!_selectedInvItem.IsEquipment)
        {
            OpenSellQuantityPanel();
            return;
        }

        // 장비는 바로 판매
        ExecuteSell(1);
    }

    void OpenSellQuantityPanel()
    {
        if (_selectedInvItem == null) return;
        int maxQty = Mathf.Clamp(_selectedInvItem.stackCount, 1, 99);

        _sellSyncLock = true;
        if (sellQuantitySlider != null) { sellQuantitySlider.maxValue = maxQty; sellQuantitySlider.value = 1; }
        if (sellQuantityInput  != null) sellQuantityInput.text = "1";
        _sellSyncLock = false;
        UpdateSellQuantityPriceText(1);

        sellButton       ?.gameObject.SetActive(false);
        cancelButton     ?.gameObject.SetActive(false);
        sellQuantityPanel?.SetActive(true);
        SetGridsInteractable(false);
    }

    // 선택한 수량 기준 총 판매가 표시
    void UpdateSellQuantityPriceText(int qty)
    {
        if (sellQuantityPriceText == null || _selectedInvItem?.data == null) return;
        sellQuantityPriceText.text = $"판매가: {_selectedInvItem.data.sellPrice * qty:N0} G";
    }

    void CloseSellQuantityPanel()
    {
        sellQuantityPanel?.SetActive(false);
        sellButton  ?.gameObject.SetActive(true);
        cancelButton?.gameObject.SetActive(true);
        SetGridsInteractable(true);
    }

    void OnConfirmSellClicked()
    {
        if (_selectedInvItem == null || DataManager.instance == null) return;
        int qty = sellQuantityInput != null && int.TryParse(sellQuantityInput.text, out int v)
            ? Mathf.Clamp(v, 1, _selectedInvItem.stackCount) : 1;
        ExecuteSell(qty);
    }

    void ExecuteSell(int qty)
    {
        if (_selectedInvItem == null || DataManager.instance == null) return;
        var      inv        = DataManager.instance.sharedInventory;
        int      totalPrice = _selectedInvItem.data.sellPrice * qty;
        ItemData soldData   = _selectedInvItem.data;

        if (_selectedInvItem.IsEquipment)
            inv.Remove(_selectedInvItem);
        else
            inv.ConsumeItem(_selectedInvItem, qty);

        DataManager.instance.AddGold(totalPrice);
        AudioManager.instance?.PlaySFX("Money");

        // 판매 아이템이 포션 퀵슬롯에 등록된 경우 갱신 또는 해제
        if (!_selectedInvItem.IsEquipment)
        {
            var mgr = PotionQuickSlotManager.instance;
            if (mgr != null)
            {
                if (_selectedInvItem.stackCount <= 0)
                {
                    // 전부 팔린 경우 — 해당 타입 슬롯 해제
                    if (mgr.GetSlot(ConsumableType.HpPotion)?.data == soldData)
                        mgr.DeregisterPotion(ConsumableType.HpPotion);
                    else if (mgr.GetSlot(ConsumableType.MpPotion)?.data == soldData)
                        mgr.DeregisterPotion(ConsumableType.MpPotion);
                }
                else
                {
                    // 일부만 판 경우 — 수량 UI만 갱신
                    mgr.RefreshIfRegistered(soldData);
                }
            }
        }

        RefreshInventory();
        CloseDetailPopup();
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
        UpdateSellQuantityPriceText(clamped);
        _sellSyncLock = false;
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
        UpdateBuyQuantityPriceText((int)value);
        if (_syncLock || quantityInput == null) return;
        _syncLock = true;
        quantityInput.text = ((int)value).ToString();
        _syncLock = false;
    }

    // 텍스트 → 슬라이더 실시간 동기화 (타이핑 중)
    void OnInputValueChanged(string s)
    {
        if (int.TryParse(s, out int typed) && typed >= 1)
            UpdateBuyQuantityPriceText(typed);

        if (_syncLock || quantitySlider == null || string.IsNullOrEmpty(s)) return;
        if (!int.TryParse(s, out int v) || v < 1) return;
        int max     = (int)quantitySlider.maxValue;
        int clamped = Mathf.Clamp(v, 1, max);
        _syncLock = true;
        // 최대치 초과 입력 시 텍스트도 즉시 보정
        if (clamped != v && quantityInput != null)
            quantityInput.text = clamped.ToString();
        quantitySlider.value = clamped;
        _syncLock = false;
    }

    // 텍스트 입력 완료 시 범위 이탈 값 보정
    void OnInputEndEdit(string s)
    {
        if (_syncLock) return;
        int max     = quantitySlider != null ? (int)quantitySlider.maxValue : 99;
        int clamped = int.TryParse(s, out int v) ? Mathf.Clamp(v, 1, max) : 1;
        UpdateBuyQuantityPriceText(clamped);
        _syncLock = true;
        if (quantityInput  != null) quantityInput.text   = clamped.ToString();
        if (quantitySlider != null) quantitySlider.value = clamped;
        _syncLock = false;
    }

    // ─────────────────────────────────────────────────────────────────
    // 메시지
    // ─────────────────────────────────────────────────────────────────

    void ShowMessage(string msg)
    {
        if (messageText == null) return;
        messageText.text = msg;
        if (_messageClearRoutine != null) StopCoroutine(_messageClearRoutine);
        _messageClearRoutine = StartCoroutine(ClearMessageAfterDelay(2.5f));
    }

    void ClearMessage()
    {
        if (_messageClearRoutine != null) { StopCoroutine(_messageClearRoutine); _messageClearRoutine = null; }
        if (messageText != null) messageText.text = "";
    }

    private IEnumerator ClearMessageAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (messageText != null) messageText.text = "";
        _messageClearRoutine = null;
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

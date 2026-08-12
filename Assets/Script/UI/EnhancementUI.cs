using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnhancementUI : MonoBehaviour
{
    public static EnhancementUI instance;
    public static bool IsOpen { get; private set; }

    // ─────────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────────

    [Header("루트 패널")]
    [SerializeField] GameObject panel;
    [SerializeField] Button     closeButton;

    [Header("강화 대상 슬롯 (좌)")]
    [SerializeField] Image           equipIcon;
    [SerializeField] TextMeshProUGUI equipNameText;
    [SerializeField] TextMeshProUGUI equipLevelText;   // "+3 / 7"
    [SerializeField] TextMeshProUGUI equipStatText;    // 메인 옵션 수치
    [SerializeField] GameObject      equipEmptyMsg;    // "장비를 등록하세요" 오브젝트
    [SerializeField] Button          equipSlotButton;  // 클릭 시 장비 해제

    [Header("주문서 슬롯 (좌)")]
    [SerializeField] Image           scrollIcon;
    [SerializeField] TextMeshProUGUI scrollNameText;
    [SerializeField] TextMeshProUGUI successRateText;
    [SerializeField] GameObject      scrollEmptyMsg;   // "주문서를 등록하세요" 오브젝트
    [SerializeField] Button          scrollSlotButton; // 클릭 시 주문서 해제

    [Header("강화 실행 (좌)")]
    [SerializeField] Slider          progressBar;
    [SerializeField] Button          enhanceButton;
    [SerializeField] GameObject      successText;       // 성공 텍스트 오브젝트
    [SerializeField] GameObject      failText;          // 실패 텍스트 오브젝트
    [SerializeField] GameObject      mismatchWarning;   // 주문서-장비 타입 불일치 경고 (선택)

    [Header("인벤토리 패널 (우)")]
    [SerializeField] Transform       invContent;
    [SerializeField] GameObject      invSlotPrefab;
    [SerializeField] TextMeshProUGUI goldText;

    [Header("DetailPopup (우)")]
    [SerializeField] RectTransform   detailPopup;
    [SerializeField] Image           popupGradeBG;
    [SerializeField] TextMeshProUGUI popupNameText;
    [SerializeField] TextMeshProUGUI popupGradeText;
    [SerializeField] TextMeshProUGUI popupTypeText;
    [SerializeField] TextMeshProUGUI[] popupMainTexts;  // 최대 3
    [SerializeField] TextMeshProUGUI[] popupSubTexts;   // 최대 4
    [SerializeField] TextMeshProUGUI popupDescText;
    [SerializeField] TextMeshProUGUI popupPriceText;
    [SerializeField] Button          enrollButton;
    [SerializeField] Button          unEnrollButton;
    [SerializeField] Button          cancelButton;

    [Header("등급 스프라이트 (0:일반 ~ 4:신화)")]
    [SerializeField] Sprite[] gradeSprites;

    // ─────────────────────────────────────────────────────────────────
    // 런타임 상태
    // ─────────────────────────────────────────────────────────────────

    private ItemInstance _equip;           // 강화 대상 (인벤토리에서 참조만)
    private ItemInstance _scroll;          // 주문서 ItemInstance (차감 전 원본 참조)
    private bool         _scrollBorrowed;  // 인벤토리에서 1개 차감됐는지

    private ItemInstance _popupItem;       // DetailPopup에 표시 중

    private struct SlotCache
    {
        public Image             icon;
        public TextMeshProUGUI   enhText;
        public TextMeshProUGUI   stackText;
        public Button            button;
        public EnhancementDragItem drag;
    }
    private SlotCache[] _slots;
    private Coroutine   _enhRoutine;
    private Coroutine   _resultRoutine;

    private const float PROGRESS_DURATION = 1f;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) { instance = null; IsOpen = false; }
    }

    void Start()
    {
        enhanceButton?.onClick.AddListener(OnEnhanceClicked);
        closeButton  ?.onClick.AddListener(Close);
        enrollButton  ?.onClick.AddListener(OnEnrollClicked);
        unEnrollButton?.onClick.AddListener(OnUnEnrollClicked);
        cancelButton  ?.onClick.AddListener(CloseDetailPopup);

        equipSlotButton ?.onClick.AddListener(() => { if (_equip  != null)      UnregisterEquip();  });
        scrollSlotButton?.onClick.AddListener(() => { if (_scrollBorrowed)      UnregisterScroll(); });

        SetupDropTargets();
        BuildInventorySlots();

        // 진행률 표시 전용 — 플레이어가 드래그로 값을 바꿀 수 없도록 잠금
        if (progressBar != null) progressBar.interactable = false;

        if (panel != null) panel.SetActive(false);
        HideResult();
    }

    void Update()
    {
        if (detailPopup != null && detailPopup.gameObject.activeSelf && Input.GetMouseButtonDown(0))
            if (!RectTransformUtility.RectangleContainsScreenPoint(detailPopup, Input.mousePosition))
                CloseDetailPopup();
    }

    // ─────────────────────────────────────────────────────────────────
    // 열기 / 닫기
    // ─────────────────────────────────────────────────────────────────

    public void Open()
    {
        if (panel != null) panel.SetActive(true);
        IsOpen = true;

        if (_enhRoutine != null) { StopCoroutine(_enhRoutine); _enhRoutine = null; }
        if (closeButton != null) closeButton.interactable = true;

        _equip = null;
        ReturnScroll();
        CloseDetailPopup();
        HideResult();

        if (progressBar != null) progressBar.value = 0f;

        RefreshEquipSlot();
        RefreshScrollSlot();
        RefreshInventory();
        RefreshEnhanceButton();
        RefreshGold();
        AudioManager.instance?.PlaySFX("UIOpen");
    }

    public void Close()
    {
        if (_enhRoutine != null) return; // 강화 진행 중에는 닫기 불가

        ReturnScroll();
        _equip = null;
        if (panel != null) panel.SetActive(false);
        IsOpen = false;
        CloseDetailPopup();
        AudioManager.instance?.PlaySFX("UIClose");
    }

    // ─────────────────────────────────────────────────────────────────
    // 등록 / 해제 (DetailPopup 버튼 & 드롭 타겟에서 호출)
    // ─────────────────────────────────────────────────────────────────

    public void RegisterEquip(ItemInstance item)
    {
        if (item == null || !item.IsEquipment) return;
        _equip = item;
        CloseDetailPopup();
        RefreshEquipSlot();
        RefreshEnhanceButton();
        RefreshInventory();
    }

    public void RegisterScroll(ItemInstance item)
    {
        if (item?.data is not ConsumableData cd) return;
        if (cd.consumableType != ConsumableType.WeaponScroll &&
            cd.consumableType != ConsumableType.ArmorScroll) return;
        if (item.stackCount <= 0) return;

        ReturnScroll(); // 기존 주문서 먼저 반환

        bool consumed = DataManager.instance != null && DataManager.instance.sharedInventory.ConsumeItem(item, 1);
        if (!consumed) return; // 소비 실패 시 등록도 하지 않음 (상태 불일치 방지)

        _scroll         = item;
        _scrollBorrowed = true;

        CloseDetailPopup();
        RefreshScrollSlot();
        RefreshEnhanceButton();
        RefreshInventory();
    }

    // 등록 대상 자동 분기 (장비/주문서) — 더블클릭·등록 버튼 공용
    public void RegisterItem(ItemInstance item)
    {
        if (item == null) return;
        if (item == _equip || item == _scroll) return; // 이미 등록됨

        if (item.IsEquipment)
            RegisterEquip(item);
        else if (item.data is ConsumableData cd
              && (cd.consumableType == ConsumableType.WeaponScroll
               || cd.consumableType == ConsumableType.ArmorScroll))
            RegisterScroll(item);
    }

    public void UnregisterEquip()
    {
        _equip = null;
        CloseDetailPopup();
        RefreshEquipSlot();
        RefreshEnhanceButton();
        RefreshInventory();
    }

    public void UnregisterScroll()
    {
        ReturnScroll();
        CloseDetailPopup();
        RefreshScrollSlot();
        RefreshEnhanceButton();
        RefreshInventory();
    }

    // 주문서를 인벤토리에 돌려줌 (UI 닫기 / 다른 주문서 교체 시)
    private void ReturnScroll()
    {
        if (!_scrollBorrowed || _scroll == null) return;

        _scroll.AddStack(1);

        var inv = DataManager.instance?.sharedInventory;
        if (inv != null)
        {
            bool found = false;
            foreach (var it in inv.Items) { if (it == _scroll) { found = true; break; } }
            if (!found) inv.TryAddItem(_scroll);
        }

        _scroll         = null;
        _scrollBorrowed = false;
    }

    // ─────────────────────────────────────────────────────────────────
    // 강화 실행
    // ─────────────────────────────────────────────────────────────────

    private void OnEnhanceClicked()
    {
        if (_equip == null || _scroll?.data is not ConsumableData cd) return;
        if (_enhRoutine != null) return;

        enhanceButton.interactable = false;
        if (closeButton != null) closeButton.interactable = false; // 강화 진행 중 닫기 버튼 잠금
        HideResult();
        if (progressBar != null) progressBar.value = 0f;

        // 결과 먼저 결정
        int ownerIndex = FindEquippedPartyIndex(_equip);
        var result = EnhancementSystem.TryEnhance(_equip, cd, ownerIndex);

        // 주문서 소모 확정 (ReturnScroll에서 반환하지 않도록)
        _scrollBorrowed = false;
        _scroll         = null;

        _enhRoutine = StartCoroutine(PlayProgressThenShow(result));
    }

    private IEnumerator PlayProgressThenShow(EnhanceResult result)
    {
        // 슬라이더 0 → 1 애니메이션
        float elapsed = 0f;
        while (elapsed < PROGRESS_DURATION)
        {
            elapsed += Time.unscaledDeltaTime;
            if (progressBar != null) progressBar.value = Mathf.Clamp01(elapsed / PROGRESS_DURATION);
            yield return null;
        }
        if (progressBar != null) progressBar.value = 1f;

        // 결과 표시
        ShowResult(result == EnhanceResult.Success);

        // 장비 / 주문서 슬롯 갱신
        RefreshEquipSlot();
        RefreshScrollSlot();
        RefreshInventory();

        yield return new WaitForSecondsRealtime(0.3f);
        if (progressBar != null) progressBar.value = 0f;

        _enhRoutine = null;
        if (closeButton != null) closeButton.interactable = true;
        RefreshEnhanceButton();
    }

    // ─────────────────────────────────────────────────────────────────
    // 결과 텍스트
    // ─────────────────────────────────────────────────────────────────

    private void ShowResult(bool success)
    {
        if (successText != null) successText.SetActive(success);
        if (failText    != null) failText.SetActive(!success);

        AudioManager.instance?.PlaySFX(success ? "EnhanceSuccess" : "EnhanceFail");

        if (_resultRoutine != null) StopCoroutine(_resultRoutine);
        _resultRoutine = StartCoroutine(HideResultAfter(2.5f));
    }

    private void HideResult()
    {
        if (_resultRoutine != null) { StopCoroutine(_resultRoutine); _resultRoutine = null; }
        if (successText != null) successText.SetActive(false);
        if (failText    != null) failText.SetActive(false);
    }

    private IEnumerator HideResultAfter(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        HideResult();
    }

    // ─────────────────────────────────────────────────────────────────
    // 강화 대상 슬롯 표시 갱신
    // ─────────────────────────────────────────────────────────────────

    private void RefreshEquipSlot()
    {
        bool has = _equip != null;

        if (equipEmptyMsg  != null) equipEmptyMsg.SetActive(!has);
        if (equipIcon      != null) equipIcon.gameObject.SetActive(has);
        if (equipNameText  != null) equipNameText.gameObject.SetActive(has);
        if (equipLevelText != null) equipLevelText.gameObject.SetActive(has);
        if (equipStatText  != null) equipStatText.gameObject.SetActive(has);

        if (!has) return;

        var equip = (EquipItemData)_equip.data;
        int cur   = _equip.enhancementLevel;
        int max   = equip.MaxEnhancement;

        if (equipIcon      != null) { equipIcon.sprite = _equip.data.icon; equipIcon.color = Color.white; }
        if (equipNameText  != null) equipNameText.text  = _equip.data.itemName;
        if (equipLevelText != null) equipLevelText.text = $"+{cur} / {max}";

        if (equipStatText != null)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (type, val) in _equip.GetMainValues())
                sb.AppendLine($"{GetMainOptionLabel(type)}: {val:F0}");
            equipStatText.text = sb.ToString().TrimEnd();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 주문서 슬롯 표시 갱신
    // ─────────────────────────────────────────────────────────────────

    private void RefreshScrollSlot()
    {
        bool has = _scrollBorrowed && _scroll != null;

        if (scrollEmptyMsg  != null) scrollEmptyMsg.SetActive(!has);
        if (scrollIcon      != null) scrollIcon.gameObject.SetActive(has);
        if (scrollNameText  != null) scrollNameText.gameObject.SetActive(has);
        if (successRateText != null) successRateText.gameObject.SetActive(has);

        if (!has) return;

        if (scrollIcon     != null) { scrollIcon.sprite = _scroll.data.icon; scrollIcon.color = Color.white; }
        if (scrollNameText != null) scrollNameText.text = _scroll.data.itemName;

        if (_scroll.data is ConsumableData cd && successRateText != null)
            successRateText.text = $"강화 확률: {cd.successRate * 100:F0}%";
    }

    private void RefreshEnhanceButton()
    {
        if (enhanceButton == null) return;

        bool hasEquip  = _equip != null && _equip.data is EquipItemData;
        bool hasScroll = _scrollBorrowed && _scroll?.data is ConsumableData;

        bool compatible = hasEquip && hasScroll
            && EnhancementSystem.IsScrollCompatible(
                (ConsumableData)_scroll.data, (EquipItemData)_equip.data);

        bool can = compatible
            && _equip.enhancementLevel < ((EquipItemData)_equip.data).MaxEnhancement
            && _enhRoutine == null;

        enhanceButton.interactable = can;

        if (mismatchWarning != null)
            mismatchWarning.SetActive(hasEquip && hasScroll && !compatible);
    }

    private void RefreshGold()
    {
        if (goldText == null || DataManager.instance == null) return;
        goldText.text = $"{DataManager.instance.gold:N0}";
    }

    // ─────────────────────────────────────────────────────────────────
    // 드롭 타겟 자동 설정
    // ─────────────────────────────────────────────────────────────────

    private void SetupDropTargets()
    {
        AddDropTarget(equipSlotButton,  EnhancementSlotDropTarget.SlotType.Equipment);
        AddDropTarget(scrollSlotButton, EnhancementSlotDropTarget.SlotType.Scroll);
    }

    private static void AddDropTarget(Button btn, EnhancementSlotDropTarget.SlotType type)
    {
        if (btn == null) return;
        var dt = btn.GetComponent<EnhancementSlotDropTarget>()
              ?? btn.gameObject.AddComponent<EnhancementSlotDropTarget>();
        dt.slotType = type;
    }

    // ─────────────────────────────────────────────────────────────────
    // 인벤토리 그리드 (우)
    // ─────────────────────────────────────────────────────────────────

    private void BuildInventorySlots()
    {
        if (invContent == null || invSlotPrefab == null) return;

        _slots = new SlotCache[Inventory.MaxSlots];
        for (int i = 0; i < Inventory.MaxSlots; i++)
        {
            var go   = Instantiate(invSlotPrefab, invContent);
            var drag = go.AddComponent<EnhancementDragItem>();
            _slots[i] = new SlotCache
            {
                icon      = go.transform.Find("Icon")        ?.GetComponent<Image>(),
                enhText   = go.transform.Find("EnhanceText") ?.GetComponent<TextMeshProUGUI>(),
                stackText = go.transform.Find("StackText")   ?.GetComponent<TextMeshProUGUI>(),
                button    = go.GetComponent<Button>(),
                drag      = drag,
            };
        }
    }

    private void RefreshInventory()
    {
        if (_slots == null) return;

        var items = DataManager.instance?.sharedInventory.Items
                    ?? (IReadOnlyList<ItemInstance>)System.Array.Empty<ItemInstance>();

        for (int i = 0; i < _slots.Length; i++)
        {
            bool         has  = i < items.Count;
            ItemInstance item = has ? items[i] : null;
            ref SlotCache c   = ref _slots[i];

            bool registered = item != null && (item == _equip || item == _scroll);

            if (c.icon != null)
            {
                c.icon.sprite  = has ? item.data.icon : null;
                c.icon.enabled = has;
                c.icon.color   = registered ? new Color(0.55f, 0.85f, 1f) : Color.white;
            }
            if (c.enhText != null)
                c.enhText.text = has && item.IsEquipment && item.enhancementLevel > 0
                    ? $"+{item.enhancementLevel}" : "";
            if (c.stackText != null)
                c.stackText.text = has && !item.IsEquipment && item.stackCount > 1
                    ? $"x{item.stackCount}" : "";

            var captured = item;
            if (c.button != null)
            {
                c.button.onClick.RemoveAllListeners();
                if (has) c.button.onClick.AddListener(() => OnInvSlotClicked(captured));
            }
            if (c.drag != null) c.drag.item = item;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // DetailPopup
    // ─────────────────────────────────────────────────────────────────

    private void OnInvSlotClicked(ItemInstance item)
    {
        if (item == null) return;
        _popupItem = item;
        ShowDetailPopup(item);
    }

    private void ShowDetailPopup(ItemInstance item)
    {
        if (detailPopup == null) return;
        detailPopup.gameObject.SetActive(true);

        var equip = item.data as EquipItemData;

        // 등급 배경
        int gi = (int)item.data.grade;
        if (popupGradeBG != null && gradeSprites != null && gi < gradeSprites.Length)
            popupGradeBG.sprite = gradeSprites[gi];

        // 기본 정보
        if (popupNameText != null)
            popupNameText.text = item.enhancementLevel > 0
                ? $"+{item.enhancementLevel} {item.data.itemName}" : item.data.itemName;
        if (popupGradeText != null) popupGradeText.text = $"등급: {GetGradeName(item.data.grade)}";
        if (popupTypeText  != null) popupTypeText.text  = GetTypeName(item, equip);

        // 메인 옵션
        var mains = equip != null
            ? new List<(MainOptionType, float)>(item.GetMainValues()) : null;
        for (int i = 0; i < popupMainTexts.Length; i++)
        {
            bool show = mains != null && i < mains.Count;
            popupMainTexts[i].gameObject.SetActive(show);
            if (show) popupMainTexts[i].text =
                $"{GetMainOptionLabel(mains[i].Item1)}: {mains[i].Item2:F0}";
        }

        // 서브 옵션
        int maxSub = GetMaxSubCount(item.data.grade);
        for (int i = 0; i < popupSubTexts.Length; i++)
        {
            bool show = equip != null && i < equip.subOptions.Count && i < maxSub;
            popupSubTexts[i].gameObject.SetActive(show);
            if (show) popupSubTexts[i].text =
                $"{GetSubOptionLabel(equip.subOptions[i].type)}: +{equip.subOptions[i].value}";
        }

        if (popupDescText  != null) popupDescText.text  = item.data.description;
        if (popupPriceText != null) popupPriceText.text = $"판매가: {item.data.sellPrice:N0}";

        // 등록 / 해제 버튼 표시 결정
        bool canEnroll   = false;
        bool canUnEnroll = false;

        if (item.IsEquipment)
        {
            canEnroll   = item != _equip;
            canUnEnroll = item == _equip;
        }
        else if (item.data is ConsumableData cd
              && (cd.consumableType == ConsumableType.WeaponScroll
               || cd.consumableType == ConsumableType.ArmorScroll))
        {
            // 같은 ItemInstance가 이미 등록돼 있으면 해제, 아니면 등록
            canEnroll   = item != _scroll && item.stackCount > 0;
            canUnEnroll = item == _scroll;
        }

        if (enrollButton   != null) enrollButton  .gameObject.SetActive(canEnroll);
        if (unEnrollButton != null) unEnrollButton.gameObject.SetActive(canUnEnroll);
    }

    private void CloseDetailPopup()
    {
        if (detailPopup != null) detailPopup.gameObject.SetActive(false);
        _popupItem = null;
    }

    private void OnEnrollClicked() => RegisterItem(_popupItem);

    private void OnUnEnrollClicked()
    {
        if (_popupItem == null) return;
        if (_popupItem == _equip)
            UnregisterEquip();
        else if (_popupItem == _scroll)
            UnregisterScroll();
    }

    // ─────────────────────────────────────────────────────────────────
    // 헬퍼
    // ─────────────────────────────────────────────────────────────────

    private int FindEquippedPartyIndex(ItemInstance target)
    {
        if (DataManager.instance == null) return -1;
        var equipments = DataManager.instance.partyEquipments;
        for (int i = 0; i < equipments.Count; i++)
            foreach (var kvp in equipments[i].Slots)
                if (kvp.Value == target) return i;
        return -1;
    }

    private static string GetMainOptionLabel(MainOptionType t) => t switch
    {
        MainOptionType.PhysAtk  => "물리 공격력",
        MainOptionType.MagicAtk => "마법 공격력",
        MainOptionType.MaxHP    => "최대 체력",
        MainOptionType.PhysDef  => "방어력",
        MainOptionType.MagicRes => "마법 저항력",
        _ => t.ToString(),
    };

    private static string GetSubOptionLabel(SubOptionType t) => t switch
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
        _ => t.ToString(),
    };

    private static string GetGradeName(ItemGrade g) => g switch
    {
        ItemGrade.Normal    => "일반",
        ItemGrade.Advanced  => "고급",
        ItemGrade.Elite     => "영웅",
        ItemGrade.Legendary => "전설",
        ItemGrade.Mythic    => "신화",
        _ => ""
    };

    private static int GetMaxSubCount(ItemGrade g) => g switch
    {
        ItemGrade.Normal    => 1,
        ItemGrade.Advanced  => 2,
        ItemGrade.Elite     => 2,
        ItemGrade.Legendary => 3,
        ItemGrade.Mythic    => 4,
        _ => 0
    };

    private static string GetTypeName(ItemInstance item, EquipItemData equip)
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
            _ => ""
        };
        if (item.IsConsumable) return "소비 아이템";
        return "";
    }
}

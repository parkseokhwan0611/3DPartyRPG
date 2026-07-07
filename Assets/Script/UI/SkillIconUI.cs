using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SkillIconUI : MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("# UI 참조")]
    public Image iconImage;
    public Image lockOverlay;
    public TextMeshProUGUI levelText;

    public SkillData SkillData    { get; private set; }
    public int SkillLevel         { get; private set; }
    public bool IsUnlocked        { get; private set; }
    public int RequiredLevel      { get; private set; }

    private SkillWindowUI skillWindow;
    private Canvas rootCanvas;
    private GameObject dragIcon;

    void Awake()
    {
        // FindObjectOfType 대신 부모에서 직접 찾기
        rootCanvas = GetComponentInParent<Canvas>();
        
        // 루트 캔버스를 찾아야 함 (중첩 캔버스 문제 방지)
        if (rootCanvas != null)
            rootCanvas = rootCanvas.rootCanvas;
    }

    public void Setup(SkillData skill, int level, bool unlocked, SkillWindowUI window, int requiredLevel)
    {
        SkillData     = skill;
        SkillLevel    = level;
        IsUnlocked    = unlocked;
        RequiredLevel = requiredLevel;
        skillWindow   = window;

        if (iconImage != null)  iconImage.sprite = skill.icon;
        if (levelText != null)  levelText.text   = level > 0 ? level.ToString() : "";
        if (lockOverlay != null) lockOverlay.gameObject.SetActive(!unlocked);
    }

    public void SetEmpty()
    {
        SkillData     = null;
        SkillLevel    = 0;
        IsUnlocked    = false;
        RequiredLevel = 0;

        if (iconImage != null)   iconImage.sprite = null;
        if (levelText != null)   levelText.text   = "";
        if (lockOverlay != null) lockOverlay.gameObject.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────
    // 클릭 → 상세 패널
    // ─────────────────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        // 드래그 후 클릭 이벤트 방지
        if (eventData.dragging) return;
        if (SkillData == null) return;
        skillWindow.OnSkillIconClicked(SkillData, RequiredLevel);
    }

    // ─────────────────────────────────────────────────────────────────
    // 드래그 앤 드롭
    // ─────────────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 습득하지 않은 스킬은 드래그 불가
        if (SkillData == null || SkillLevel <= 0) return;
        if (rootCanvas == null) return;

        // 패시브 스킬은 드래그 불가
        if (SkillData.skillType == SkillData.SkillType.Passive)
        {
            Debug.Log("[SkillIconUI] 패시브 스킬은 퀵슬롯에 등록할 수 없습니다.");
            return;
        }

        // 드래그 임시 아이콘 생성
        dragIcon = new GameObject("DragIcon");
        dragIcon.transform.SetParent(rootCanvas.transform, false);
        dragIcon.transform.SetAsLastSibling();

        Image img         = dragIcon.AddComponent<Image>();
        img.sprite        = iconImage.sprite;
        img.raycastTarget = false; // 드래그 아이콘이 레이캐스트를 막지 않도록

        RectTransform rt = dragIcon.GetComponent<RectTransform>();
        rt.sizeDelta     = new Vector2(60, 60);

        UpdateDragIconPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragIcon == null) return;
        UpdateDragIconPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragIcon != null)
        {
            Destroy(dragIcon);
            dragIcon = null;
        }
    }

    private void UpdateDragIconPosition(PointerEventData eventData)
    {
        if (dragIcon == null || rootCanvas == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.GetComponent<RectTransform>(),
            eventData.position,
            rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera,
            out Vector2 localPoint);

        dragIcon.GetComponent<RectTransform>().localPosition = localPoint;
    }
}
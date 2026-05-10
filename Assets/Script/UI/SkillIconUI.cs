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
    public Image lockOverlay;      // 잠금 오버레이 (레벨 미달 시 어둡게)
    public TextMeshProUGUI levelText; // 현재 스킬 레벨 표시

    // ─────────────────────────────────────────
    // 데이터
    // ─────────────────────────────────────────
    public SkillData SkillData    { get; private set; }
    public int SkillLevel         { get; private set; }
    public bool IsUnlocked        { get; private set; }

    private SkillWindowUI skillWindow;

    // ─────────────────────────────────────────
    // 드래그 관련
    // ─────────────────────────────────────────
    private GameObject dragIcon;       // 드래그 중 표시할 임시 아이콘
    private Canvas rootCanvas;

    // ─────────────────────────────────────────────────────────────────
    // 초기화
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
    }

    public void Setup(SkillData skill, int level, bool unlocked, SkillWindowUI window)
    {
        SkillData  = skill;
        SkillLevel = level;
        IsUnlocked = unlocked;
        skillWindow = window;

        // 아이콘 설정
        if (iconImage != null)
            iconImage.sprite = skill.icon;

        // 레벨 표시
        if (levelText != null)
            levelText.text = level > 0 ? level.ToString() : "";

        // 잠금 오버레이
        if (lockOverlay != null)
            lockOverlay.gameObject.SetActive(!unlocked);
    }

    public void SetEmpty()
    {
        SkillData  = null;
        SkillLevel = 0;
        IsUnlocked = false;

        if (iconImage != null) iconImage.sprite = null;
        if (levelText != null) levelText.text   = "";
        if (lockOverlay != null) lockOverlay.gameObject.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────
    // 클릭 → 상세 패널 표시
    // ─────────────────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        if (SkillData == null) return;
        skillWindow.OnSkillIconClicked(SkillData);
    }

    // ─────────────────────────────────────────────────────────────────
    // 드래그 앤 드롭 (퀵슬롯에 등록)
    // ─────────────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 습득하지 않은 스킬은 드래그 불가
        if (SkillData == null || SkillLevel <= 0) return;

        // 드래그 중 표시할 임시 아이콘 생성
        dragIcon = new GameObject("DragIcon");
        dragIcon.transform.SetParent(rootCanvas.transform, false);
        dragIcon.transform.SetAsLastSibling();

        Image img       = dragIcon.AddComponent<Image>();
        img.sprite      = iconImage.sprite;
        img.raycastTarget = false;

        RectTransform rt = dragIcon.GetComponent<RectTransform>();
        rt.sizeDelta     = new Vector2(60, 60);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragIcon == null) return;

        // 마우스 위치로 아이콘 이동
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.GetComponent<RectTransform>(),
            eventData.position,
            rootCanvas.worldCamera,
            out Vector2 localPoint);

        dragIcon.GetComponent<RectTransform>().localPosition = localPoint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragIcon != null)
        {
            Destroy(dragIcon);
            dragIcon = null;
        }
    }
}
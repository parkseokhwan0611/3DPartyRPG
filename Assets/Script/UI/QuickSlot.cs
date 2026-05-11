using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class QuickSlot : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Header("# UI 참조")]
    public Image iconImage;
    public TextMeshProUGUI keyText;
    public Image cooldownOverlay;

    [Header("# 슬롯 설정")]
    public int slotIndex; // 0=Q, 1=W, 2=E, 3=R

    public SkillData AssignedSkill { get; private set; }
    private QuickSlotUI quickSlotUI;

    void Awake()
    {
        quickSlotUI = GetComponentInParent<QuickSlotUI>();
    }

    void Start()
    {
        if (cooldownOverlay != null)
        {
            cooldownOverlay.type       = Image.Type.Filled;
            cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
            cooldownOverlay.gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 드롭 처리
    // ─────────────────────────────────────────────────────────────────

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        SkillIconUI draggedIcon = eventData.pointerDrag.GetComponent<SkillIconUI>();
        if (draggedIcon == null) return;

        if (draggedIcon.SkillData == null)
        {
            Debug.Log("[QuickSlot] 스킬 데이터가 없습니다.");
            return;
        }

        if (draggedIcon.SkillLevel <= 0)
        {
            Debug.Log("[QuickSlot] 습득하지 않은 스킬은 등록할 수 없습니다.");
            return;
        }

        SetSkill(draggedIcon.SkillData);

        if (quickSlotUI != null)
            quickSlotUI.RegisterSkill(slotIndex, draggedIcon.SkillData);
    }

    // 슬롯 우클릭으로 스킬 해제
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
            ClearSkill();
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 설정 / 해제
    // ─────────────────────────────────────────────────────────────────

    public void SetSkill(SkillData skill)
    {
        AssignedSkill = skill;

        if (iconImage != null)
        {
            iconImage.sprite = skill != null ? skill.icon : null;
            iconImage.color  = skill != null ? Color.white : new Color(1f, 1f, 1f, 0.3f);
        }
    }

    public void ClearSkill()
    {
        SetSkill(null);
        if (quickSlotUI != null)
            quickSlotUI.RegisterSkill(slotIndex, null);
    }

    // ─────────────────────────────────────────────────────────────────
    // 쿨다운 오버레이
    // ─────────────────────────────────────────────────────────────────

    public void UpdateCooldown(float ratio)
    {
        if (cooldownOverlay == null) return;
        cooldownOverlay.gameObject.SetActive(ratio > 0f);
        cooldownOverlay.fillAmount = ratio;
    }
}
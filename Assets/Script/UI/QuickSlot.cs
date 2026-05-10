using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class QuickSlot : MonoBehaviour, IDropHandler
{
    [Header("# UI 참조")]
    public Image iconImage;
    public TextMeshProUGUI keyText;   // Q / W / E / R 텍스트
    public Image cooldownOverlay;     // 쿨다운 오버레이 (Image Type: Filled)

    [Header("# 슬롯 설정")]
    public int slotIndex; // 0=Q, 1=W, 2=E, 3=R

    private SkillData assignedSkill;
    private QuickSlotUI quickSlotUI;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        quickSlotUI = GetComponentInParent<QuickSlotUI>();
    }

    void Start()
    {
        // 쿨다운 오버레이 초기화
        if (cooldownOverlay != null)
        {
            cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
            cooldownOverlay.gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 드롭 처리 (SkillIconUI에서 드래그 앤 드롭)
    // ─────────────────────────────────────────────────────────────────

    public void OnDrop(PointerEventData eventData)
    {
        SkillIconUI draggedIcon = eventData.pointerDrag?.GetComponent<SkillIconUI>();
        if (draggedIcon == null) return;

        // 습득한 스킬만 등록 가능
        if (draggedIcon.SkillLevel <= 0)
        {
            Debug.Log("[QuickSlot] 습득하지 않은 스킬은 등록할 수 없습니다.");
            return;
        }

        SetSkill(draggedIcon.SkillData);
        quickSlotUI.RegisterSkill(slotIndex, draggedIcon.SkillData);
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 표시
    // ─────────────────────────────────────────────────────────────────

    public void SetSkill(SkillData skill)
    {
        assignedSkill = skill;

        if (iconImage != null)
        {
            iconImage.sprite = skill != null ? skill.icon : null;
            iconImage.color  = skill != null ? Color.white : new Color(1f, 1f, 1f, 0.3f);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 쿨다운 오버레이 갱신 (매 프레임 SkillManager에서 호출)
    // ─────────────────────────────────────────────────────────────────

    public void UpdateCooldown(float ratio)
    {
        if (cooldownOverlay == null) return;

        cooldownOverlay.gameObject.SetActive(ratio > 0f);
        cooldownOverlay.fillAmount = ratio;
    }
}
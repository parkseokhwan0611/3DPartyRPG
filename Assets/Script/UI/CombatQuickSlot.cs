using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class CombatQuickSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("# UI 참조")]
    public Image iconImage;                  // 스킬 아이콘
    public Image cooldownOverlay;            // 쿨다운 오버레이
    public TextMeshProUGUI keyText;          // Q/W/E/R 텍스트
    public TextMeshProUGUI cooldownText;     // 쿨다운 남은 시간
    public Image buffDurationBar;            // 버프 지속시간 바

    // 호버 디테일 팝업용 — 스킬/물약 중 등록된 쪽만 채워짐
    private SkillBase    _skill;
    private ItemInstance _potionItem;

    // ─────────────────────────────────────────────────────────────────
    // 스킬 표시
    // ─────────────────────────────────────────────────────────────────

    public void SetSkill(SkillBase skill)
    {
        _skill      = skill;
        _potionItem = null;

        if (iconImage == null) return;

        SkillData data = skill?.skillData;
        if (data != null && data.icon != null)
        {
            iconImage.sprite = data.icon;
            iconImage.color  = Color.white;
        }
        else
        {
            iconImage.sprite = null;
            iconImage.color  = new Color(1f, 1f, 1f, 0f); // 투명
        }
    }

    // 포션 슬롯 표시 (HP/MP 퀵슬롯 전용) — 디테일 팝업용으로 ItemInstance까지 함께 보관
    public void SetPotion(ItemInstance item)
    {
        _potionItem = item;
        _skill      = null;
        SetIcon(item?.data?.icon);
    }

    public void SetIcon(Sprite icon)
    {
        if (iconImage == null) return;
        iconImage.sprite = icon;
        iconImage.color  = icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
    }

    // ─────────────────────────────────────────────────────────────────
    // 쿨다운 오버레이
    // ─────────────────────────────────────────────────────────────────

    public void UpdateCooldown(float ratio, float remaining, float buffRemainingRatio)
    {
        // 쿨다운 오버레이
        if (cooldownOverlay != null)
        {
            cooldownOverlay.gameObject.SetActive(ratio > 0f);
            cooldownOverlay.fillAmount = ratio;
        }

        // 쿨다운 텍스트
        if (cooldownText != null)
        {
            if (remaining > 0f)
            {
                cooldownText.gameObject.SetActive(true);
                cooldownText.text = FormatTime(remaining);
            }
            else
            {
                cooldownText.gameObject.SetActive(false);
            }
        }

        // 버프 지속시간 바
        if (buffDurationBar != null)
        {
            buffDurationBar.gameObject.SetActive(buffRemainingRatio > 0f);
            buffDurationBar.fillAmount = buffRemainingRatio;
        }
    }

    // 1 이상: 정수, 1 미만: 소수점 한 자리
    private string FormatTime(float time)
    {
        return time >= 1f
            ? Mathf.CeilToInt(time).ToString()
            : time.ToString("F1");
    }

    // ─────────────────────────────────────────────────────────────────
    // 디테일 팝업 (호버) — 스킬/물약 슬롯 공용 팝업 하나를 재사용
    // ─────────────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_skill?.skillData != null)
            CombatDetailPopupUI.instance?.ShowSkill(_skill, transform as RectTransform);
        else if (_potionItem != null)
            CombatDetailPopupUI.instance?.ShowPotion(_potionItem, transform as RectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 즉시 숨기지 않고 약간 대기 — 스크롤하려고 마우스를 팝업 쪽으로 옮기는 동안 사라지는 것 방지
        CombatDetailPopupUI.instance?.RequestHide();
    }
}
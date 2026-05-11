using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatQuickSlot : MonoBehaviour
{
    [Header("# UI 참조")]
    public Image iconImage;           // Icon 오브젝트의 Image
    public Image cooldownOverlay;     // 쿨다운 오버레이 Image
    public TextMeshProUGUI keyText;   // Q/W/E/R 텍스트 (있으면)

    // ─────────────────────────────────────────────────────────────────
    // 스킬 표시
    // ─────────────────────────────────────────────────────────────────

    public void SetSkill(SkillData skill)
    {
        if (iconImage == null) return;

        if (skill != null && skill.icon != null)
        {
            iconImage.sprite = skill.icon;
            iconImage.color  = Color.white;
        }
        else
        {
            iconImage.sprite = null;
            iconImage.color  = new Color(1f, 1f, 1f, 0f); // 투명
        }
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
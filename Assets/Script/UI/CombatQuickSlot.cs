using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatQuickSlot : MonoBehaviour
{
    [Header("# UI 참조")]
    public Image iconImage;                     // 스킬 아이콘
    public Image cooldownOverlay;               // 쿨다운 오버레이
    public TextMeshProUGUI keyText;             // Q/W/E/R 텍스트
    public TextMeshProUGUI cooldownText;        // 쿨다운 남은 시간
    public TextMeshProUGUI buffDurationText;    // 버프 지속 시간

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

    public void UpdateCooldown(float ratio, float remaining, float buffRemaining)
    {
        // 오버레이
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

        // 버프 지속시간 텍스트
        if (buffDurationText != null)
        {
            if (buffRemaining > 0f)
            {
                buffDurationText.gameObject.SetActive(true);
                buffDurationText.text = FormatTime(buffRemaining);
            }
            else
            {
                buffDurationText.gameObject.SetActive(false);
            }
        }
    }

    // 1 이상: 정수, 1 미만: 소수점 한 자리
    private string FormatTime(float time)
    {
        return time >= 1f
            ? Mathf.CeilToInt(time).ToString()
            : time.ToString("F1");
    }
}
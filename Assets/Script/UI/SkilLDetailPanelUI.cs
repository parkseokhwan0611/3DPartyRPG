using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillDetailPanelUI : MonoBehaviour
{
    [Header("# 스킬 포인트")]
    public TextMeshProUGUI skillPointText;

    [Header("# 현재 스킬 정보")]
    public TextMeshProUGUI skillNameText;
    public TextMeshProUGUI skillLvText;
    public TextMeshProUGUI skillDescText;
    public TextMeshProUGUI mpCostText;       // 패시브는 표시 안 함
    public TextMeshProUGUI cooldownText;
    public TextMeshProUGUI damageText;       // 데미지 스킬 전용
    public TextMeshProUGUI specialText;      // 버프/패시브 효과

    [Header("# 다음 레벨 정보")]
    public TextMeshProUGUI nextSkillLvText;
    public TextMeshProUGUI levelConditionText;
    public TextMeshProUGUI requiredSkillPointText;
    public TextMeshProUGUI nextMpCostText;
    public TextMeshProUGUI nextCooldownText;
    public TextMeshProUGUI nextDamageText;
    public TextMeshProUGUI nextSpecialText;

    [Header("# 버튼")]
    public Button learnButton;

    private SkillData currentSkill;
    private int currentCharIndex;
    private SkillWindowUI skillWindow;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Start()
    {
        skillWindow = GetComponentInParent<SkillWindowUI>();
        if (learnButton != null)
            learnButton.onClick.AddListener(OnLearnButtonClicked);
        Clear();
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 포인트 갱신
    // ─────────────────────────────────────────────────────────────────

    public void RefreshSkillPoint(int point)
    {
        if (skillPointText != null)
            skillPointText.text = $"Skill Point: {point}";
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 상세 표시
    // ─────────────────────────────────────────────────────────────────

    public void ShowSkillDetail(SkillData skill, CharacterStatus status, int partyLevel, int charIndex)
    {
        currentSkill     = skill;
        currentCharIndex = charIndex;

        int currentLevel = status.GetSkillLevel(skill);
        int nextLevel    = currentLevel + 1;
        bool isMaxLevel  = currentLevel >= skill.maxLevel;
        bool isPassive   = skill.skillType == SkillData.SkillType.Passive;

        // ── 기본 정보 ──
        SetTextSafe(skillNameText, skill.skillName);
        SetTextSafe(skillLvText,   currentLevel > 0 ? $"LV {currentLevel}" : "미습득");
        SetTextSafe(skillDescText, skill.description);

        // ── 현재 레벨 수치 ──
        if (currentLevel > 0)
        {
            int idx = currentLevel - 1;

            // 패시브는 마나 소모 없음
            if (!isPassive && idx < skill.mpCost.Length)
                SetTextSafe(mpCostText, $"마나 소모량: {skill.mpCost[idx]}");
            else
                SetTextSafe(mpCostText, "");

            // 패시브는 쿨다운 없음
            if (!isPassive && idx < skill.cooldown.Length)
                SetTextSafe(cooldownText, $"쿨타임: {skill.cooldown[idx]}초");
            else
                SetTextSafe(cooldownText, "");

            ShowCurrentStatByType(skill, currentLevel);
        }
        else
        {
            SetTextSafe(mpCostText,   "");
            SetTextSafe(cooldownText, "");
            SetTextSafe(damageText,   "");
            SetTextSafe(specialText,  "");
        }

        // ── 다음 레벨 수치 ──
        if (!isMaxLevel)
        {
            int nextIdx = nextLevel - 1;

            SetTextSafe(nextSkillLvText, $"LV {nextLevel}");

            if (!isPassive && nextIdx < skill.mpCost.Length)
                SetTextSafe(nextMpCostText, $"마나 소모량: {skill.mpCost[nextIdx]}");
            else
                SetTextSafe(nextMpCostText, "");

            if (!isPassive && nextIdx < skill.cooldown.Length)
                SetTextSafe(nextCooldownText, $"쿨타임: {skill.cooldown[nextIdx]}초");
            else
                SetTextSafe(nextCooldownText, "");

            ShowNextStatByType(skill, nextLevel);

            // 필요 스킬 포인트
            if (nextIdx < skill.skillPointCost.Length)
            {
                int cost = skill.skillPointCost[nextIdx];
                SetTextSafe(requiredSkillPointText, $"필요 스킬 포인트: {cost}");
                SetTextSafe(levelConditionText, $"스킬 포인트 {status.skillPoint} 보유");

                if (learnButton != null)
                    learnButton.interactable = status.skillPoint >= cost;
            }
        }
        else
        {
            SetTextSafe(nextSkillLvText,        "최대 레벨");
            SetTextSafe(nextMpCostText,         "");
            SetTextSafe(nextCooldownText,       "");
            SetTextSafe(nextDamageText,         "");
            SetTextSafe(nextSpecialText,        "");
            SetTextSafe(requiredSkillPointText, "");
            SetTextSafe(levelConditionText,     "");
            if (learnButton != null) learnButton.interactable = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 타입별 현재 레벨 계수
    // ─────────────────────────────────────────────────────────────────

    private void ShowCurrentStatByType(SkillData skill, int level)
    {
        if (skill is DamageSkillData dmgSkill)
        {
            SetTextSafe(damageText,  $"데미지: {dmgSkill.GetDamageMultiplier(level) * 100f:F1}%");
            SetTextSafe(specialText, "");
        }
        else if (skill is BuffSkillData buffSkill)
        {
            SetTextSafe(damageText,  "");
            SetTextSafe(specialText, GetBuffDescription(buffSkill, level));
        }
        else if (skill is PassiveSkillData passiveSkill)
        {
            SetTextSafe(damageText,  "");
            SetTextSafe(specialText, GetPassiveDescription(passiveSkill, level));
        }
        else
        {
            SetTextSafe(damageText,  "");
            SetTextSafe(specialText, "");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 타입별 다음 레벨 계수
    // ─────────────────────────────────────────────────────────────────

    private void ShowNextStatByType(SkillData skill, int nextLevel)
    {
        if (skill is DamageSkillData dmgSkill)
        {
            SetTextSafe(nextDamageText,  $"데미지: {dmgSkill.GetDamageMultiplier(nextLevel) * 100f:F1}%");
            SetTextSafe(nextSpecialText, "");
        }
        else if (skill is BuffSkillData buffSkill)
        {
            SetTextSafe(nextDamageText,  "");
            SetTextSafe(nextSpecialText, GetBuffDescription(buffSkill, nextLevel));
        }
        else if (skill is PassiveSkillData passiveSkill)
        {
            SetTextSafe(nextDamageText,  "");
            SetTextSafe(nextSpecialText, GetPassiveDescription(passiveSkill, nextLevel));
        }
        else
        {
            SetTextSafe(nextDamageText,  "");
            SetTextSafe(nextSpecialText, "");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 버프 설명 생성 (최대 3개 효과)
    // ─────────────────────────────────────────────────────────────────

    private string GetBuffDescription(BuffSkillData buff, int level)
    {
        if (buff.buffEffects == null || buff.buffEffects.Count == 0) return "";

        string result = buff.isPartyBuff ? "[파티 버프]\n" : "[개인 버프]\n";
        result += $"지속시간: {buff.GetDuration(level)}초\n";

        foreach (var effect in buff.buffEffects)
        {
            float value = effect.GetValue(level);
            switch (effect.effectType)
            {
                case BuffSkillData.BuffEffectType.AtkBonus:
                    result += $"공격력 +{value}\n";
                    break;
                case BuffSkillData.BuffEffectType.ApBonus:
                    result += $"주문력 +{value}\n";
                    break;
                case BuffSkillData.BuffEffectType.DefBonus:
                    result += $"방어력 +{value}\n";
                    break;
                case BuffSkillData.BuffEffectType.CritRate:
                    result += $"치명타 확률 +{value * 100f:F1}%\n";
                    break;
                case BuffSkillData.BuffEffectType.CritDamage:
                    result += $"치명타 배율 +{value * 100f:F1}%\n";
                    break;
                case BuffSkillData.BuffEffectType.MaxHpBonus:
                    result += $"최대 체력 +{value}\n";
                    break;
                case BuffSkillData.BuffEffectType.SpeedBonus:
                    result += $"이동속도 +{value}\n";
                    break;
            }
        }

        return result.TrimEnd('\n');
    }

    // ─────────────────────────────────────────────────────────────────
    // 패시브 설명 생성
    // ─────────────────────────────────────────────────────────────────

    private string GetPassiveDescription(PassiveSkillData passive, int level)
    {
        switch (passive.effectType)
        {
            case PassiveSkillData.PassiveEffectType.AtkPercent:
                return $"공격력 {passive.GetValue(level) * 100f:F1}% 증가";
            case PassiveSkillData.PassiveEffectType.ApPercent:
                return $"주문력 {passive.GetValue(level) * 100f:F1}% 증가";
            case PassiveSkillData.PassiveEffectType.DefPercent:
                return $"방어력 {passive.GetValue(level) * 100f:F1}% 증가";
            case PassiveSkillData.PassiveEffectType.CritRate:
                return $"치명타 확률 +{passive.GetValue(level) * 100f:F1}%";
            case PassiveSkillData.PassiveEffectType.CritDamage:
                return $"치명타 배율 +{passive.GetValue(level) * 100f:F1}%";
            case PassiveSkillData.PassiveEffectType.MaxHpPercent:
                return $"최대 체력 {passive.GetValue(level) * 100f:F1}% 증가";
            case PassiveSkillData.PassiveEffectType.OnCritLightning:
                return $"치명타 시 번개 발동 확률 {passive.GetProcChance(level) * 100f:F1}%";
            case PassiveSkillData.PassiveEffectType.OnHitPoison:
                return $"공격 시 독 발동 확률 {passive.GetProcChance(level) * 100f:F1}%";
            case PassiveSkillData.PassiveEffectType.OnKillHeal:
                return $"적 처치 시 체력 {passive.GetProcValue(level)} 회복";
            default:
                return "";
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 배우기 버튼
    // ─────────────────────────────────────────────────────────────────

    private void OnLearnButtonClicked()
    {
        if (currentSkill == null || DataManager.instance == null) return;

        CharacterStatus status = DataManager.instance.partyStatuses[currentCharIndex];
        bool success           = status.TryLevelUpSkill(currentSkill);

        if (!success)
        {
            Debug.Log("[SkillDetailPanelUI] 스킬 레벨업 실패");
            return;
        }

        skillWindow.OnSkillLevelUp();
        ShowSkillDetail(currentSkill, status, DataManager.instance.partyLevel, currentCharIndex);
    }

    // ─────────────────────────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────────────────────────

    private void SetTextSafe(TextMeshProUGUI tmp, string text)
    {
        if (tmp != null) tmp.text = text;
    }

    public void Clear()
    {
        currentSkill = null;

        SetTextSafe(skillNameText,          "");
        SetTextSafe(skillLvText,            "");
        SetTextSafe(skillDescText,          "");
        SetTextSafe(mpCostText,             "");
        SetTextSafe(cooldownText,           "");
        SetTextSafe(damageText,             "");
        SetTextSafe(specialText,            "");
        SetTextSafe(nextSkillLvText,        "");
        SetTextSafe(levelConditionText,     "");
        SetTextSafe(requiredSkillPointText, "");
        SetTextSafe(nextMpCostText,         "");
        SetTextSafe(nextCooldownText,       "");
        SetTextSafe(nextDamageText,         "");
        SetTextSafe(nextSpecialText,        "");

        if (learnButton != null) learnButton.interactable = false;
    }
}
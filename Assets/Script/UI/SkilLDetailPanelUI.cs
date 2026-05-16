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
    public TextMeshProUGUI mpCostText;
    public TextMeshProUGUI cooldownText;
    public TextMeshProUGUI damageText;
    public TextMeshProUGUI specialText;

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

    void Start()
    {
        skillWindow = GetComponentInParent<SkillWindowUI>();
        if (learnButton != null)
            learnButton.onClick.AddListener(OnLearnButtonClicked);
        Clear();
    }

    public void RefreshSkillPoint(int point)
    {
        if (skillPointText != null)
            skillPointText.text = $"Skill Point: {point}";
    }

    public void ShowSkillDetail(SkillData skill, CharacterStatus status, int partyLevel, int charIndex)
    {
        currentSkill     = skill;
        currentCharIndex = charIndex;

        int currentLevel = status.GetSkillLevel(skill);
        int nextLevel    = currentLevel + 1;
        bool isMaxLevel  = currentLevel >= skill.maxLevel;
        bool isPassive   = skill.skillType == SkillData.SkillType.Passive;

        SetTextSafe(skillNameText, skill.skillName);
        SetTextSafe(skillLvText,   currentLevel > 0 ? $"LV {currentLevel}" : "미습득");
        SetTextSafe(skillDescText, skill.description);

        // ── 현재 레벨 수치 ──
        if (currentLevel > 0)
        {
            int idx = currentLevel - 1;

            if (!isPassive && idx < skill.mpCost.Length)
                SetTextSafe(mpCostText, $"마나 소모량: {skill.mpCost[idx]}");
            else
                SetTextSafe(mpCostText, "");

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

    private void ShowCurrentStatByType(SkillData skill, int level)
    {
        if (skill is DamageSkillData dmgSkill)
        {
            SetTextSafe(damageText,  $"데미지: {dmgSkill.GetDamageMultiplier(level) * 100f:F1}%");
            SetTextSafe(specialText, "");
        }
        else if (skill is HealSkillData healSkill)
        {
            SetTextSafe(damageText,  "");
            string healType = healSkill.isAoe ? "[광역 힐]" : "[단일 힐]";
            string dotInfo  = healSkill.isDotHeal ? $"\n도트 간격: {healSkill.dotInterval}초" : "";
            SetTextSafe(specialText, $"{healType}\n힐량: {healSkill.GetHealMultiplier(level) * 100f:F1}%{dotInfo}");
        }
        else if (skill is BuffSkillData buffSkill)
        {
            SetTextSafe(damageText,  "");
            SetTextSafe(specialText, GetBuffDescription(buffSkill, level));
        }
        else if (skill is DebuffSkillData debuffSkill)
        {
            SetTextSafe(damageText,  "");
            SetTextSafe(specialText, GetDebuffDescription(debuffSkill, level));
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

    private void ShowNextStatByType(SkillData skill, int nextLevel)
    {
        if (skill is DamageSkillData dmgSkill)
        {
            SetTextSafe(nextDamageText,  $"데미지: {dmgSkill.GetDamageMultiplier(nextLevel) * 100f:F1}%");
            SetTextSafe(nextSpecialText, "");
        }
        else if (skill is HealSkillData healSkill)
        {
            SetTextSafe(nextDamageText,  "");
            SetTextSafe(nextSpecialText, $"힐량: {healSkill.GetHealMultiplier(nextLevel) * 100f:F1}%");
        }
        else if (skill is BuffSkillData buffSkill)
        {
            SetTextSafe(nextDamageText,  "");
            SetTextSafe(nextSpecialText, GetBuffDescription(buffSkill, nextLevel));
        }
        else if (skill is DebuffSkillData debuffSkill)
        {
            SetTextSafe(nextDamageText,  "");
            SetTextSafe(nextSpecialText, GetDebuffDescription(debuffSkill, nextLevel));
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
                case BuffSkillData.BuffEffectType.AtkBonus:    result += $"공격력 +{value}\n";                     break;
                case BuffSkillData.BuffEffectType.ApBonus:     result += $"주문력 +{value}\n";                     break;
                case BuffSkillData.BuffEffectType.DefBonus:    result += $"방어력 +{value}\n";                     break;
                case BuffSkillData.BuffEffectType.CritRate:    result += $"치명타 확률 +{value * 100f:F1}%\n";     break;
                case BuffSkillData.BuffEffectType.CritDamage:  result += $"치명타 배율 +{value * 100f:F1}%\n";     break;
                case BuffSkillData.BuffEffectType.MaxHpBonus:  result += $"최대 체력 +{value}\n";                  break;
                case BuffSkillData.BuffEffectType.SpeedBonus:  result += $"이동속도 +{value}\n";                   break;
                case BuffSkillData.BuffEffectType.Shield:      result += $"쉴드 +{value}\n";                       break;
                case BuffSkillData.BuffEffectType.ManaRegen:   result += $"마나 재생 +{value}/초\n";               break;
                case BuffSkillData.BuffEffectType.HpRegen:     result += $"체력 재생 +{value}/초\n";               break;
                case BuffSkillData.BuffEffectType.DebuffImmune: result += "디버프 면역\n";                         break;
            }
        }

        return result.TrimEnd('\n');
    }

    private string GetDebuffDescription(DebuffSkillData debuff, int level)
    {
        if (debuff.debuffEffects == null || debuff.debuffEffects.Count == 0) return "";

        string result = debuff.isAoe ? "[광역 디버프]\n" : "[단일 디버프]\n";

        foreach (var effect in debuff.debuffEffects)
        {
            float value    = effect.GetValue(level);
            float duration = effect.GetDuration(level);

            switch (effect.effectType)
            {
                case StatusEffectType.Stun:         result += $"스턴 {duration}초\n";                          break;
                case StatusEffectType.Slow:         result += $"슬로우 {value * 100f:F0}% {duration}초\n";     break;
                case StatusEffectType.AtkDown:      result += $"공격력 감소 {value * 100f:F0}% {duration}초\n"; break;
                case StatusEffectType.MoveSpeedDown: result += $"이속 감소 {value * 100f:F0}% {duration}초\n"; break;
                case StatusEffectType.DefDown:      result += $"방어력 감소 {value * 100f:F0}% {duration}초\n"; break;
            }
        }

        return result.TrimEnd('\n');
    }

    private string GetPassiveDescription(PassiveSkillData passive, int level)
    {
        switch (passive.effectType)
        {
            case PassiveSkillData.PassiveEffectType.AtkPercent:
                return $"물리 공격력 {passive.GetValue(level) * 100f:F1}% 증가";
            case PassiveSkillData.PassiveEffectType.ApPercent:
                return $"마법 공격력 {passive.GetValue(level) * 100f:F1}% 증가";
            case PassiveSkillData.PassiveEffectType.DefPercent:
                return $"방어력 {passive.GetValue(level) * 100f:F1}% 증가";
            case PassiveSkillData.PassiveEffectType.CritRate:
                return $"치명타 확률 +{passive.GetValue(level) * 100f:F1}%";
            case PassiveSkillData.PassiveEffectType.CritDamage:
                return $"치명타 배율 +{passive.GetValue(level) * 100f:F1}%";
            case PassiveSkillData.PassiveEffectType.MaxHpPercent:
                return $"최대 체력 {passive.GetValue(level) * 100f:F1}% 증가";
            case PassiveSkillData.PassiveEffectType.MagicResPercent:
                return $"마법 저항력 {passive.GetValue(level) * 100f:F1}% 증가";
            case PassiveSkillData.PassiveEffectType.HealPercent:
                return $"힐량 {passive.GetValue(level) * 100f:F1}% 증가";
            case PassiveSkillData.PassiveEffectType.FaithToHp:
                return $"신앙 스탯 비례 체력 증가 (계수: {passive.GetValue(level):F2})";
            case PassiveSkillData.PassiveEffectType.OnHitManaRestore:
                return $"평타 적중 시 마나 {passive.GetProcValue(level)} 회복";
            case PassiveSkillData.PassiveEffectType.OnHitAtkSpeedUp:
                return $"평타 적중 시 공격속도 {passive.GetProcValue(level) * 100f:F0}% 증가 ({passive.GetProcChance(level)}초)";
            case PassiveSkillData.PassiveEffectType.OnDebuffExtraDamage:
                return $"디버프 걸린 적에게 추가 데미지 {passive.GetValue(level) * 100f:F1}%";
            case PassiveSkillData.PassiveEffectType.OnCritLightning:
                return $"치명타 시 번개 발동 확률 {passive.GetProcChance(level) * 100f:F1}%";
            case PassiveSkillData.PassiveEffectType.OnKillHeal:
                return $"적 처치 시 체력 {passive.GetProcValue(level)} 회복";
            case PassiveSkillData.PassiveEffectType.HealCrit:
                return $"힐에 치명타 적용 (치명타 배율로 힐량 증가)";
            case PassiveSkillData.PassiveEffectType.OnHealAtkSpeedUp:
                return $"힐 받은 대상 공격속도 {passive.GetProcValue(level) * 100f:F0}% {passive.GetProcChance(level)}초 증가";
            case PassiveSkillData.PassiveEffectType.Revive:
                return "사망 시 1회 부활 (쿨타임 10분)";
            default:
                return "";
        }
    }

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
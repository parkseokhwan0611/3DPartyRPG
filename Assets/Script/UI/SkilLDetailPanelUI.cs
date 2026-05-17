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
    private CharacterStat currentCaster;

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
        currentCaster    = FindCasterStat(charIndex);

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

    // ─────────────────────────────────────────────────────────────────
    // 타입별 표시
    // ─────────────────────────────────────────────────────────────────

    private void ShowCurrentStatByType(SkillData skill, int level)
    {
        if (skill is DamageSkillData dmgSkill)
        {
            SetTextSafe(damageText,  $"데미지: {dmgSkill.GetDamageMultiplier(level) * 100f:F1}%");
            SetTextSafe(specialText, GetDamageSkillSpecial(dmgSkill, level));
        }
        else if (skill is HealSkillData healSkill)
        {
            SetTextSafe(damageText,  "");
            SetTextSafe(specialText, BuildHealDescription(healSkill, level, currentCaster));
        }
        else if (skill is BuffSkillData buffSkill)
        {
            SetTextSafe(damageText,  "");
            SetTextSafe(specialText, GetBuffDescription(buffSkill, level, currentCaster));
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
            SetTextSafe(nextSpecialText, GetDamageSkillSpecial(dmgSkill, nextLevel));
        }
        else if (skill is HealSkillData healSkill)
        {
            SetTextSafe(nextDamageText,  "");
            SetTextSafe(nextSpecialText, BuildHealDescription(healSkill, nextLevel, currentCaster));
        }
        else if (skill is BuffSkillData buffSkill)
        {
            SetTextSafe(nextDamageText,  "");
            SetTextSafe(nextSpecialText, GetBuffDescription(buffSkill, nextLevel, currentCaster));
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

    // ─────────────────────────────────────────────────────────────────
    // 힐 스킬 설명
    // ─────────────────────────────────────────────────────────────────

    private string BuildHealDescription(HealSkillData heal, int level, CharacterStat caster)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine(heal.targetType == HealSkillData.HealTargetType.Party ? "[파티 힐]" : "[단일 힐]");

        string baseLabel = heal.useApRatio ? "마법공격력" : "공격력";
        float  mult      = heal.GetHealMultiplier(level);

        var expr = new System.Text.StringBuilder();
        if (caster != null)
        {
            float baseStat = heal.useApRatio ? caster.TotalAp : caster.TotalAtk;
            expr.Append($"({baseLabel}({baseStat:F0})");
            if (heal.intRatio > 0f) expr.Append($" + 지능({caster.TotalInt:F0})×{heal.intRatio * 100f:F0}%");
            if (heal.fthRatio > 0f) expr.Append($" + 신앙({caster.TotalFth:F0})×{heal.fthRatio * 100f:F0}%");
            expr.Append($") × {mult * 100f:F1}%");

            float healBase = baseStat
                           + caster.TotalInt * heal.intRatio
                           + caster.TotalFth * heal.fthRatio;
            sb.AppendLine($"치유량: {expr} = {healBase * mult:F0}");
        }
        else
        {
            expr.Append($"({baseLabel}");
            if (heal.intRatio > 0f) expr.Append($" + 지능×{heal.intRatio * 100f:F0}%");
            if (heal.fthRatio > 0f) expr.Append($" + 신앙×{heal.fthRatio * 100f:F0}%");
            expr.Append($") × {mult * 100f:F1}%");
            sb.AppendLine($"치유량: {expr}");
        }

        if (heal.isDotHeal)
            sb.AppendLine($"지속시간: {heal.GetDotDuration(level)}초");

        return sb.ToString().TrimEnd('\n', '\r');
    }

    // ─────────────────────────────────────────────────────────────────
    // 데미지 스킬 부가 효과
    // ─────────────────────────────────────────────────────────────────

    private string GetDamageSkillSpecial(DamageSkillData dmg, int level)
    {
        var lines = new System.Text.StringBuilder();

        if (dmg.isAoe)
            lines.AppendLine($"[광역] 범위: {dmg.GetRange(level):F1}m");

        if (dmg.onHitDebuffs != null && dmg.onHitDebuffs.Count > 0)
        {
            lines.AppendLine("[적중 시 디버프]");
            foreach (var d in dmg.onHitDebuffs)
            {
                float val      = d.GetValue(level);
                float duration = d.GetDuration(level);
                switch (d.effectType)
                {
                    case StatusEffectType.Stun:          lines.AppendLine($"  스턴 {duration}초");                         break;
                    case StatusEffectType.Slow:          lines.AppendLine($"  슬로우 {val * 100f:F0}% {duration}초");      break;
                    case StatusEffectType.AtkDown:       lines.AppendLine($"  공격력 감소 {val * 100f:F0}% {duration}초"); break;
                    case StatusEffectType.MoveSpeedDown: lines.AppendLine($"  이속 감소 {val * 100f:F0}% {duration}초");   break;
                    case StatusEffectType.DefDown:       lines.AppendLine($"  방어력 감소 {val * 100f:F0}% {duration}초"); break;
                }
            }
        }

        if (dmg.hasNextSkillBuff)
            lines.AppendLine($"[연계] 다음 스킬 데미지 +{dmg.nextSkillDamageBonus * 100f:F0}% ({dmg.nextSkillBuffDuration}초)");

        if (dmg.hasAggroEffect)
            lines.AppendLine($"[어그로] 주변 {dmg.aggroRange:F0}m 적에게 {dmg.aggroAmount:F0} 어그로");

        return lines.ToString().TrimEnd('\n', '\r');
    }

    // ─────────────────────────────────────────────────────────────────
    // 버프 스킬 — 스탯 비례 포함
    // ─────────────────────────────────────────────────────────────────

    private string GetBuffDescription(BuffSkillData buff, int level, CharacterStat caster)
    {
        if (buff.buffEffects == null || buff.buffEffects.Count == 0) return "";

        string result = buff.isPartyBuff ? "[파티 버프]\n" : "[개인 버프]\n";
        result += $"지속시간: {buff.GetDuration(level)}초\n";

        foreach (var effect in buff.buffEffects)
        {
            float flat          = effect.GetValue(level);
            float scalingAmount = GetScalingAmount(effect, level, caster);
            float total         = flat + scalingAmount;
            result += FormatBuffLine(effect, level, total, flat, scalingAmount, caster) + "\n";
        }

        return result.TrimEnd('\n');
    }

    private float GetScalingAmount(BuffSkillData.BuffEffect effect, int level, CharacterStat caster)
    {
        if (caster == null || effect.scalingStat == BuffSkillData.ScalingStat.None) return 0f;
        float coeff = effect.GetScaling(level);
        if (coeff == 0f) return 0f;

        float stat = effect.scalingStat switch
        {
            BuffSkillData.ScalingStat.Str => caster.TotalStr,
            BuffSkillData.ScalingStat.Vit => caster.TotalVit,
            BuffSkillData.ScalingStat.Int => caster.TotalInt,
            BuffSkillData.ScalingStat.Fth => caster.TotalFth,
            _                             => 0f,
        };
        return stat * coeff;
    }

    private string GetScalingNote(BuffSkillData.BuffEffect effect, int level, float flat, float scalingAmount, CharacterStat caster)
    {
        if (effect.scalingStat == BuffSkillData.ScalingStat.None) return "";
        float coeff = effect.GetScaling(level);
        if (coeff == 0f) return "";

        string statName = effect.scalingStat switch
        {
            BuffSkillData.ScalingStat.Str => "STR",
            BuffSkillData.ScalingStat.Vit => "VIT",
            BuffSkillData.ScalingStat.Int => "INT",
            BuffSkillData.ScalingStat.Fth => "FTH",
            _                             => "",
        };

        // 시전자 스탯을 알면 실제 수치 계산 표기, 모르면 계수만 표기
        return caster != null
            ? $" (기본{flat:F0} + {statName}×{coeff * 100f:F0}% = +{scalingAmount:F0})"
            : $" + {statName}×{coeff * 100f:F0}%";
    }

    private string FormatBuffLine(BuffSkillData.BuffEffect effect, int level, float total, float flat, float scalingAmount, CharacterStat caster)
    {
        string note = GetScalingNote(effect, level, flat, scalingAmount, caster);

        return effect.effectType switch
        {
            BuffSkillData.BuffEffectType.AtkBonus      => $"공격력 +{total:F0}{note}",
            BuffSkillData.BuffEffectType.ApBonus       => $"주문력 +{total:F0}{note}",
            BuffSkillData.BuffEffectType.DefBonus      => $"방어력 +{total:F0}{note}",
            BuffSkillData.BuffEffectType.MagicResBonus => $"마법 저항력 +{total:F0}{note}",
            BuffSkillData.BuffEffectType.CritRate      => $"치명타 확률 +{total * 100f:F1}%{note}",
            BuffSkillData.BuffEffectType.CritDamage    => $"치명타 배율 +{total * 100f:F1}%{note}",
            BuffSkillData.BuffEffectType.MaxHpBonus    => $"최대 체력 +{total:F0}{note}",
            BuffSkillData.BuffEffectType.SpeedBonus    => $"이동속도 +{total:F1}{note}",
            BuffSkillData.BuffEffectType.Shield        => $"쉴드 +{total:F0}{note}",
            BuffSkillData.BuffEffectType.ManaRegen     => $"마나 재생 +{total:F1}/초{note}",
            BuffSkillData.BuffEffectType.HpRegen       => $"체력 재생 +{total:F1}/초{note}",
            BuffSkillData.BuffEffectType.HpOnHit       => $"공격 적중 시 체력 +{total:F0}{note}",
            BuffSkillData.BuffEffectType.DebuffImmune  => "디버프 면역",
            BuffSkillData.BuffEffectType.DispelDebuff  => "디버프 즉시 제거",
            _                                          => "",
        };
    }

    // ─────────────────────────────────────────────────────────────────
    // 디버프 스킬
    // ─────────────────────────────────────────────────────────────────

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
                case StatusEffectType.Stun:          result += $"스턴 {duration}초\n";                          break;
                case StatusEffectType.Slow:          result += $"슬로우 {value * 100f:F0}% {duration}초\n";     break;
                case StatusEffectType.AtkDown:       result += $"공격력 감소 {value * 100f:F0}% {duration}초\n"; break;
                case StatusEffectType.MoveSpeedDown: result += $"이속 감소 {value * 100f:F0}% {duration}초\n";  break;
                case StatusEffectType.DefDown:       result += $"방어력 감소 {value * 100f:F0}% {duration}초\n"; break;
            }
        }

        return result.TrimEnd('\n');
    }

    // ─────────────────────────────────────────────────────────────────
    // 패시브 스킬
    // ─────────────────────────────────────────────────────────────────

    private string GetPassiveDescription(PassiveSkillData passive, int level)
    {
        switch (passive.effectType)
        {
            case PassiveSkillData.PassiveEffectType.PhysDmgBonus:
                return $"물리 피해 {passive.GetValue(level) * 100f:F1}% 증가 (최종 데미지 적용)";
            case PassiveSkillData.PassiveEffectType.MagicDmgBonus:
                return $"마법 피해 {passive.GetValue(level) * 100f:F1}% 증가 (최종 데미지 적용)";
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
                return $"평타 적중 시 마나 {passive.GetValue(level)} 회복";
            case PassiveSkillData.PassiveEffectType.OnHitAtkSpeedUp:
                return $"평타 적중 시 공격속도 {passive.GetProcValue(level) * 100f:F0}% 증가 ({passive.GetProcChance(level)}초)";
            case PassiveSkillData.PassiveEffectType.OnDebuffExtraDamage:
                return $"디버프 걸린 적에게 추가 데미지 {passive.GetValue(level) * 100f:F1}%";
            case PassiveSkillData.PassiveEffectType.OnCritLightning:
                return $"치명타 시 번개 발동 확률 {passive.GetProcChance(level) * 100f:F1}%";
            case PassiveSkillData.PassiveEffectType.OnKillHeal:
                return $"적 처치 시 체력 {passive.GetProcValue(level)} 회복";
            case PassiveSkillData.PassiveEffectType.HealCrit:
                return "힐에 치명타 적용 (치명타 배율로 힐량 증가)";
            case PassiveSkillData.PassiveEffectType.OnHealAtkSpeedUp:
                return $"힐 받은 대상 공격속도 {passive.GetProcValue(level) * 100f:F0}% {passive.GetProcChance(level)}초 증가";
            case PassiveSkillData.PassiveEffectType.Revive:
                return "사망 시 1회 부활 (쿨타임 10분)";
            default:
                return "";
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────────────────────────

    private CharacterStat FindCasterStat(int partyIndex)
    {
        if (PartyManager.instance == null) return null;
        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member == null) continue;
            CharacterStat stat = member.GetComponent<CharacterStat>();
            if (stat != null && stat.partyIndex == partyIndex)
                return stat;
        }
        return null;
    }

    private void OnLearnButtonClicked()
    {
        if (currentSkill == null || DataManager.instance == null) return;

        CharacterStatus status = DataManager.instance.partyStatuses[currentCharIndex];
        bool success           = status.TryLevelUpSkill(currentSkill);

        if (!success) return;

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

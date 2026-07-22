using System.Text;

// 스킬 타입별 상세 설명 텍스트 빌더 — SkillDetailPanelUI(메뉴 스킬창)와
// CombatDetailPopupUI(전투 퀵슬롯 호버 팝업)가 공용으로 사용
public static class SkillDescriptionBuilder
{
    // ─────────────────────────────────────────────────────────────────
    // 데미지 스킬
    // ─────────────────────────────────────────────────────────────────

    public static string BuildDamageDescription(DamageSkillData dmg, int level, CharacterStat caster)
    {
        var sb = new StringBuilder();

        sb.AppendLine(dmg.isAoe ? "[광역]" : "[단일]");

        string baseLabel = dmg.useAp ? "마법공격력" : "공격력";
        float  mult      = dmg.GetDamageMultiplier(level);

        var expr = new StringBuilder();
        if (caster != null)
        {
            float baseStat  = dmg.useAp ? caster.TotalAp : caster.TotalAtk;
            float statBonus = 0f;
            expr.Append($"({baseLabel}({baseStat:F0})");
            foreach (var s in dmg.statScalings)
            {
                if (s.stat == DamageSkillData.ScalingStat.None) continue;
                float statVal   = GetStatValue(caster, s.stat);
                float coeff     = s.GetScaling(level);
                statBonus      += statVal * coeff;
                expr.Append($" + {StatName(s.stat)}({statVal:F0})×{coeff * 100f:F0}%");
            }
            expr.Append($") × {mult * 100f:F1}%");
            sb.Append($"데미지: {expr} = {(baseStat + statBonus) * mult:F0}");
        }
        else
        {
            expr.Append($"({baseLabel}");
            foreach (var s in dmg.statScalings)
            {
                if (s.stat == DamageSkillData.ScalingStat.None) continue;
                expr.Append($" + {StatName(s.stat)}×{s.GetScaling(level) * 100f:F0}%");
            }
            expr.Append($") × {mult * 100f:F1}%");
            sb.Append($"데미지: {expr}");
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string GetDamageSkillSpecial(DamageSkillData dmg, int level)
    {
        var lines = new StringBuilder();

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
    // 힐 스킬
    // ─────────────────────────────────────────────────────────────────

    public static string BuildHealDescription(HealSkillData heal, int level, CharacterStat caster)
    {
        var sb = new StringBuilder();

        sb.AppendLine(heal.targetType == HealSkillData.HealTargetType.Party ? "[파티 힐]" : "[단일 힐]");

        string baseLabel = heal.useApRatio ? "마법공격력" : "공격력";
        float  mult      = heal.GetHealMultiplier(level);

        var expr = new StringBuilder();
        if (caster != null)
        {
            float baseStat  = heal.useApRatio ? caster.TotalAp : caster.TotalAtk;
            float statBonus = 0f;
            expr.Append($"({baseLabel}({baseStat:F0})");
            foreach (var s in heal.statScalings)
            {
                if (s.stat == DamageSkillData.ScalingStat.None) continue;
                float statVal   = GetStatValue(caster, s.stat);
                float coeff     = s.GetScaling(level);
                statBonus      += statVal * coeff;
                expr.Append($" + {StatName(s.stat)}({statVal:F0})×{coeff * 100f:F0}%");
            }
            expr.Append($") × {mult * 100f:F1}%");
            sb.AppendLine($"치유량: {expr} = {(baseStat + statBonus) * mult:F0}");
        }
        else
        {
            expr.Append($"({baseLabel}");
            foreach (var s in heal.statScalings)
            {
                if (s.stat == DamageSkillData.ScalingStat.None) continue;
                expr.Append($" + {StatName(s.stat)}×{s.GetScaling(level) * 100f:F0}%");
            }
            expr.Append($") × {mult * 100f:F1}%");
            sb.AppendLine($"치유량: {expr}");
        }

        if (heal.isDotHeal)
            sb.AppendLine($"지속시간: {heal.GetDotDuration(level)}초");

        return sb.ToString().TrimEnd('\n', '\r');
    }

    // ─────────────────────────────────────────────────────────────────
    // 버프 스킬
    // ─────────────────────────────────────────────────────────────────

    public static string GetBuffDescription(BuffSkillData buff, int level, CharacterStat caster)
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

    private static float GetScalingAmount(BuffSkillData.BuffEffect effect, int level, CharacterStat caster)
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

    private static string GetScalingNote(BuffSkillData.BuffEffect effect, int level, float flat, float scalingAmount, CharacterStat caster)
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

    private static string FormatBuffLine(BuffSkillData.BuffEffect effect, int level, float total, float flat, float scalingAmount, CharacterStat caster)
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

    public static string GetDebuffDescription(DebuffSkillData debuff, int level)
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

    public static string GetPassiveDescription(PassiveSkillData passive, int level)
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
            case PassiveSkillData.PassiveEffectType.MaxMpBonus:
                return $"최대 마나 +{passive.GetValue(level):F0}";
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
    // 공용 스탯 스케일링 헬퍼
    // ─────────────────────────────────────────────────────────────────

    private static float GetStatValue(CharacterStat caster, DamageSkillData.ScalingStat stat)
    {
        return stat switch
        {
            DamageSkillData.ScalingStat.Str => caster.TotalStr,
            DamageSkillData.ScalingStat.Vit => caster.TotalVit,
            DamageSkillData.ScalingStat.Int => caster.TotalInt,
            DamageSkillData.ScalingStat.Fth => caster.TotalFth,
            _                               => 0f,
        };
    }

    private static string StatName(DamageSkillData.ScalingStat stat)
    {
        return stat switch
        {
            DamageSkillData.ScalingStat.Str => "힘",
            DamageSkillData.ScalingStat.Vit => "체력",
            DamageSkillData.ScalingStat.Int => "지능",
            DamageSkillData.ScalingStat.Fth => "신앙",
            _                               => "",
        };
    }

    // 스킬 타입에 맞는 설명(데미지/치유/버프/디버프/패시브)을 한 번에 조립
    public static string BuildFullDescription(SkillData skill, int level, CharacterStat caster)
    {
        if (skill is DamageSkillData dmg)
        {
            string special = GetDamageSkillSpecial(dmg, level);
            return string.IsNullOrEmpty(special)
                ? BuildDamageDescription(dmg, level, caster)
                : BuildDamageDescription(dmg, level, caster) + "\n" + special;
        }
        if (skill is HealSkillData heal)   return BuildHealDescription(heal, level, caster);
        if (skill is BuffSkillData buff)   return GetBuffDescription(buff, level, caster);
        if (skill is DebuffSkillData deb)  return GetDebuffDescription(deb, level);
        if (skill is PassiveSkillData pas) return GetPassiveDescription(pas, level);
        return "";
    }
}

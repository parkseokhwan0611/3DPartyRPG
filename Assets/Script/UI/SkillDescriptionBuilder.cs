using System.Text;
using UnityEngine;

// 스킬 타입별 상세 설명 텍스트 빌더 — SkillDetailPanelUI(메뉴 스킬창)와
// CombatDetailPopupUI(전투 퀵슬롯 호버 팝업)가 공용으로 사용
public static class SkillDescriptionBuilder
{
    // ─────────────────────────────────────────────────────────────────
    // 마나 소모량 / 쿨타임 — 장비 감소가 반영된 실제 값. showBase면 감소가 있을 때 "(기본 N)"을 덧붙인다
    // (글자 칸이 넓은 스킬창은 기본값 포함, 좁은 전투 팝업 쿨타임 칸은 실제 값만)
    // ─────────────────────────────────────────────────────────────────

    public static string FormatMpCost(float baseCost, CharacterStat caster, bool showBase)
        => WithBase(SkillBase.ReducedMpCost(baseCost, caster), baseCost, "0.#", "", showBase);

    public static string FormatCooldown(float baseCooldown, CharacterStat caster, bool showBase, string format = "0.#")
        => WithBase(SkillBase.ReducedCooldown(baseCooldown, caster), baseCooldown, format, "초", showBase);

    private static string WithBase(float actual, float baseValue, string format, string unit, bool showBase)
    {
        string shown = actual.ToString(format) + unit;
        if (!showBase || actual.ToString(format) == baseValue.ToString(format)) return shown;
        return $"{shown} (기본 {baseValue.ToString(format)}{unit})";
    }

    // ─────────────────────────────────────────────────────────────────
    // 데미지 스킬
    // ─────────────────────────────────────────────────────────────────

    public static string BuildDamageDescription(DamageSkillData dmg, int level, CharacterStat caster)
    {
        var sb = new StringBuilder();

        sb.AppendLine(dmg.IsAreaDamage ? "[광역]" : "[단일]");

        string baseLabel = dmg.useAp ? "마법 공격력" : "물리 공격력";
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
            float dmgBonus = dmg.useAp ? caster.MagicDmgBonus : caster.PhysDmgBonus;
            if (dmgBonus != 0f)
                expr.Append($" × (1 + {(dmg.useAp ? "마법" : "물리")} 데미지 증가 {dmgBonus * 100f:0.#}%)");
            sb.Append($"데미지: {expr} = {dmg.GetRawDamage(level, caster):F0}");
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

    // caster를 넘기면 시전 시 버프의 스탯 비례분을 실제 수치로, 없으면 계수만 표기
    public static string GetDamageSkillSpecial(DamageSkillData dmg, int level, CharacterStat caster = null)
    {
        var lines = new StringBuilder();

        if (dmg.throwGrenade)
            lines.AppendLine($"[투척] 폭발 범위: {dmg.GetRange(level):F1}m");
        else if (dmg.fireProjectile && dmg.projectileCount > 1)
            lines.AppendLine($"[투사체] {dmg.projectileCount}발 발사 (한 발마다 위 데미지)");
        else if (dmg.IsZone)
        {
            // 장판 — SkillZone과 같은 규칙: 단발이면 1회 판정, 아니면 간격마다 지속시간 동안 반복 판정
            // 설명 칸 높이가 고정이라 한 줄에 들어가게 짧게. 첫 판정까지의 대기(zoneActivationDelay)는 연출 타이밍이라 표기하지 않음
            lines.AppendLine(dmg.zoneHitOnce || dmg.zoneDamageInterval <= 0f
                ? $"[장판] 범위 {dmg.GetRange(level):F1}m, 1회 판정"
                : $"[장판] 범위 {dmg.GetRange(level):F1}m, {dmg.zoneDamageInterval:0.#}초마다 {dmg.zoneDuration:0.#}초간");
        }
        else if (dmg.isAoe)
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
                    // 용어는 머리 위 상태 표시(기절·둔화)와 통일
                    case StatusEffectType.Stun:          lines.AppendLine($"기절 {duration}초");                         break;
                    case StatusEffectType.Slow:          lines.AppendLine($"둔화 {val * 100f:F0}% {duration}초");        break;
                    case StatusEffectType.AtkDown:       lines.AppendLine($"공격력 감소 {FormatDebuffValue(d, val)} {duration}초"); break;
                    case StatusEffectType.MoveSpeedDown: lines.AppendLine($"이동 속도 감소 {val * 100f:F0}% {duration}초"); break;
                    case StatusEffectType.DefDown:       lines.AppendLine($"방어력 감소 {FormatDebuffValue(d, val)} {duration}초"); break;
                    case StatusEffectType.Poison:        lines.AppendLine($"독 초당 {val:F0} {duration}초");             break;
                }
            }
        }

        if (dmg.onCastBuffs != null && dmg.onCastBuffs.Count > 0)
        {
            // 버프 스킬과 같은 형식 — [개인 버프] / 지속시간 / 효과 줄.
            // 부가 버프끼리 지속시간이 다르면 공통 지속시간 줄 대신 줄마다 (n초)로 표기
            float firstDur = dmg.onCastBuffs[0].GetDuration(level);
            bool  sameDur  = dmg.onCastBuffs.TrueForAll(b => Mathf.Approximately(b.GetDuration(level), firstDur));

            lines.AppendLine("[개인 버프]");
            if (sameDur) lines.AppendLine($"지속시간: {firstDur}초");
            foreach (var b in dmg.onCastBuffs)
            {
                string line = FormatCastBuffLine(b, level, caster, withDuration: !sameDur);
                if (!string.IsNullOrEmpty(line)) lines.AppendLine(line);
            }
        }

        if (dmg.hasNextSkillBuff)
            lines.AppendLine($"[연계] 다음 스킬 데미지 +{dmg.nextSkillDamageBonus * 100f:F0}% ({dmg.nextSkillBuffDuration}초)");

        if (dmg.hasAggroEffect)
            lines.AppendLine($"[어그로] 주변 {dmg.aggroRange:F0}m 적에게 {dmg.aggroAmount:F0} 어그로");

        return lines.ToString().TrimEnd('\n', '\r');
    }

    // 데미지 스킬의 시전 시 자기 버프 한 줄 (보호막, 공격 속도 등)
    private static string FormatCastBuffLine(DamageSkillData.CastBuffEffect b, int level, CharacterStat caster, bool withDuration)
    {
        float flat    = b.GetValue(level);
        float scaling = b.GetScalingAmount(level, caster);
        float val     = flat + scaling;
        // 공통 "지속시간:" 줄이 있으면 줄 끝의 (n초)는 생략
        string dur    = withDuration ? $" ({b.GetDuration(level)}초)" : "";
        // Percent 모드는 능력치 증가 5종 + 받는 데미지 감소에서만 실제로 적용됨 — 보호막 등은 모드와 무관하게 고정 수치
        bool  pct     = b.IsPercent;
        string amount = pct ? $"{val * 100f:0.#}%" : $"{val:F0}";
        // 공격 속도·치명타처럼 원래 비율(0.2 = 20%)인 효과는 스탯 비례분도 % 단위로 표기
        bool ratio = pct || b.effectType is StatusEffectType.AtkSpeedUp or StatusEffectType.CritRateUp
                                         or StatusEffectType.CritDamageUp or StatusEffectType.MoveSpeedUp;
        string note   = GetCastBuffScalingNote(b, level, flat, scaling, ratio, caster);

        return b.effectType switch
        {
            StatusEffectType.Shield         => $"보호막 +{val:F0}{note}{dur}",
            StatusEffectType.AtkUp          => $"물리 공격력 +{amount}{note}{dur}",
            StatusEffectType.ApUp           => $"마법 공격력 +{amount}{note}{dur}",
            StatusEffectType.DefUp          => $"방어력 +{amount}{note}{dur}",
            StatusEffectType.MagicResUp     => $"마법 저항력 +{amount}{note}{dur}",
            StatusEffectType.MaxHpUp        => $"최대 체력 +{amount}{note}{dur}",
            StatusEffectType.AtkSpeedUp     => $"공격 속도 +{val * 100f:0.#}%{note}{dur}",
            StatusEffectType.CritRateUp     => $"치명타 확률 +{val * 100f:0.#}%{note}{dur}",
            StatusEffectType.CritDamageUp   => $"치명타 데미지 +{val * 100f:0.#}%{note}{dur}",
            StatusEffectType.DmgReductionUp => $"받는 데미지 {amount} 감소{note}{dur}",
            StatusEffectType.MoveSpeedUp    => $"이동 속도 +{val * 100f:0.#}%{note}{dur}",
            StatusEffectType.Invulnerable   => $"무적{dur}",
            StatusEffectType.DebuffImmune   => $"디버프 면역{dur}",
            _                               => "",
        };
    }

    // 시전 시 버프의 스탯 비례 표기 — 버프 스킬(GetScalingNote)과 같은 형식
    // 예: "보호막 +174 (기본150 + VIT×200% = +24)"
    private static string GetCastBuffScalingNote(DamageSkillData.CastBuffEffect b, int level, float flat, float scaling,
                                                 bool pct, CharacterStat caster)
    {
        if (b.scalingStat == DamageSkillData.ScalingStat.None) return "";
        float coeff = b.GetScaling(level);
        if (coeff == 0f) return "";

        string statName = b.scalingStat switch
        {
            DamageSkillData.ScalingStat.Str => "STR",
            DamageSkillData.ScalingStat.Vit => "VIT",
            DamageSkillData.ScalingStat.Int => "INT",
            DamageSkillData.ScalingStat.Fth => "FTH",
            DamageSkillData.ScalingStat.Dex => "DEX",
            _                               => "",
        };

        if (pct)
        {
            return caster != null
                ? $" (기본 {flat * 100f:0.#}% + {statName} 1당 {coeff * 100f:0.##}% = +{scaling * 100f:0.#}%)"
                : $" + {statName} 1당 {coeff * 100f:0.##}%";
        }
        return caster != null
            ? $" (기본{flat:F0} + {statName}×{coeff * 100f:F0}% = +{scaling:F0})"
            : $" + {statName}×{coeff * 100f:F0}%";
    }

    // 전투 퀵슬롯 호버 팝업 전용 — 계산식 없이 최종 데미지 수치만, [단일]/[광역] 태그도 생략
    public static string BuildDamageFinal(DamageSkillData dmg, int level, CharacterStat caster)
    {
        if (caster == null) return "";
        return $"데미지: {dmg.GetRawDamage(level, caster):F0}";
    }

    // ─────────────────────────────────────────────────────────────────
    // 힐 스킬
    // ─────────────────────────────────────────────────────────────────

    public static string BuildHealDescription(HealSkillData heal, int level, CharacterStat caster)
    {
        var sb = new StringBuilder();

        sb.AppendLine(heal.targetType == HealSkillData.HealTargetType.Party ? "[파티 힐]" : "[단일 힐]");

        string baseLabel = heal.useApRatio ? "마법 공격력" : "물리 공격력";
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
            // 실제 힐(HealSkill.CalculateHeal)과 같이 힐량 증가 패시브까지 반영
            float healBonus = caster.HealBonus;
            if (healBonus != 0f)
                expr.Append($" × (1 + 힐량 증가 {healBonus * 100f:0.#}%)");
            sb.AppendLine($"치유량: {expr} = {(baseStat + statBonus) * mult * (1f + healBonus):F0}");
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

    // 전투 퀵슬롯 호버 팝업 전용 — 계산식 없이 최종 치유량 수치만
    public static string BuildHealFinal(HealSkillData heal, int level, CharacterStat caster)
    {
        if (caster == null) return "";

        float baseStat  = heal.useApRatio ? caster.TotalAp : caster.TotalAtk;
        float mult      = heal.GetHealMultiplier(level);
        float statBonus = 0f;
        foreach (var s in heal.statScalings)
        {
            if (s.stat == DamageSkillData.ScalingStat.None) continue;
            statBonus += GetStatValue(caster, s.stat) * s.GetScaling(level);
        }

        string result = $"치유량: {(baseStat + statBonus) * mult * (1f + caster.HealBonus):F0}";
        if (heal.isDotHeal)
            result += $"\n지속시간: {heal.GetDotDuration(level)}초";
        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    // 버프 스킬
    // ─────────────────────────────────────────────────────────────────

    public static string GetBuffDescription(BuffSkillData buff, int level, CharacterStat caster)
    {
        if (buff.buffEffects == null || buff.buffEffects.Count == 0) return "";

        string result = buff.isPartyBuff ? "[파티 버프]\n" : "[개인 버프]\n";
        if (buff.HasDurationEffect) result += $"지속시간: {buff.GetDuration(level)}초\n";

        foreach (var effect in buff.buffEffects)
        {
            if (effect.effectType == BuffSkillData.BuffEffectType.Thorns)
            {
                result += FormatThornsLine(buff, effect, level, caster, detailed: true) + "\n";
                continue;
            }

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
            BuffSkillData.ScalingStat.Dex => caster.TotalDex,
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
            BuffSkillData.ScalingStat.Dex => "DEX",
            _                             => "",
        };

        // 퍼센트 버프는 스탯 1당 몇 %가 붙는지로 표기 (계수 0.001 = 스탯 1당 0.1%)
        if (effect.IsPercent)
        {
            return caster != null
                ? $" (기본 {flat * 100f:0.#}% + {statName} 1당 {coeff * 100f:0.##}% = +{scalingAmount * 100f:0.#}%)"
                : $" + {statName} 1당 {coeff * 100f:0.##}%";
        }

        // 시전자 스탯을 알면 실제 수치 계산 표기, 모르면 계수만 표기
        return caster != null
            ? $" (기본{flat:F0} + {statName}×{coeff * 100f:F0}% = +{scalingAmount:F0})"
            : $" + {statName}×{coeff * 100f:F0}%";
    }

    // 가시 반사 한 줄
    // detailed(스킬 창): "피격 시 공격자에게 마법 데미지 230 반사 (기본 50 + 방어력(120)×150%) · 치명타 적용"
    // 간략(전투 팝업):   "피격 시 공격자에게 마법 데미지 230 반사"
    private static string FormatThornsLine(BuffSkillData buff, BuffSkillData.BuffEffect effect, int level,
                                           CharacterStat caster, bool detailed)
    {
        string dmgType = buff.thornsIsMagic ? "마법 데미지" : "물리 데미지";
        float  baseDmg = effect.GetValue(level);

        if (!detailed)
        {
            return caster != null
                ? $"피격 시 공격자에게 {dmgType} {buff.GetThornsDamage(effect, level, caster):F0} 반사"
                : $"피격 시 공격자에게 {dmgType} 반사";
        }

        var expr = new StringBuilder($"기본 {baseDmg:F0}");
        foreach (var s in buff.thornsScalings)
        {
            if (s == null || s.GetCoeff(level) == 0f) continue;
            string name = ThornsStatName(s.stat);
            expr.Append(caster != null
                ? $" + {name}({BuffSkillData.GetThornsStatValue(s.stat, caster):F0})×{s.GetCoeff(level) * 100f:0.#}%"
                : $" + {name}×{s.GetCoeff(level) * 100f:0.#}%");
        }

        return caster != null
            ? $"피격 시 공격자에게 {dmgType} {buff.GetThornsDamage(effect, level, caster):F0} 반사 ({expr}) · 치명타 적용"
            : $"피격 시 공격자에게 {dmgType} 반사 ({expr}) · 치명타 적용";
    }

    private static string ThornsStatName(BuffSkillData.ThornsStat stat) => stat switch
    {
        BuffSkillData.ThornsStat.Def      => "방어력",
        BuffSkillData.ThornsStat.MagicRes => "마법 저항력",
        BuffSkillData.ThornsStat.MaxHp    => "최대 체력",
        BuffSkillData.ThornsStat.Atk      => "물리 공격력",
        BuffSkillData.ThornsStat.Ap       => "마법 공격력",
        BuffSkillData.ThornsStat.Str      => "힘",
        BuffSkillData.ThornsStat.Vit      => "체력",
        BuffSkillData.ThornsStat.Int      => "지능",
        BuffSkillData.ThornsStat.Fth      => "신앙",
        BuffSkillData.ThornsStat.Dex      => "민첩",
        _                                 => "",
    };

    // 능력치 증가 수치 표기 — 퍼센트 버프면 "12.5%", 고정이면 "20"
    private static string FormatStatAmount(BuffSkillData.BuffEffect effect, float total)
        => effect.IsPercent ? $"{total * 100f:0.#}%" : $"{total:F0}";

    private static string FormatBuffLine(BuffSkillData.BuffEffect effect, int level, float total, float flat, float scalingAmount, CharacterStat caster)
    {
        string note   = GetScalingNote(effect, level, flat, scalingAmount, caster);
        string amount = FormatStatAmount(effect, total);

        return effect.effectType switch
        {
            BuffSkillData.BuffEffectType.AtkBonus      => $"물리 공격력 +{amount}{note}",
            BuffSkillData.BuffEffectType.ApBonus       => $"마법 공격력 +{amount}{note}",
            BuffSkillData.BuffEffectType.DefBonus      => $"방어력 +{amount}{note}",
            BuffSkillData.BuffEffectType.MagicResBonus => $"마법 저항력 +{amount}{note}",
            BuffSkillData.BuffEffectType.CritRate      => $"치명타 확률 +{total * 100f:F1}%{note}",
            BuffSkillData.BuffEffectType.CritDamage    => $"치명타 데미지 +{total * 100f:F1}%{note}",
            BuffSkillData.BuffEffectType.MaxHpBonus    => $"최대 체력 +{amount}{note}",
            BuffSkillData.BuffEffectType.Shield        => $"보호막 +{total:F0}{note}",
            BuffSkillData.BuffEffectType.HpOnHit       => $"공격 적중 시 체력 +{total:F0}{note}",
            _                                          => FormatBuffLineFinal(effect, total) + note,
        };
    }

    // 전투 퀵슬롯 호버 팝업 전용 — 스탯 비례 계산식(괄호 안 내역) 없이 최종 수치만
    public static string GetBuffDescriptionFinal(BuffSkillData buff, int level, CharacterStat caster)
    {
        if (buff.buffEffects == null || buff.buffEffects.Count == 0) return "";

        string result = buff.isPartyBuff ? "[파티 버프]\n" : "[개인 버프]\n";
        if (buff.HasDurationEffect) result += $"지속시간: {buff.GetDuration(level)}초\n";

        foreach (var effect in buff.buffEffects)
        {
            if (effect.effectType == BuffSkillData.BuffEffectType.Thorns)
            {
                result += FormatThornsLine(buff, effect, level, caster, detailed: false) + "\n";
                continue;
            }

            float total = effect.GetValue(level) + GetScalingAmount(effect, level, caster);
            result += FormatBuffLineFinal(effect, total) + "\n";
        }

        return result.TrimEnd('\n');
    }

    private static string FormatBuffLineFinal(BuffSkillData.BuffEffect effect, float total)
    {
        string amount = FormatStatAmount(effect, total);

        return effect.effectType switch
        {
            BuffSkillData.BuffEffectType.AtkBonus      => $"물리 공격력 +{amount}",
            BuffSkillData.BuffEffectType.ApBonus       => $"마법 공격력 +{amount}",
            BuffSkillData.BuffEffectType.DefBonus      => $"방어력 +{amount}",
            BuffSkillData.BuffEffectType.MagicResBonus => $"마법 저항력 +{amount}",
            BuffSkillData.BuffEffectType.CritRate      => $"치명타 확률 +{total * 100f:F1}%",
            BuffSkillData.BuffEffectType.CritDamage    => $"치명타 데미지 +{total * 100f:F1}%",
            BuffSkillData.BuffEffectType.MaxHpBonus    => $"최대 체력 +{amount}",
            BuffSkillData.BuffEffectType.SpeedBonus    => $"이동 속도 +{total * 100f:0.#}%",
            BuffSkillData.BuffEffectType.Shield        => $"보호막 +{total:F0}",
            BuffSkillData.BuffEffectType.ManaRegen     => $"마나 재생 +{total:0.#}/초",
            BuffSkillData.BuffEffectType.HpRegen       => $"체력 재생 +{total:0.#}/초",
            BuffSkillData.BuffEffectType.HpOnHit       => $"공격 적중 시 체력 +{total:F0}",
            BuffSkillData.BuffEffectType.DebuffImmune  => "디버프 면역",
            BuffSkillData.BuffEffectType.DispelDebuff  => "디버프 즉시 제거",
            BuffSkillData.BuffEffectType.AtkSpeedBonus => $"공격 속도 +{total * 100f:0.#}%",
            BuffSkillData.BuffEffectType.DmgReduction  => $"받는 데미지 {amount} 감소",
            BuffSkillData.BuffEffectType.Invulnerable  => "무적 (데미지·디버프 무시)",
            BuffSkillData.BuffEffectType.CooldownReset => Mathf.RoundToInt(total) > 0
                ? $"스킬 {Mathf.RoundToInt(total)}개 쿨타임 즉시 초기화 (남은 쿨타임이 긴 순, 쿨 초기화 스킬 제외)"
                : "모든 스킬 쿨타임 즉시 초기화 (쿨 초기화 스킬 제외)",
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
                case StatusEffectType.Stun:          result += $"기절 {duration}초\n";                          break;
                case StatusEffectType.Slow:          result += $"둔화 {value * 100f:F0}% {duration}초\n";       break;
                case StatusEffectType.AtkDown:       result += $"공격력 감소 {FormatDebuffValue(effect, value)} {duration}초\n"; break;
                case StatusEffectType.MoveSpeedDown: result += $"이동 속도 감소 {value * 100f:F0}% {duration}초\n"; break;
                case StatusEffectType.DefDown:       result += $"방어력 감소 {FormatDebuffValue(effect, value)} {duration}초\n"; break;
                case StatusEffectType.Poison:        result += $"독 초당 {value:F0} {duration}초\n";              break;
            }
        }

        return result.TrimEnd('\n');
    }

    // 감소 수치 표기 — Flat이면 "20", Percent면 "20%"
    private static string FormatDebuffValue(DebuffSkillData.DebuffEffect d, float value)
        => d.IsFlat ? $"{value:F0}" : $"{value * 100f:F0}%";

    // ─────────────────────────────────────────────────────────────────
    // 패시브 스킬
    // ─────────────────────────────────────────────────────────────────

    public static string GetPassiveDescription(PassiveSkillData passive, int level)
    {
        string result = GetPassiveEffectLine(passive, passive.effectType, passive.valueMode, passive.GetValue(level), level);

        // 두 번째 효과 (예: 방어력 + 마법 저항력)는 다음 줄에 이어서 표기
        if (passive.HasValidSecondEffect)
        {
            string second = GetPassiveEffectLine(passive, passive.secondEffectType, passive.secondValueMode,
                                                 passive.GetSecondValue(level), level);
            if (!string.IsNullOrEmpty(second))
                result = string.IsNullOrEmpty(result) ? second : $"{result}\n{second}";
        }

        return result;
    }

    // 효과 한 줄 — value는 해당 효과의 레벨별 수치 (첫 번째/두 번째 효과 공용)
    private static string GetPassiveEffectLine(PassiveSkillData passive, PassiveSkillData.PassiveEffectType type,
                                               ModifierMode mode, float value, int level)
    {
        // 공격력/마법 공격력/방어력/마법 저항력/최대 체력 — 고정이면 "+20", 퍼센트면 "10% 증가"
        if (PassiveSkillData.TryGetModifierStat(type, out ModifierStat modifierStat))
        {
            string label = modifierStat switch
            {
                ModifierStat.Atk      => "물리 공격력",
                ModifierStat.Ap       => "마법 공격력",
                ModifierStat.Def      => "방어력",
                ModifierStat.MagicRes => "마법 저항력",
                ModifierStat.MaxHp    => "최대 체력",
                _                     => "",
            };
            return mode == ModifierMode.Percent
                ? $"{label} {value * 100f:F1}% 증가 (스탯·장비 기준)"
                : $"{label} +{value:F0}";
        }

        switch (type)
        {
            case PassiveSkillData.PassiveEffectType.PhysDmgBonus:
                return $"물리 데미지 {value * 100f:F1}% 증가 (최종 데미지 적용)";
            case PassiveSkillData.PassiveEffectType.MagicDmgBonus:
                return $"마법 데미지 {value * 100f:F1}% 증가 (최종 데미지 적용)";
            case PassiveSkillData.PassiveEffectType.CritRate:
                return $"치명타 확률 +{value * 100f:F1}%";
            case PassiveSkillData.PassiveEffectType.CritDamage:
                return $"치명타 데미지 +{value * 100f:F1}%";
            case PassiveSkillData.PassiveEffectType.HealPercent:
                return $"힐량 {value * 100f:F1}% 증가";
            case PassiveSkillData.PassiveEffectType.FaithToHp:
                return $"신앙 1당 최대 체력 +{value:0.##}";
            case PassiveSkillData.PassiveEffectType.AtkSpeed:
                return $"공격 속도 {value * 100f:0.#}% 증가";
            case PassiveSkillData.PassiveEffectType.MaxMpBonus:
                return $"최대 마나 +{value:F0}";
            case PassiveSkillData.PassiveEffectType.OnHitManaRestore:
                return $"평타 적중 시 마나 {value} 회복";
            case PassiveSkillData.PassiveEffectType.SummonHitManaRestore:
                return $"소환수 공격 적중 시 마나 {value:0.#} 회복";
            case PassiveSkillData.PassiveEffectType.PhysDmgReduction:
                return mode == ModifierMode.Percent
                    ? $"받는 물리 데미지 {value * 100f:F1}% 감소"
                    : $"받는 물리 데미지 {value:F0} 감소";
            case PassiveSkillData.PassiveEffectType.MagicDmgReduction:
                return mode == ModifierMode.Percent
                    ? $"받는 마법 데미지 {value * 100f:F1}% 감소"
                    : $"받는 마법 데미지 {value:F0} 감소";
            case PassiveSkillData.PassiveEffectType.OnHitAtkSpeedUp:
                return $"평타 적중 시 {ChanceText(passive, level)}공격 속도 {passive.GetProcValue(level) * 100f:0.#}% 증가 ({passive.GetProcDuration(level):0.#}초)";
            case PassiveSkillData.PassiveEffectType.OnDebuffExtraDamage:
                return $"디버프 걸린 적에게 주는 데미지 {value * 100f:0.#}% 증가";
            case PassiveSkillData.PassiveEffectType.OnCritLightning:
                return passive.procSkill != null
                    ? $"평타 치명타 시 {ChanceText(passive, level)}{passive.procSkill.skillName} 발동"
                    : $"평타 치명타 시 {ChanceText(passive, level)}번개 (마법 공격력 × {passive.GetProcValue(level) * 100f:0.#}%)";
            case PassiveSkillData.PassiveEffectType.OnHitPoison:
                // 실제 독 데미지 기준 — 근접 평타는 물리 공격력, 원거리 평타는 마법 공격력 (CharacterStat.ApplyPoison)
                return $"평타 적중 시 {ChanceText(passive, level)}독 — 초당 공격력(근접: 물리 / 원거리: 마법)의 {passive.GetProcValue(level) * 100f:0.#}% 마법 데미지 ({passive.GetProcDuration(level):0.#}초)";
            case PassiveSkillData.PassiveEffectType.OnKillHeal:
                return $"적 처치 시 최대 체력의 {passive.GetProcValue(level) * 100f:0.#}% 회복";
            case PassiveSkillData.PassiveEffectType.HealCrit:
                return "힐에 치명타 적용 (치명타 확률로 발동, 치명타 데미지만큼 힐량 증가)";
            case PassiveSkillData.PassiveEffectType.OnHealAtkSpeedUp:
                return $"힐 받은 대상 공격 속도 {passive.GetProcValue(level) * 100f:0.#}% 증가 ({passive.GetProcDuration(level):0.#}초)";
            case PassiveSkillData.PassiveEffectType.OnHitCooldownReset:
                return $"평타 적중 시 {ChanceText(passive, level)}퀵슬롯 스킬 쿨타임 초기화 (쿨 초기화 스킬 제외)";
            case PassiveSkillData.PassiveEffectType.Revive:
            {
                // 실제 부활 체력 — CharacterStat.TryRevive와 같은 기준 (값이 없으면 20%)
                float ratio = value > 0f ? value : 0.2f;
                return $"사망 시 최대 체력의 {ratio * 100f:0.#}%로 1회 부활 (쿨타임 10분)";
            }
            default:
                return "";
        }
    }

    // 발동 확률 100% 이상이면 생략, 아니면 "20% 확률로 "
    private static string ChanceText(PassiveSkillData passive, int level)
    {
        float chance = passive.GetProcChance(level);
        return chance >= 1f ? "" : $"{chance * 100f:0.#}% 확률로 ";
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
            DamageSkillData.ScalingStat.Dex => caster.TotalDex,
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
            DamageSkillData.ScalingStat.Dex => "민첩",
            _                               => "",
        };
    }

    // 스킬 타입에 맞는 설명(데미지/치유/버프/디버프/패시브)을 한 번에 조립
    public static string BuildFullDescription(SkillData skill, int level, CharacterStat caster)
    {
        if (skill is DamageSkillData dmg)
        {
            string special = GetDamageSkillSpecial(dmg, level, caster);
            return string.IsNullOrEmpty(special)
                ? BuildDamageDescription(dmg, level, caster)
                : BuildDamageDescription(dmg, level, caster) + "\n" + special;
        }
        if (skill is HealSkillData heal)   return BuildHealDescription(heal, level, caster);
        if (skill is BuffSkillData buff)   return GetBuffDescription(buff, level, caster);
        if (skill is DebuffSkillData deb)  return GetDebuffDescription(deb, level);
        if (skill is PassiveSkillData pas) return GetPassiveDescription(pas, level);
        if (skill is SummonSkillData sum)  return GetSummonDescription(sum, level, caster);
        return "";
    }

    // ─────────────────────────────────────────────────────────────────
    // 소환 스킬
    // ─────────────────────────────────────────────────────────────────

    // 시전자를 알면 실제 수치까지, 모르면 비율만 표기
    public static string GetSummonDescription(SummonSkillData s, int level, CharacterStat caster)
    {
        var sb = new StringBuilder();

        if (s.action == SummonSkillData.SummonAction.Taunt)
        {
            string kindName = s.tauntKind == SummonUnit.Kind.Melee ? "근접" : "원거리";
            sb.AppendLine($"[도발] {kindName} 소환수 주변 {s.tauntRadius:0.#}m 내의 몬스터 도발");
            sb.AppendLine($"도발 시간: {s.GetTauntDuration(level):0.#}초");
            float ratio = s.GetTauntDamageRatio(level);
            if (ratio > 0f)
            {
                // 데미지 스킬 설명과 같은 형식 — (공격력 + 스탯×계수) × 비율 [× (1 + 데미지 증가)] = 최종
                string baseLabel = s.useAp ? "마법 공격력" : "물리 공격력";
                var expr = new StringBuilder();
                if (caster != null)
                {
                    expr.Append($"({baseLabel}({(s.useAp ? caster.TotalAp : caster.TotalAtk):F0})");
                    foreach (var sc in s.tauntStatScalings)
                    {
                        if (sc == null || sc.stat == DamageSkillData.ScalingStat.None) continue;
                        expr.Append($" + {StatName(sc.stat)}({GetStatValue(caster, sc.stat):F0})×{sc.GetScaling(level) * 100f:F0}%");
                    }
                    expr.Append($") × {ratio * 100f:0.#}%");
                    float dmgBonus = s.useAp ? caster.MagicDmgBonus : caster.PhysDmgBonus;
                    if (dmgBonus != 0f)
                        expr.Append($" × (1 + {(s.useAp ? "마법" : "물리")} 데미지 증가 {dmgBonus * 100f:0.#}%)");
                    sb.AppendLine($"데미지: {expr} = {s.GetTauntRawDamage(level, caster):F0}");
                }
                else
                {
                    expr.Append($"({baseLabel}");
                    foreach (var sc in s.tauntStatScalings)
                    {
                        if (sc == null || sc.stat == DamageSkillData.ScalingStat.None) continue;
                        expr.Append($" + {StatName(sc.stat)}×{sc.GetScaling(level) * 100f:F0}%");
                    }
                    expr.Append($") × {ratio * 100f:0.#}%");
                    sb.AppendLine($"데미지: {expr}");
                }
            }
            sb.Append($"{kindName} 소환수가 1기 이상 있어야 사용 가능");
            return sb.ToString();
        }

        string kind   = s.SummonKind == SummonUnit.Kind.Melee ? "근접" : "원거리";
        string dmgStr = s.useAp ? "마법 공격력" : "물리 공격력";
        float  hpR    = s.GetHpRatio(level);
        float  atkR   = s.GetAtkRatio(level);
        float  defR   = s.GetDefRatio(level);

        sb.AppendLine($"[{kind} 소환수 소환] {s.GetSummonCount(level)}마리, {s.GetDuration(level):0.#}초 (재사용 시 교체)");
        if (caster != null)
        {
            float atkBase = s.useAp ? caster.TotalAp : caster.TotalAtk;
            sb.AppendLine($"체력: 최대 체력({caster.MaxHp:F0}) × {hpR * 100f:0.#}% = {caster.MaxHp * hpR:F0}");
            sb.AppendLine($"공격력: {dmgStr}({atkBase:F0}) × {atkR * 100f:0.#}% = {atkBase * atkR:F0}");
        }
        else
        {
            sb.AppendLine($"체력: 최대 체력 × {hpR * 100f:0.#}%");
            sb.AppendLine($"공격력: {dmgStr} × {atkR * 100f:0.#}%");
        }
        sb.Append($"방어력·마법 저항력: 시전자의 {defR * 100f:0.#}%");
        return sb.ToString();
    }

    // 전투 퀵슬롯 호버 팝업 전용 — 계산식/[단일]·[광역] 태그 없이 최종 수치만 (데미지/치유/버프),
    // 디버프는 원래도 최종 수치만 표기라 GetDebuffDescription을 그대로 사용
    public static string BuildCombatDescription(SkillData skill, int level, CharacterStat caster)
    {
        if (skill is DamageSkillData dmg)
        {
            string dmgLine = BuildDamageFinal(dmg, level, caster);
            string special = GetDamageSkillSpecial(dmg, level, caster);
            return string.IsNullOrEmpty(special) ? dmgLine : dmgLine + "\n" + special;
        }
        if (skill is HealSkillData heal)   return BuildHealFinal(heal, level, caster);
        if (skill is BuffSkillData buff)   return GetBuffDescriptionFinal(buff, level, caster);
        if (skill is DebuffSkillData deb)  return GetDebuffDescription(deb, level);
        if (skill is PassiveSkillData pas) return GetPassiveDescription(pas, level);
        if (skill is SummonSkillData sum)  return GetSummonDescription(sum, level, caster);
        return "";
    }
}

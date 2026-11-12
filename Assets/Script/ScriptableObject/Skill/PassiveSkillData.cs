using UnityEngine;

[CreateAssetMenu(fileName = "PassiveSkill", menuName = "Scriptable Object/PassiveSkillData")]
public class PassiveSkillData : SkillData
{
    public PassiveEffectType effectType;

    [Tooltip("Atk/Ap/Def/MagicRes/MaxHp, PhysDmgReduction/MagicDmgReduction 타입에서만 사용.\n" +
             "Flat: 수치 그대로 증가·감소 (20 = 20)\n" +
             "Percent: 능력치는 스탯·장비 기본 수치 기준 증가, 데미지 감소는 받는 데미지 비율 감소 (0.1 = 10%)")]
    public ModifierMode valueMode = ModifierMode.Flat;

    public float baseValue     = 0f;
    public float valuePerLevel = 0f;

    // 두 번째 효과 — 방어력 + 마법 저항력처럼 능력치 두 개를 함께 올리는 패시브용.
    // 부활·발동형 효과는 첫 번째 효과 타입만 보고 동작하므로 여기엔 수치 증가형만 허용한다
    public bool hasSecondEffect = false;
    public PassiveEffectType secondEffectType = PassiveEffectType.MagicRes;
    [Tooltip("Atk/Ap/Def/MagicRes/MaxHp, PhysDmgReduction/MagicDmgReduction 타입에서만 사용. Flat: 20 = 20 / Percent: 0.1 = 10%")]
    public ModifierMode secondValueMode = ModifierMode.Flat;
    public float secondBaseValue     = 0f;
    public float secondValuePerLevel = 0f;

    [Tooltip("발동 확률 (1 = 100%, 0.2 = 20%)")]
    public float baseProcChance     = 0f;
    public float procChancePerLevel = 0f;
    public float baseProcValue      = 0f;
    public float procValuePerLevel  = 0f;
    [Tooltip("공격 속도 증가·독처럼 지속시간이 있는 발동 효과의 지속시간 (초)")]
    public float baseProcDuration     = 0f;
    public float procDurationPerLevel = 0f;
    [Tooltip("발동 시 대상 위치에 스폰할 이펙트 풀 키 (비우면 없음)")]
    public string procEffectPoolKey;
    [Tooltip("OnCritLightning 전용: 발동할 데미지 스킬. 이 스킬의 데미지 공식을 캐릭터가 배운 레벨(최소 1)로 계산한다.\n" +
             "비우면 마법 공격력 × 발동 수치로 계산")]
    public DamageSkillData procSkill;
    [Tooltip("OnDebuffExtraDamage 전용: 디버프를 최대 몇 개까지 셀지 (0 = 제한 없음)")]
    [Min(0)] public int maxDebuffStacks = 0;

    public float GetValue(int level)      => baseValue + (valuePerLevel * (level - 1));
    public float GetProcChance(int level) => baseProcChance + (procChancePerLevel * (level - 1));
    public float GetProcValue(int level)  => baseProcValue + (procValuePerLevel * (level - 1));
    public float GetProcDuration(int level) => baseProcDuration + (procDurationPerLevel * (level - 1));
    public float GetSecondValue(int level) => secondBaseValue + (secondValuePerLevel * (level - 1));

    // 두 번째 효과가 켜져 있고, 허용된 수치 증가형 타입일 때만 적용한다
    public bool HasValidSecondEffect => hasSecondEffect && IsValueEffect(secondEffectType);

    // 효과 타입마다 실제로 읽는 칸 — 인스펙터(PassiveSkillDataEditor)가 안 쓰는 칸을 숨기는 데 사용.
    // 발동 코드(CharacterStatTriggers / CharacterStat.TryRevive / CharacterStatus.ApplyPassiveEffect)와 맞춰 둘 것
    [System.Flags]
    public enum UsedField
    {
        None            = 0,
        Value           = 1 << 0, // Base Value / Value Per Level
        ProcChance      = 1 << 1,
        ProcValue       = 1 << 2,
        ProcDuration    = 1 << 3,
        ProcEffect      = 1 << 4, // Proc Effect Pool Key
        ProcSkill       = 1 << 5,
        MaxDebuffStacks = 1 << 6,
    }

    public static UsedField GetUsedFields(PassiveEffectType type)
    {
        switch (type)
        {
            case PassiveEffectType.OnHitAtkSpeedUp:     return UsedField.ProcChance | UsedField.ProcValue | UsedField.ProcDuration;
            case PassiveEffectType.OnDebuffExtraDamage: return UsedField.Value | UsedField.MaxDebuffStacks;
            case PassiveEffectType.OnCritLightning:     return UsedField.ProcChance | UsedField.ProcValue | UsedField.ProcEffect | UsedField.ProcSkill;
            case PassiveEffectType.OnKillHeal:          return UsedField.ProcValue;
            case PassiveEffectType.OnHitPoison:         return UsedField.ProcChance | UsedField.ProcValue | UsedField.ProcDuration | UsedField.ProcEffect;
            case PassiveEffectType.HealCrit:            return UsedField.None;
            case PassiveEffectType.OnHealAtkSpeedUp:    return UsedField.ProcValue | UsedField.ProcDuration;
            case PassiveEffectType.Revive:              return UsedField.Value | UsedField.ProcEffect;
            case PassiveEffectType.OnHitCooldownReset:  return UsedField.ProcChance;
            case PassiveEffectType.OnHitShield:         return UsedField.Value | UsedField.ProcChance | UsedField.ProcValue | UsedField.ProcDuration | UsedField.ProcEffect;
            case PassiveEffectType.OnSkillSummonShield: return UsedField.Value | UsedField.ProcValue | UsedField.ProcDuration | UsedField.ProcEffect;
            default:                                    return UsedField.Value; // 수치 증가형
        }
    }

    // 두 번째 효과로 쓸 수 있는 수치 증가형 타입 (레벨별 수치를 더하기만 하는 효과)
    public static bool IsValueEffect(PassiveEffectType type)
    {
        switch (type)
        {
            case PassiveEffectType.Atk:
            case PassiveEffectType.Ap:
            case PassiveEffectType.Def:
            case PassiveEffectType.PhysDmgBonus:
            case PassiveEffectType.MagicDmgBonus:
            case PassiveEffectType.CritRate:
            case PassiveEffectType.CritDamage:
            case PassiveEffectType.MaxHp:
            case PassiveEffectType.MagicRes:
            case PassiveEffectType.HealPercent:
            case PassiveEffectType.MaxMpBonus:
            case PassiveEffectType.OnHitManaRestore:
            case PassiveEffectType.PhysDmgReduction:
            case PassiveEffectType.MagicDmgReduction:
            case PassiveEffectType.AtkSpeed:
            case PassiveEffectType.FaithToHp:
            case PassiveEffectType.SummonHitManaRestore:
            case PassiveEffectType.SummonCritRate:
            case PassiveEffectType.SummonDmgBonus:
                return true;
            default:
                return false;
        }
    }

    // 발동형 — 공격·치명타·처치·힐 시점에 조건을 확인해서 터지는 효과 (Revive는 CharacterStat이 따로 처리)
    public static bool IsTriggerEffect(PassiveEffectType type)
    {
        switch (type)
        {
            case PassiveEffectType.OnHitAtkSpeedUp:
            case PassiveEffectType.OnDebuffExtraDamage:
            case PassiveEffectType.OnCritLightning:
            case PassiveEffectType.OnKillHeal:
            case PassiveEffectType.OnHitPoison:
            case PassiveEffectType.HealCrit:
            case PassiveEffectType.OnHealAtkSpeedUp:
            case PassiveEffectType.OnHitCooldownReset:
            case PassiveEffectType.OnHitShield:
            case PassiveEffectType.OnSkillSummonShield:
                return true;
            default:
                return false;
        }
    }

    // Value Mode(고정/퍼센트)를 따르는 타입 — 능력치 5종 + 받는 데미지 감소 2종
    public static bool SupportsValueMode(PassiveEffectType type)
        => TryGetModifierStat(type, out _)
        || type == PassiveEffectType.PhysDmgReduction
        || type == PassiveEffectType.MagicDmgReduction;

    // 첫 번째 효과 기준 (기존 호출부 호환용)
    public bool TryGetModifierStat(out ModifierStat stat) => TryGetModifierStat(effectType, out stat);

    // 고정/퍼센트 증가를 지원하는 능력치 타입이면 대응하는 ModifierStat을 돌려준다
    public static bool TryGetModifierStat(PassiveEffectType type, out ModifierStat stat)
    {
        switch (type)
        {
            case PassiveEffectType.Atk:      stat = ModifierStat.Atk;      return true;
            case PassiveEffectType.Ap:       stat = ModifierStat.Ap;       return true;
            case PassiveEffectType.Def:      stat = ModifierStat.Def;      return true;
            case PassiveEffectType.MagicRes: stat = ModifierStat.MagicRes; return true;
            case PassiveEffectType.MaxHp:    stat = ModifierStat.MaxHp;    return true;
            default:                         stat = default;               return false;
        }
    }

    // 세이브/에셋에는 정수로 저장되므로 번호는 고정 — 선언 순서는 인스펙터 목록 순서(종류별 묶음)일 뿐.
    // 새 타입은 지금 가장 큰 번호(29) 다음 번호로 추가하고, 어울리는 묶음 안에 넣을 것
    public enum PassiveEffectType
    {
        // 공격 (수치 증가형) — Atk/Ap는 valueMode로 고정/퍼센트 선택
        Atk                  = 0,  // 물리 공격력 증가
        Ap                   = 1,  // 마법 공격력 증가
        PhysDmgBonus         = 3,  // 최종 물리 피해 % 증가
        MagicDmgBonus        = 4,  // 최종 마법 피해 % 증가
        CritRate             = 5,  // 치명타 확률 증가
        CritDamage           = 6,  // 치명타 배율 증가
        AtkSpeed             = 23, // 공격 속도 증가 (0.1 = +10%)
        SummonCritRate       = 27, // 소환수 치명타 확률 증가 (0.04 = +4%) — 시전자 본인은 오르지 않음
        SummonDmgBonus       = 28, // 소환수 피해 증가 (0.1 = +10%) — 소환수 평타·명령 스킬 피해에만

        // 방어 (수치 증가·감소형) — Def/MaxHp/MagicRes·데미지 감소는 valueMode로 고정/퍼센트 선택
        Def                  = 2,  // 방어력 증가
        MagicRes             = 8,  // 마법 저항력 증가
        MaxHp                = 7,  // 최대 체력 증가
        FaithToHp            = 10, // 신앙 스탯 비례 체력 증가
        PhysDmgReduction     = 21, // 받는 물리 데미지 감소
        MagicDmgReduction    = 22, // 받는 마법 데미지 감소

        // 회복 / 마나 (수치 증가형)
        HealPercent          = 9,  // 힐량 % 증가
        MaxMpBonus           = 11, // 최대 마나 고정 증가 (퍼센트 없음)
        OnHitManaRestore     = 12, // 평타 적중 시 마나 회복
        SummonHitManaRestore = 25, // 소환수가 공격을 적중시킬 때마다 주인 마나 고정 회복

        // 발동형 — 평타 적중 시
        OnHitAtkSpeedUp      = 13, // 평타 적중 시 공격 속도 증가
        OnHitPoison          = 17, // 공격 시 독 발동
        OnHitShield          = 26, // 평타 적중 시 확률로 자신에게 보호막 (최대 체력 비례)
        OnHitCooldownReset   = 24, // 평타 적중 시 확률로 퀵슬롯 스킬 쿨타임 초기화 (쿨 초기화 스킬은 제외)

        // 발동형 — 스킬 사용 시
        OnSkillSummonShield  = 29, // 본인이 스킬을 쓸 때마다 자기 소환수 전원에게 보호막 (소환수 최대 체력 비례)
        OnCritLightning      = 15, // 치명타 시 번개 발동

        // 발동형 — 그 외
        OnDebuffExtraDamage  = 14, // 디버프 걸린 적에게 추가 데미지
        OnKillHeal           = 16, // 적 처치 시 체력 회복
        HealCrit             = 18, // 힐에 치명타 적용
        OnHealAtkSpeedUp     = 19, // 힐 받은 대상 공격 속도 증가
        Revive               = 20, // 1회 부활 (쿨타임 10분)
    }
}
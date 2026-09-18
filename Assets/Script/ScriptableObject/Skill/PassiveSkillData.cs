using UnityEngine;

[CreateAssetMenu(fileName = "PassiveSkill", menuName = "Scriptable Object/PassiveSkillData")]
public class PassiveSkillData : SkillData
{
    [Header("패시브 효과 타입")]
    public PassiveEffectType effectType;

    [Tooltip("Atk/Ap/Def/MagicRes/MaxHp 타입에서만 사용.\n" +
             "Flat: 수치 그대로 증가 (20 = +20)\n" +
             "Percent: 스탯·장비로 얻은 기본 수치 기준 증가 (0.1 = +10%)")]
    public ModifierMode valueMode = ModifierMode.Flat;

    [Header("수치 설정")]
    public float baseValue     = 0f;
    public float valuePerLevel = 0f;

    // 두 번째 효과 — 방어력 + 마법 저항력처럼 능력치 두 개를 함께 올리는 패시브용.
    // 부활·발동형 효과는 첫 번째 효과 타입만 보고 동작하므로 여기엔 수치 증가형만 허용한다
    [Header("두 번째 효과 (선택, 수치 증가형만)")]
    public bool hasSecondEffect = false;
    public PassiveEffectType secondEffectType = PassiveEffectType.MagicRes;
    [Tooltip("Atk/Ap/Def/MagicRes/MaxHp 타입에서만 사용. Flat: 20 = +20 / Percent: 0.1 = +10%")]
    public ModifierMode secondValueMode = ModifierMode.Flat;
    public float secondBaseValue     = 0f;
    public float secondValuePerLevel = 0f;

    [Header("특수 효과 설정 (트리거 기반)")]
    public float baseProcChance     = 0f;
    public float procChancePerLevel = 0f;
    public float baseProcValue      = 0f;
    public float procValuePerLevel  = 0f;
    public string procEffectPoolKey;

    public float GetValue(int level)      => baseValue + (valuePerLevel * (level - 1));
    public float GetProcChance(int level) => baseProcChance + (procChancePerLevel * (level - 1));
    public float GetProcValue(int level)  => baseProcValue + (procValuePerLevel * (level - 1));
    public float GetSecondValue(int level) => secondBaseValue + (secondValuePerLevel * (level - 1));

    // 두 번째 효과가 켜져 있고, 허용된 수치 증가형 타입일 때만 적용한다
    public bool HasValidSecondEffect => hasSecondEffect && IsValueEffect(secondEffectType);

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
                return true;
            default:
                return false;
        }
    }

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

    // 세이브/에셋에는 정수로 저장되므로 기존 항목의 순서(값)는 바꾸지 말 것
    public enum PassiveEffectType
    {
        // 수치 증가형 — Atk/Ap/Def/MaxHp/MagicRes는 valueMode로 고정/퍼센트 선택
        Atk,                 // 물리 공격력 증가
        Ap,                  // 마법 공격력 증가
        Def,                 // 방어력 증가
        PhysDmgBonus,        // 최종 물리 피해 % 증가
        MagicDmgBonus,       // 최종 마법 피해 % 증가
        CritRate,            // 치명타 확률 증가
        CritDamage,          // 치명타 배율 증가
        MaxHp,               // 최대 체력 증가
        MagicRes,            // 마법 저항력 증가
        HealPercent,         // 힐량 % 증가
        FaithToHp,           // 신앙 스탯 비례 체력 증가
        MaxMpBonus,          // 최대 마나 고정 증가 (퍼센트 없음)

        // 트리거형
        OnHitManaRestore,    // 평타 적중 시 마나 회복
        OnHitAtkSpeedUp,     // 평타 적중 시 공격속도 증가
        OnDebuffExtraDamage, // 디버프 걸린 적에게 추가 데미지
        OnCritLightning,     // 치명타 시 번개 발동
        OnKillHeal,          // 적 처치 시 체력 회복
        OnHitPoison,         // 공격 시 독 발동
        HealCrit,            // 힐에 치명타 적용
        OnHealAtkSpeedUp,    // 힐 받은 대상 공격속도 증가
        Revive,              // 1회 부활 (쿨타임 10분)
    }
}
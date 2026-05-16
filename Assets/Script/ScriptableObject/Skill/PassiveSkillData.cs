using UnityEngine;

[CreateAssetMenu(fileName = "PassiveSkill", menuName = "Scriptable Object/PassiveSkillData")]
public class PassiveSkillData : SkillData
{
    [Header("패시브 효과 타입")]
    public PassiveEffectType effectType;

    [Header("수치 설정")]
    public float baseValue     = 0f;
    public float valuePerLevel = 0f;

    [Header("특수 효과 설정 (트리거 기반)")]
    public float baseProcChance     = 0f;
    public float procChancePerLevel = 0f;
    public float baseProcValue      = 0f;
    public float procValuePerLevel  = 0f;
    public string procEffectPoolKey;

    public float GetValue(int level)      => baseValue + (valuePerLevel * (level - 1));
    public float GetProcChance(int level) => baseProcChance + (procChancePerLevel * (level - 1));
    public float GetProcValue(int level)  => baseProcValue + (procValuePerLevel * (level - 1));

    public enum PassiveEffectType
    {
        // 수치 증가형
        AtkPercent,          // 물리 공격력 % 증가 (deprecated)
        ApPercent,           // 마법 공격력 % 증가 (deprecated)
        DefPercent,          // 방어력 % 증가
        PhysDmgBonus,        // 최종 물리 피해 % 증가
        MagicDmgBonus,       // 최종 마법 피해 % 증가
        CritRate,            // 치명타 확률 증가
        CritDamage,          // 치명타 배율 증가
        MaxHpPercent,        // 최대 체력 % 증가
        MagicResPercent,     // 마법 저항력 % 증가
        HealPercent,         // 힐량 % 증가
        FaithToHp,           // 신앙 스탯 비례 체력 증가

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
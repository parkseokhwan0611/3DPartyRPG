using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PassiveSkill", menuName = "Scriptable Object/PassiveSkillData")]
public class PassiveSkillData : SkillData
{
    public enum PassiveEffectType
    {
        // 단순 수치 증가
        AtkPercent,      // 공격력 n% 증가
        ApPercent,       // 주문력 n% 증가
        DefPercent,      // 방어력 n% 증가
        CritRate,        // 치명타 확률 증가
        CritDamage,      // 치명타 배율 증가
        MaxHpPercent,    // 최대 체력 n% 증가

        // 특수 효과 (트리거 기반)
        OnCritLightning, // 치명타 시 번개 일정 확률로 낙뢰
        OnHitPoison,     // 공격 시 일정 확률로 독
        OnKillHeal,      // 적 처치 시 체력 회복
        // 이후 추가 가능
    }

    [Header("패시브 효과 타입")]
    public PassiveEffectType effectType;

    [Header("수치 설정")]
    public float baseValue = 0f;        // 기본값 (1렙)
    public float valuePerLevel = 0f;    // 레벨당 증가량

    [Header("특수 효과 설정 (트리거 기반 패시브 전용)")]
    public float baseProcChance = 0f;       // 발동 확률 기본값
    public float procChancePerLevel = 0f;   // 레벨당 발동 확률 증가
    public float baseProcValue = 0f;        // 특수 효과 수치 기본값
    public float procValuePerLevel = 0f;    // 레벨당 수치 증가
    public string procEffectPoolKey;        // 특수 효과 이펙트 풀 키

    public float GetValue(int level)      => baseValue + (valuePerLevel * (level - 1));
    public float GetProcChance(int level) => baseProcChance + (procChancePerLevel * (level - 1));
    public float GetProcValue(int level)  => baseProcValue + (procValuePerLevel * (level - 1));
}
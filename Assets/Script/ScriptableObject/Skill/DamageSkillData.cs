using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DamageSkill", menuName = "Scriptable Object/DamageSkillData")]
public class DamageSkillData : SkillData
{
    [Header("데미지 설정")]
    public float baseDamageMultiplier = 1.0f;  // 기본 배율
    public float damageMultiplierPerLevel = 0.1f; // 레벨당 증가 배율
    public bool useAp; //True면 AP, False면 AD
    [Header("사거리 설정")]
    public float castRange = 3f;  
    public float baseRange = 3f;
    public float rangePerLevel = 0f;
    public bool isAoe;
    [Header("부가 디버프 효과 (선택)")]
    public List<DebuffSkillData.DebuffEffect> onHitDebuffs = new List<DebuffSkillData.DebuffEffect>();

    [Header("다음 스킬 연계 버프 (선택)")]
    public bool hasNextSkillBuff       = false;
    public float nextSkillDamageBonus  = 0.2f;  // 20% 증가
    public float nextSkillBuffDuration = 4f;    // 4초

    [Header("애니메이션 / 이펙트")]
    public string animTriggerName;    // 애니메이터 트리거 이름
    public float animDuration;        // 애니메이션 전체 길이
    public float effectSpawnDelay;    // 이펙트 생성 타이밍 (선딜)
    public string effectPoolKey;      // 오브젝트 풀 키

    // 최종 수치 계산
    public float GetDamageMultiplier(int level)
        => baseDamageMultiplier + (damageMultiplierPerLevel * (level - 1));

    public float GetRange(int level)
        => baseRange + (rangePerLevel * (level - 1));
}
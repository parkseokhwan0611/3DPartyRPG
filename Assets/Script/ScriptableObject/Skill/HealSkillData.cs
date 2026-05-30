using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "HealSkill", menuName = "Scriptable Object/HealSkillData")]
public class HealSkillData : SkillData
{
    public enum HealTargetType { Single, Party }

    [Header("힐 설정")]
    public HealTargetType targetType = HealTargetType.Single;
    public bool useApRatio = true;       // AP 비례 힐 여부
    public float baseHealMultiplier = 1f;
    public float healMultiplierPerLevel = 0.1f;

    [Header("스탯 배율 (선택, 최대 2개)")]
    public List<DamageSkillData.StatScaling> statScalings = new List<DamageSkillData.StatScaling>();

    [Header("도트 힐 설정")]
    public bool isDotHeal = false;            // 도트 힐 여부
    public float dotInterval = 1f;            // 틱 간격 (초)
    public float baseDotDuration    = 5f;     // 1레벨 지속시간 (초)
    public float dotDurationPerLevel = 0f;    // 레벨당 추가 지속시간 (초)

    public float GetDotDuration(int level) => baseDotDuration + dotDurationPerLevel * (level - 1);

    [Header("애니메이션 / 이펙트")]
    public string animTriggerName;
    public float animDuration;
    public float effectSpawnDelay;
    public string effectPoolKey;
    public Vector3 effectSpawnOffset;           // 시전자 이펙트 위치 오프셋 (로컬 스페이스)
    public Vector3 effectSpawnRotation;         // 시전자 이펙트 회전 오프셋 (오일러각, 로컬 스페이스)
    public string targetEffectPoolKey;          // 대상에게 생기는 이펙트
    public Vector3 targetEffectSpawnOffset;     // 대상 이펙트 위치 오프셋 (로컬 스페이스)
    public Vector3 targetEffectSpawnRotation;   // 대상 이펙트 회전 오프셋 (오일러각, 로컬 스페이스)

    public float GetHealMultiplier(int level)
        => baseHealMultiplier + (healMultiplierPerLevel * (level - 1));

    public float GetTotalStatBonus(int level, System.Func<DamageSkillData.ScalingStat, float> getStatValue)
    {
        float total = 0f;
        foreach (var s in statScalings)
        {
            if (s.stat == DamageSkillData.ScalingStat.None) continue;
            total += getStatValue(s.stat) * s.GetScaling(level);
        }
        return total;
    }
}
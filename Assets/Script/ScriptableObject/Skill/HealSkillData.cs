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

    [Header("추가 스탯 계수")]
    [Tooltip("지능(Int) 1당 힐 베이스에 더해지는 비율 (예: 0.5 → Int × 0.5 추가)")]
    public float intRatio = 0f;
    [Tooltip("신앙(Fth) 1당 힐 베이스에 더해지는 비율 (예: 0.5 → Fth × 0.5 추가)")]
    public float fthRatio = 0f;

    [Header("도트 힐 설정")]
    public bool isDotHeal = false;       // 도트 힐 여부
    public float dotInterval = 1f;       // 틱 간격 (초)
    public float dotDuration = 5f;       // 총 지속시간 (초)

    [Header("애니메이션 / 이펙트")]
    public string animTriggerName;
    public float animDuration;
    public float effectSpawnDelay;
    public string effectPoolKey;
    public string targetEffectPoolKey;   // 대상에게 생기는 이펙트

    public float GetHealMultiplier(int level)
        => baseHealMultiplier + (healMultiplierPerLevel * (level - 1));
}
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "HealSkill", menuName = "Scriptable Object/HealSkillData")]
public class HealSkillData : SkillData
{
    [Header("힐 설정")]
    public bool isAoe = false;           // 광역 힐 여부
    public bool useApRatio = true;       // AP 비례 힐 여부
    public float baseHealMultiplier = 1f;
    public float healMultiplierPerLevel = 0.1f;

    [Header("도트 힐 설정")]
    public bool isDotHeal = false;       // 도트 힐 여부
    public float dotInterval = 1f;       // 힐 간격 (초)

    [Header("애니메이션 / 이펙트")]
    public string animTriggerName;
    public float animDuration;
    public float effectSpawnDelay;
    public string effectPoolKey;
    public string targetEffectPoolKey;   // 대상에게 생기는 이펙트

    public float GetHealMultiplier(int level)
        => baseHealMultiplier + (healMultiplierPerLevel * (level - 1));
}
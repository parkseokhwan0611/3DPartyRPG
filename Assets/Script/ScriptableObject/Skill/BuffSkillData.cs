using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// BuffSkillData.cs 수정
[CreateAssetMenu(fileName = "BuffSkill", menuName = "Scriptable Object/BuffSkillData")]
public class BuffSkillData : SkillData
{
    [Header("버프 대상")]
    public bool isPartyBuff = false; // true면 파티 전체, false면 개인

    [Header("애니메이션 / 이펙트")]
    public string animTriggerName;
    public float animDuration;
    public float effectSpawnDelay;
    public string effectPoolKey;

    [Header("버프 지속시간 (레벨별)")]
    public float baseDuration = 5f;
    public float durationPerLevel = 1f;

    // 버프 효과 목록 (최대 3개)
    [Header("버프 효과 (최대 3개)")]
    public List<BuffEffect> buffEffects = new List<BuffEffect>();

    public float GetDuration(int level) => baseDuration + (durationPerLevel * (level - 1));

    [System.Serializable]
    public class BuffEffect
    {
        public BuffEffectType effectType;
        public float baseValue      = 0f; // 기본값
        public float valuePerLevel  = 0f; // 레벨당 증가
        public float GetValue(int level) => baseValue + (valuePerLevel * (level - 1));
    }

    public enum BuffEffectType
    {
        AtkBonus,    // 공격력 증가
        ApBonus,     // 주문력 증가
        DefBonus,    // 방어력 증가
        CritRate,    // 치명타 확률 증가
        CritDamage,  // 치명타 배율 증가
        MaxHpBonus,  // 최대 체력 증가
        SpeedBonus,  // 이동속도 증가
    }
}
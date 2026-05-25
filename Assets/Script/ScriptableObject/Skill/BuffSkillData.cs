using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BuffSkill", menuName = "Scriptable Object/BuffSkillData")]
public class BuffSkillData : SkillData
{
    public enum EffectStyle
    {
        OneShot, // 시전 시 풀에서 이펙트를 한 번 스폰하고 끝
        Aura,    // 버프 지속 중 캐릭터 아우라를 켜고, 만료 시 끔
    }

    [Header("버프 대상")]
    public bool isPartyBuff = false;

    [Header("이펙트 스타일")]
    public EffectStyle effectStyle = EffectStyle.OneShot;
    [Tooltip("OneShot 전용: 오브젝트 풀 키")]
    public string effectPoolKey;
    [Tooltip("Aura 전용: CharacterStat.buffAuras 배열의 인덱스")]
    public int auraIndex = 0;

    [Header("애니메이션 / 이펙트")]
    public string animTriggerName;
    public float animDuration;
    public float effectSpawnDelay;
    public Vector3 effectSpawnOffset;   // 스폰 위치 오프셋 (로컬 스페이스)
    public Vector3 effectSpawnRotation; // 스폰 회전 오프셋 (오일러각, 로컬 스페이스)

    [Header("버프 지속시간 (레벨별)")]
    public float baseDuration = 5f;
    public float durationPerLevel = 1f;

    [Header("버프 효과 (최대 3개)")]
    public List<BuffEffect> buffEffects = new List<BuffEffect>();

    public float GetDuration(int level) => baseDuration + (durationPerLevel * (level - 1));

    public enum ScalingStat
    {
        None, // 스탯 비례 없음
        Str,  // 힘
        Vit,  // 체력
        Int,  // 지능
        Fth,  // 신앙
    }

    [System.Serializable]
    public class BuffEffect
    {
        public BuffEffectType effectType;

        [Header("기본 수치")]
        public float baseValue     = 0f;
        public float valuePerLevel = 0f;

        [Header("스탯 비례 계수 (시전자 기준)")]
        public ScalingStat scalingStat     = ScalingStat.None;
        public float       scalingCoeff    = 0f; // 기본 계수 (예: 0.3 = 스탯의 30%)
        public float       scalingPerLevel = 0f; // 레벨당 계수 증가

        public float GetValue(int level)   => baseValue + (valuePerLevel * (level - 1));
        public float GetScaling(int level) => scalingCoeff + (scalingPerLevel * (level - 1));
    }

    public enum BuffEffectType
    {
        AtkBonus,
        ApBonus,
        DefBonus,
        MagicResBonus,  // 마법 저항력 증가
        CritRate,
        CritDamage,
        MaxHpBonus,
        SpeedBonus,
        Shield,
        ManaRegen,
        HpRegen,
        HpOnHit,        // 기본 공격 적중 시 체력 회복
        DebuffImmune,
        DispelDebuff,   // 즉시 디버프 전체 제거
    }
}

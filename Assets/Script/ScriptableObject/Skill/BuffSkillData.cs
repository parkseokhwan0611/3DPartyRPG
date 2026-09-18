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

        [Tooltip("AtkBonus/ApBonus/DefBonus/MagicResBonus/MaxHpBonus에서만 사용.\n" +
                 "Flat: 수치 그대로 증가 (20 = +20)\n" +
                 "Percent: 대상의 스탯·장비 기본 수치 기준 증가 (0.1 = +10%). 스탯 비례 계수도 같은 단위로 더해짐")]
        public ModifierMode valueMode = ModifierMode.Flat;

        [Header("기본 수치")]
        public float baseValue     = 0f;
        public float valuePerLevel = 0f;

        [Header("스탯 비례 계수 (시전자 기준)")]
        public ScalingStat scalingStat     = ScalingStat.None;
        public float       scalingCoeff    = 0f; // 기본 계수 (예: 0.3 = 스탯의 30%)
        public float       scalingPerLevel = 0f; // 레벨당 계수 증가

        public float GetValue(int level)   => baseValue + (valuePerLevel * (level - 1));
        public float GetScaling(int level) => scalingCoeff + (scalingPerLevel * (level - 1));

        // 퍼센트 증가가 실제로 적용되는 효과인지 (그 외 타입은 valueMode와 무관하게 고정 증가)
        public bool IsPercent => valueMode == ModifierMode.Percent && SupportsPercent(effectType);
    }

    public static bool SupportsPercent(BuffEffectType type)
    {
        switch (type)
        {
            case BuffEffectType.AtkBonus:
            case BuffEffectType.ApBonus:
            case BuffEffectType.DefBonus:
            case BuffEffectType.MagicResBonus:
            case BuffEffectType.MaxHpBonus:
                return true;
            default:
                return false;
        }
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
        Thorns,         // 가시 반사 — 지속시간 동안 피격 시 공격자에게 데미지 (시전자 자신에게만 적용)
    }

    // ─────────────────────────────────────────────────────────────────
    // 가시 반사 (Thorns 효과 전용)
    // ─────────────────────────────────────────────────────────────────

    // 반사 데미지 계수에 쓸 수 있는 능력치 — 받은 데미지가 아니라 시전자 능력치로 계산
    public enum ThornsStat
    {
        Def,       // 방어력
        MagicRes,  // 마법 저항력
        MaxHp,     // 최대 체력
        Atk,       // 물리 공격력
        Ap,        // 마법 공격력
        Str,       // 힘
        Vit,       // 체력
        Int,       // 지능
        Fth,       // 신앙
    }

    [System.Serializable]
    public class ThornsScaling
    {
        public ThornsStat stat;
        [Tooltip("능력치 대비 비율 (0.5 = 능력치의 50%)")]
        public float coeff         = 0f;
        public float coeffPerLevel = 0f;

        public float GetCoeff(int level) => coeff + (coeffPerLevel * (level - 1));
    }

    [Header("가시 반사 (Thorns 효과 전용)")]
    [Tooltip("체크: 마법 데미지 (몬스터 마법 저항력으로 경감)\n해제: 물리 데미지 (몬스터 방어력으로 경감)")]
    public bool thornsIsMagic = true;
    [Tooltip("반사 데미지 = Thorns 효과의 기본 수치 + Σ(능력치 × 계수)\n" +
             "피격 시점의 능력치로 계산하며, 시전자의 치명타 확률·치명타 데미지가 적용됨.\n" +
             "Thorns 효과의 '스탯 비례 계수' 칸은 쓰지 않고 이 목록을 사용")]
    public List<ThornsScaling> thornsScalings = new List<ThornsScaling>();
    [Tooltip("반사할 때마다 공격한 몬스터에게 쌓을 어그로 (0이면 없음)")]
    public float thornsAggroPerHit = 15f;
    [Tooltip("반사 적중 시 공격한 몬스터 위치에 스폰할 이펙트 풀 키 (비우면 없음)")]
    public string thornsHitEffectPoolKey;

    public static float GetThornsStatValue(ThornsStat stat, CharacterStat owner)
    {
        if (owner == null) return 0f;
        return stat switch
        {
            ThornsStat.Def      => owner.TotalDef,
            ThornsStat.MagicRes => owner.TotalMagicRes,
            ThornsStat.MaxHp    => owner.MaxHp,
            ThornsStat.Atk      => owner.TotalAtk,
            ThornsStat.Ap       => owner.TotalAp,
            ThornsStat.Str      => owner.TotalStr,
            ThornsStat.Vit      => owner.TotalVit,
            ThornsStat.Int      => owner.TotalInt,
            ThornsStat.Fth      => owner.TotalFth,
            _                   => 0f,
        };
    }

    // 치명타 적용 전 반사 데미지 — owner가 null이면 기본 수치만 (설명문에서 시전자를 모를 때)
    public float GetThornsDamage(BuffEffect thornsEffect, int level, CharacterStat owner)
    {
        float damage = thornsEffect != null ? thornsEffect.GetValue(level) : 0f;
        foreach (var s in thornsScalings)
        {
            if (s == null) continue;
            damage += GetThornsStatValue(s.stat, owner) * s.GetCoeff(level);
        }
        return Mathf.Max(0f, damage);
    }
}

using UnityEngine;

public enum DamageType
{
    Physical,
    Magic,
}

public enum StatusEffectType
{
    Stun,
    Slow,
    AtkDown,
    MoveSpeedDown,
    DefDown,
    AtkUp,
    DefUp,
    MagicResUp,
    AtkSpeedUp,
    HpRegen,
    ManaRegen,
    Shield,
    DebuffImmune,
    ApUp,
    CritRateUp,
    CritDamageUp,
    MaxHpUp,
    HpOnHitUp,
}

// 버프/패시브 능력치 증가 방식
// Flat   : 최종 수치에 그대로 N 더함
// Percent: 스탯·장비로 얻은 기본 수치에만 비례해 증가 (0.1 = +10%). 다른 스킬 증가분에는 곱해지지 않고,
//          여러 개가 겹치면 합산된다 (10% + 20% = 기본 수치의 30%)
public enum ModifierMode
{
    Flat,
    Percent,
}

// 고정/퍼센트 증가를 모두 지원하는 능력치 (최대 마나·치명타 등은 고정 증가만 지원)
public enum ModifierStat
{
    Atk,
    Ap,
    Def,
    MagicRes,
    MaxHp,
}

[System.Serializable]
public class StatusEffect
{
    public StatusEffectType effectType;
    public float value;
    public float duration;
    public GameObject source;
    // 능력치 증가 버프(AtkUp/ApUp/DefUp/MagicResUp/MaxHpUp)에서만 의미 있음
    public ModifierMode mode;

    [System.NonSerialized]
    public Coroutine routine;

    public StatusEffect(StatusEffectType type, float value, float duration, GameObject source,
                        ModifierMode mode = ModifierMode.Flat)
    {
        this.effectType = type;
        this.value      = value;
        this.duration   = duration;
        this.source     = source;
        this.mode       = mode;
    }
}
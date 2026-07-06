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

[System.Serializable]
public class StatusEffect
{
    public StatusEffectType effectType;
    public float value;
    public float duration;
    public GameObject source;

    [System.NonSerialized]
    public Coroutine routine;

    public StatusEffect(StatusEffectType type, float value, float duration, GameObject source)
    {
        this.effectType = type;
        this.value      = value;
        this.duration   = duration;
        this.source     = source;
    }
}
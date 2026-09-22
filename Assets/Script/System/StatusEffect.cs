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
    Thorns,         // 가시 반사 — 수치 없이 시작/종료 알림용 (PartyBuffVfx 등에서 연출 매핑 가능)
    Poison,         // 독 (몬스터 전용 디버프) — value = 1초마다 입히는 마법 데미지
    DmgReductionUp, // 받는 데미지 감소 (물리·마법 공통, 0.2 = 20%)
    Invulnerable,   // 무적 — 데미지와 디버프를 모두 무시
    MoveSpeedUp,    // 이동속도 증가 (0.2 = +20%)
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

    // 같은 키로 다시 걸면 누적하지 않고 기존 것을 지우고 새로 건다 (발동형 패시브의 공격속도 증가처럼
    // 매 타격마다 갱신되는 효과용). null이면 기존처럼 독립적으로 누적
    [System.NonSerialized]
    public object refreshKey;

    // 이 효과의 수치를 실제로 더한 CharacterStatus — 해제할 때 조회 시점의 상태가 아니라
    // 적용했던 그 객체에서 되돌려야 데이터가 교체된 뒤(새 게임·불러오기)에도 새 상태가 오염되지 않는다
    [System.NonSerialized]
    public CharacterStatus appliedStatus;

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
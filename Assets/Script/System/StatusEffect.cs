using UnityEngine;

public enum DamageType
{
    Physical,
    Magic,
}

// 에셋(스킬의 적중 디버프·시전 버프, PartyBuffVfx 등)에 정수로 저장되므로 번호는 고정 —
// 선언 순서는 인스펙터 목록 순서(종류별 묶음)일 뿐. 새 타입은 지금 가장 큰 번호(29) 다음 번호로 추가할 것
public enum StatusEffectType
{
    // 디버프
    Stun               = 0,
    Slow               = 1,
    MoveSpeedDown      = 3,
    AtkDown            = 2,
    DefDown            = 4,
    Poison             = 19, // 독 (몬스터 전용 디버프) — value = 1초마다 입히는 마법 데미지

    // 버프 — 공격
    AtkUp              = 5,
    ApUp               = 13,
    PhysDmgUp          = 26, // 최종 물리 데미지 증가 (0.3 = +30%, 패시브·장비 보너스와 합산)
    MagicDmgUp         = 27, // 최종 마법 데미지 증가 (0.3 = +30%, 패시브·장비 보너스와 합산)
    MainSkillDmgUp     = 28, // 메인 데미지 스킬 피해 증가 (0.3 = +30%, 물리/마법 데미지 증가와 합산)
    CritRateUp         = 14,
    CritDamageUp       = 15,
    AtkSpeedUp         = 8,
    OnHitPhysDamageUp  = 23, // 기본 공격 적중 시 물리 추가 피해 (치명타 미적용)
    OnHitMagicDamageUp = 24, // 기본 공격 적중 시 마법 추가 피해 (치명타 미적용)

    // 버프 — 방어
    DefUp              = 6,
    MagicResUp         = 7,
    MaxHpUp            = 16,
    Shield             = 11,
    DmgReductionUp     = 20, // 받는 데미지 감소 (물리·마법 공통, mode Percent 0.2 = 20% / Flat 20 = 한 대당 -20)
    Thorns             = 18, // 가시 반사 — 수치 없이 시작/종료 알림용 (PartyBuffVfx 등에서 연출 매핑 가능)
    Invulnerable       = 21, // 무적 — 데미지와 디버프를 모두 무시
    DebuffImmune       = 12,

    // 버프 — 회복 / 이동
    HpRegen            = 9,
    ManaRegen          = 10,
    HpOnHitUp          = 17,
    MoveSpeedUp        = 22, // 이동 속도 증가 (0.2 = +20%)

    // 대가 (강화 스킬의 단점 — 디버프가 아니라 면역·해제 대상 아님)
    MoveSpeedPenalty   = 25, // 자신 이동 속도 감소 (0.3 = 30% 감속)
    MpCostUp           = 29, // 마나 소모 증가 (0.5 = +50%, 모든 스킬)
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
    // 능력치 증가 버프(AtkUp/ApUp/DefUp/MagicResUp/MaxHpUp), DmgReductionUp, 몬스터 AtkDown/DefDown에서만 의미 있음
    public ModifierMode mode;

    [System.NonSerialized]
    public Coroutine routine;

    // 같은 키로 다시 걸면 누적하지 않고 기존 것을 지우고 새로 건다 (발동형 패시브의 공격 속도 증가처럼
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
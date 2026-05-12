using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// 상태이상 타입
public enum StatusEffectType
{
    // 디버프
    Stun,           // 스턴 (행동 불가)
    Slow,           // 슬로우 (이속 감소)
    AtkDown,        // 공격력 감소
    MoveSpeedDown,  // 이속 감소
    DefDown,        // 방어력 감소

    // 버프
    AtkUp,          // 공격력 증가
    DefUp,          // 방어력 증가
    MagicResUp,     // 마법 저항력 증가
    AtkSpeedUp,     // 공격속도 증가
    HpRegen,        // 체력 재생
    ManaRegen,      // 마나 재생
    Shield,         // 쉴드
    DebuffImmune,   // 디버프 면역
}

// 개별 상태이상 데이터
[System.Serializable]
public class StatusEffect
{
    public StatusEffectType effectType;
    public float value;       // 효과 수치 (슬로우 30% = 0.3f)
    public float duration;    // 지속 시간
    public GameObject source; // 시전자

    public StatusEffect(StatusEffectType type, float value, float duration, GameObject source)
    {
        this.effectType = type;
        this.value      = value;
        this.duration   = duration;
        this.source     = source;
    }
}
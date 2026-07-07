using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// 파티원에 붙는 버프/상태이상 처리 컴포넌트
public class PartyStatusEffectHandler : MonoBehaviour
{
    private CharacterStat myStat;
    private AttackBase attackBase;

    // 쉴드 수치
    public float CurrentShield { get; private set; } = 0f;

    // 디버프 면역 여부
    public bool IsDebuffImmune { get; private set; } = false;

    // 버프 이벤트 (UI 갱신용)
    public System.Action OnShieldChanged;
    public System.Action<StatusEffectType, bool> OnBuffChanged;

    // 활성화된 버프 목록
    private List<StatusEffect> activeBuffs = new List<StatusEffect>();

    void Awake()
    {
        myStat     = GetComponent<CharacterStat>();
        attackBase = GetComponent<AttackBase>();
    }

    // ─────────────────────────────────────────────────────────────────
    // 버프 적용
    // ─────────────────────────────────────────────────────────────────

    public void ApplyBuff(StatusEffect effect)
    {
        // 디버프 면역 중이면 디버프 무시
        if (IsDebuffImmune && IsDebuff(effect.effectType)) return;

        // 같은 타입이라도 교체하지 않고 독립적으로 누적 — 각자 자기 지속시간에 따라 개별 종료
        activeBuffs.Add(effect);
        effect.routine = StartCoroutine(BuffRoutine(effect));
        ApplyBuffValue(effect, true);

        OnBuffChanged?.Invoke(effect.effectType, true);
    }

    // 쉴드 적용
    public void ApplyShield(float amount, float duration, GameObject source)
    {
        CurrentShield += amount;
        OnShieldChanged?.Invoke();
        StartCoroutine(ShieldRoutine(amount, duration));
    }

    // ─────────────────────────────────────────────────────────────────
    // 데미지 시 쉴드 먼저 소모
    // ─────────────────────────────────────────────────────────────────

    public float AbsorbDamage(float damage)
    {
        if (CurrentShield <= 0) return damage;

        if (CurrentShield >= damage)
        {
            CurrentShield -= damage;
            OnShieldChanged?.Invoke();
            return 0f; // 데미지 전부 흡수
        }
        else
        {
            damage        -= CurrentShield;
            CurrentShield  = 0f;
            OnShieldChanged?.Invoke();
            return damage; // 남은 데미지 반환
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 버프 제거
    // ─────────────────────────────────────────────────────────────────

    // 해당 타입의 활성 버프를 전부 제거 (스택 전체 해제)
    public void RemoveBuff(StatusEffectType type)
    {
        var matches = activeBuffs.FindAll(e => e.effectType == type);
        foreach (var effect in matches)
            RemoveBuffInstance(effect);
    }

    private void RemoveBuffInstance(StatusEffect effect)
    {
        if (!activeBuffs.Remove(effect)) return;

        if (effect.routine != null)
            StopCoroutine(effect.routine);

        ApplyBuffValue(effect, false);
        OnBuffChanged?.Invoke(effect.effectType, false);
    }

    public void DispelAllDebuffs()
    {
        var debuffs = activeBuffs.FindAll(e => IsDebuff(e.effectType));
        foreach (var debuff in debuffs)
            RemoveBuffInstance(debuff);
    }

    public bool HasActiveDebuff()
    {
        return activeBuffs.Exists(e => IsDebuff(e.effectType));
    }

    // ─────────────────────────────────────────────────────────────────
    // 코루틴
    // ─────────────────────────────────────────────────────────────────

    private IEnumerator BuffRoutine(StatusEffect effect)
    {
        yield return new WaitForSeconds(effect.duration);
        effect.routine = null; // 정상 만료 시 StopCoroutine 대상 아님을 표시
        RemoveBuffInstance(effect);
    }

    private IEnumerator ShieldRoutine(float amount, float duration)
    {
        yield return new WaitForSeconds(duration);

        // 남은 쉴드에서 제거
        CurrentShield = Mathf.Max(0, CurrentShield - amount);
        OnShieldChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────
    // 버프 수치 적용/해제
    // ─────────────────────────────────────────────────────────────────

    private void ApplyBuffValue(StatusEffect effect, bool apply)
    {
        if (myStat == null) return;

        if (DataManager.instance == null) return;
        if (myStat.partyIndex < 0 || myStat.partyIndex >= DataManager.instance.partyStatuses.Count) return;
        var status = DataManager.instance.partyStatuses[myStat.partyIndex];

        float multiplier = apply ? 1f : -1f;

        switch (effect.effectType)
        {
            case StatusEffectType.AtkUp:
                status.addedStr += effect.value * multiplier;
                break;
            case StatusEffectType.DefUp:
                status.addedDef += effect.value * multiplier;
                break;
            case StatusEffectType.MagicResUp:
                status.addedMagicRes += effect.value * multiplier;
                break;
            case StatusEffectType.AtkSpeedUp:
                if (attackBase != null)
                    attackBase.attackSpeed += effect.value * multiplier;
                break;
            case StatusEffectType.DebuffImmune:
                IsDebuffImmune = apply;
                break;

            // 이동속도 감소 (value = 0.3 → 30% 감속)
            case StatusEffectType.Slow:
            case StatusEffectType.MoveSpeedDown:
                if (apply)
                    status.moveSpeedMultiplier *= (1f - effect.value);
                else
                    status.moveSpeedMultiplier /= (1f - effect.value);
                break;

            case StatusEffectType.ApUp:
                status.addedInt += effect.value * multiplier;
                break;
            case StatusEffectType.CritRateUp:
                status.addedCritRate += effect.value * multiplier;
                break;
            case StatusEffectType.CritDamageUp:
                status.addedCritDamage += effect.value * multiplier;
                break;
            case StatusEffectType.MaxHpUp:
                status.addedVit += effect.value * multiplier;
                break;
            case StatusEffectType.HpOnHitUp:
                status.hpOnHit += effect.value * multiplier;
                break;
        }
    }

    private bool IsDebuff(StatusEffectType type)
    {
        return type == StatusEffectType.Stun
            || type == StatusEffectType.Slow
            || type == StatusEffectType.AtkDown
            || type == StatusEffectType.MoveSpeedDown
            || type == StatusEffectType.DefDown;
    }
}
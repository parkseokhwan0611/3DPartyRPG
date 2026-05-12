using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

// 몬스터에 붙는 상태이상 처리 컴포넌트
public class StatusEffectHandler : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator anim;
    private float baseSpeed;

    // 현재 활성화된 상태이상 목록
    private List<StatusEffect> activeEffects = new List<StatusEffect>();

    // 디버프 개수 (힐러 스킬 연계용)
    public int DebuffCount { get; private set; } = 0;

    // 상태이상 이벤트
    public System.Action OnDebuffAdded;
    public System.Action OnDebuffRemoved;

    void Awake()
    {
        agent    = GetComponent<NavMeshAgent>();
        anim     = GetComponent<Animator>();
        baseSpeed = agent != null ? agent.speed : 3f;
    }

    // ─────────────────────────────────────────────────────────────────
    // 상태이상 적용
    // ─────────────────────────────────────────────────────────────────

    public void ApplyEffect(StatusEffect effect)
    {
        // 같은 타입의 효과가 있으면 갱신 (덮어씌우기)
        RemoveEffect(effect.effectType);

        activeEffects.Add(effect);
        StartCoroutine(EffectRoutine(effect));

        // 디버프면 카운트 증가
        if (IsDebuff(effect.effectType))
        {
            DebuffCount++;
            OnDebuffAdded?.Invoke();
        }

        ApplyEffectValue(effect, true);
    }

    // ─────────────────────────────────────────────────────────────────
    // 상태이상 제거
    // ─────────────────────────────────────────────────────────────────

    public void RemoveEffect(StatusEffectType type)
    {
        StatusEffect existing = activeEffects.Find(e => e.effectType == type);
        if (existing == null) return;

        activeEffects.Remove(existing);
        ApplyEffectValue(existing, false);

        if (IsDebuff(type))
        {
            DebuffCount = Mathf.Max(0, DebuffCount - 1);
            OnDebuffRemoved?.Invoke();
        }
    }

    // 모든 디버프 제거 (힐러 디버프 제거 스킬용)
    public void RemoveAllDebuffs()
    {
        List<StatusEffect> debuffs = activeEffects.FindAll(e => IsDebuff(e.effectType));
        foreach (var debuff in debuffs)
        {
            StopCoroutine(EffectRoutine(debuff));
            activeEffects.Remove(debuff);
            ApplyEffectValue(debuff, false);
        }
        DebuffCount = 0;
        OnDebuffRemoved?.Invoke();
    }

    public bool HasDebuff(StatusEffectType type)
    {
        return activeEffects.Exists(e => e.effectType == type);
    }

    // ─────────────────────────────────────────────────────────────────
    // 효과 지속 코루틴
    // ─────────────────────────────────────────────────────────────────

    private IEnumerator EffectRoutine(StatusEffect effect)
    {
        yield return new WaitForSeconds(effect.duration);
        RemoveEffect(effect.effectType);
    }

    // ─────────────────────────────────────────────────────────────────
    // 효과 수치 적용/해제
    // ─────────────────────────────────────────────────────────────────

    private void ApplyEffectValue(StatusEffect effect, bool apply)
    {
        float multiplier = apply ? 1f : -1f;

        switch (effect.effectType)
        {
            case StatusEffectType.Stun:
                if (agent != null) agent.isStopped = apply;
                if (anim != null)  anim.SetBool("isWalking", !apply);
                break;

            case StatusEffectType.Slow:
            case StatusEffectType.MoveSpeedDown:
                if (agent != null)
                    agent.speed = apply
                        ? baseSpeed * (1f - effect.value)
                        : baseSpeed;
                break;

            case StatusEffectType.AtkDown:
                // EnemyHp나 MonsterMeleeAttack에서 참조
                var monsterAttack = GetComponent<MonsterMeleeAttack>();
                if (monsterAttack != null)
                    monsterAttack.attackDamage -= monsterAttack.attackDamage * effect.value * multiplier;
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
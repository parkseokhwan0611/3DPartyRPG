using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

public class StatusEffectHandler : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator anim;

    // 원본 수치 저장 (디버프 해제 시 정확히 복구)
    private float originalSpeed     = 0f;
    private float originalAtkDamage = 0f;

    private List<StatusEffect> activeEffects = new List<StatusEffect>();

    public int DebuffCount { get; private set; } = 0;

    public System.Action OnDebuffAdded;
    public System.Action OnDebuffRemoved;
    public System.Action OnStunEnded;

    // 스턴 전용 타이머
    private float stunTimer = 0f;
    private bool  isStunned = false;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        agent         = GetComponent<NavMeshAgent>();
        anim          = GetComponent<Animator>();
        originalSpeed = agent != null ? agent.speed : 3f;
    }

    void Update()
    {
        if (!isStunned) return;

        stunTimer -= Time.deltaTime;
        if (stunTimer <= 0f)
        {
            isStunned = false;
            EndStun();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 상태이상 적용
    // ─────────────────────────────────────────────────────────────────

    public void ApplyEffect(StatusEffect effect)
    {
        if (effect.effectType == StatusEffectType.Stun)
        {
            ApplyStun(effect);
            return;
        }

        CancelEffect(effect.effectType);
        activeEffects.Add(effect);

        if (IsDebuff(effect.effectType))
        {
            DebuffCount++;
            OnDebuffAdded?.Invoke();
        }

        ApplyEffectValue(effect, true);
        effect.routine = StartCoroutine(EffectRoutine(effect));
    }

    // ─────────────────────────────────────────────────────────────────
    // 스턴 전용 로직
    // ─────────────────────────────────────────────────────────────────

    private void ApplyStun(StatusEffect effect)
    {
        if (isStunned)
        {
            stunTimer = Mathf.Max(stunTimer, effect.duration);
            return;
        }

        isStunned = true;
        stunTimer = effect.duration;
        DebuffCount++;
        OnDebuffAdded?.Invoke();

        if (agent != null) agent.isStopped = true;

        if (anim != null)
        {
            anim.SetBool("isWalking", false);
            anim.SetTrigger("isStun");
        }

        var attackBase    = GetComponent<AttackBase>();
        var monsterAttack = GetComponent<MonsterMeleeAttack>();

        if (monsterAttack != null) monsterAttack.ResetAttackState();
        if (attackBase    != null) attackBase.ForceCancelAttack();
    }

    private void EndStun()
    {
        stunTimer   = 0f;
        DebuffCount = Mathf.Max(0, DebuffCount - 1);
        OnDebuffRemoved?.Invoke();

        if (agent != null)
        {
            agent.isStopped = false;
            agent.velocity  = Vector3.zero;
        }

        if (anim != null) anim.ResetTrigger("isStun");

        OnStunEnded?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────
    // 상태이상 확인
    // ─────────────────────────────────────────────────────────────────

    public bool HasDebuff(StatusEffectType type)
    {
        if (type == StatusEffectType.Stun) return isStunned;
        return activeEffects.Exists(e => e.effectType == type);
    }

    // ─────────────────────────────────────────────────────────────────
    // 상태이상 제거
    // ─────────────────────────────────────────────────────────────────

    public void RemoveEffect(StatusEffectType type)
    {
        if (type == StatusEffectType.Stun)
        {
            if (isStunned)
            {
                isStunned = false;
                EndStun();
            }
            return;
        }

        StatusEffect existing = activeEffects.Find(e => e.effectType == type);
        if (existing == null) return;

        if (existing.routine != null)
            StopCoroutine(existing.routine);

        activeEffects.Remove(existing);
        ApplyEffectValue(existing, false);

        if (IsDebuff(type))
        {
            DebuffCount = Mathf.Max(0, DebuffCount - 1);
            OnDebuffRemoved?.Invoke();
        }
    }

    private void CancelEffect(StatusEffectType type)
    {
        StatusEffect existing = activeEffects.Find(e => e.effectType == type);
        if (existing == null) return;

        if (existing.routine != null)
            StopCoroutine(existing.routine);

        activeEffects.Remove(existing);

        // 스턴 외 효과는 수치 복구
        if (type != StatusEffectType.Stun)
            ApplyEffectValue(existing, false);

        if (IsDebuff(type))
            DebuffCount = Mathf.Max(0, DebuffCount - 1);
    }

    public void RemoveAllDebuffs()
    {
        if (isStunned)
        {
            isStunned = false;
            EndStun();
        }

        List<StatusEffect> debuffs = activeEffects.FindAll(e => IsDebuff(e.effectType));
        foreach (var debuff in debuffs)
        {
            if (debuff.routine != null)
                StopCoroutine(debuff.routine);

            activeEffects.Remove(debuff);
            ApplyEffectValue(debuff, false);
        }

        DebuffCount = 0;
        OnDebuffRemoved?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────
    // 코루틴
    // ─────────────────────────────────────────────────────────────────

    private IEnumerator EffectRoutine(StatusEffect effect)
    {
        yield return new WaitForSeconds(effect.duration);

        if (!activeEffects.Contains(effect)) yield break;

        activeEffects.Remove(effect);
        ApplyEffectValue(effect, false);

        if (IsDebuff(effect.effectType))
        {
            DebuffCount = Mathf.Max(0, DebuffCount - 1);
            OnDebuffRemoved?.Invoke();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 효과 수치 적용/해제 — 원본값 저장 방식으로 정확히 복구
    // ─────────────────────────────────────────────────────────────────

    private void ApplyEffectValue(StatusEffect effect, bool apply)
    {
        switch (effect.effectType)
        {
            // ── 이동속도 감소 ──
            case StatusEffectType.Slow:
            case StatusEffectType.MoveSpeedDown:
                if (agent == null) break;
                if (apply)
                {
                    originalSpeed  = agent.speed; // 현재 속도 저장
                    agent.speed    = originalSpeed * (1f - effect.value);
                }
                else
                {
                    agent.speed    = originalSpeed; // 원본 속도로 복구
                    originalSpeed  = agent != null ? agent.speed : 3f;
                }
                break;

            // ── 공격력 감소 ──
            case StatusEffectType.AtkDown:
                var monsterAttack = GetComponent<MonsterMeleeAttack>();
                if (monsterAttack == null) break;
                if (apply)
                {
                    originalAtkDamage          = monsterAttack.attackDamage; // 원본값 저장
                    monsterAttack.attackDamage = originalAtkDamage * (1f - effect.value);
                }
                else
                {
                    monsterAttack.attackDamage = originalAtkDamage; // 원본값으로 정확히 복구
                    originalAtkDamage          = 0f;
                }
                break;

            // ── 방어력 감소 ──
            case StatusEffectType.DefDown:
                // 필요 시 추가
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
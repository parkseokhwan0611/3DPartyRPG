using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

public class StatusEffectHandler : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator anim;
    private float baseSpeed;

    private List<StatusEffect> activeEffects = new List<StatusEffect>();

    public int DebuffCount { get; private set; } = 0;

    public System.Action OnDebuffAdded;
    public System.Action OnDebuffRemoved;
    public System.Action OnStunEnded;

    // 스턴 전용 타이머 — 코루틴 의존 안 함
    private float stunTimer  = 0f;
    private bool  isStunned  = false;

    void Awake()
    {
        agent     = GetComponent<NavMeshAgent>();
        anim      = GetComponent<Animator>();
        baseSpeed = agent != null ? agent.speed : 3f;
    }

    // ─────────────────────────────────────────────────────────────────
    // Update — 스턴 타이머를 Update에서 직접 관리
    // AttackBase/StatusEffectHandler 코루틴 충돌 완전 회피
    // ─────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!isStunned) return;

        stunTimer -= Time.deltaTime;

        if (stunTimer <= 0f)
        {
            // EndStun 전에 isStunned를 먼저 false로 설정
            // 같은 프레임에 Update가 다시 실행되는 것을 방지
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

        // 스턴 외 효과는 기존 코루틴 방식
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
    // 스턴 전용 로직 — Update 타이머 방식
    // ─────────────────────────────────────────────────────────────────

    private void ApplyStun(StatusEffect effect)
    {
        if (isStunned)
        {
            stunTimer = Mathf.Max(stunTimer, effect.duration);
            return;
        }

        // 1. isStunned를 먼저 true로 설정
        //    AttackBase.Update()가 StopAndAttack()으로 진입 못 하게 막음
        isStunned = true;
        stunTimer = effect.duration;
        DebuffCount++;
        OnDebuffAdded?.Invoke();

        // 2. NavMesh 정지
        if (agent != null) agent.isStopped = true;

        // 3. 애니메이션
        if (anim != null)
        {
            anim.SetBool("isWalking", false);
            anim.SetTrigger("isStun");
        }

        // 4. ForceCancelAttack 먼저 (코루틴 정지)
        var attackBase = GetComponent<AttackBase>();
        if (attackBase != null)
            attackBase.ForceCancelAttack();

        // 5. ResetAttackState 나중에 (IsAttacking 초기화)
        var monsterAttack = GetComponent<MonsterMeleeAttack>();
        if (monsterAttack != null)
            monsterAttack.ResetAttackState();
    }

    private void EndStun()
    {
        // isStunned는 Update에서 이미 false로 설정됨
        stunTimer = 0f;
        DebuffCount = Mathf.Max(0, DebuffCount - 1);
        OnDebuffRemoved?.Invoke();

        if (agent != null)
        {
            agent.isStopped = false;
            agent.velocity  = Vector3.zero;
        }

        if (anim != null)
            anim.ResetTrigger("isStun");

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
                    isStunned = false; // 먼저 false
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
        ApplyEffectValue(existing, false);

        if (IsDebuff(type))
            DebuffCount = Mathf.Max(0, DebuffCount - 1);
    }

    public void RemoveAllDebuffs()
    {
        // 스턴 해제
        if (isStunned) EndStun();

        // 나머지 디버프 해제
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
    // 코루틴 (스턴 제외 효과)
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
    // 효과 수치 적용/해제 (스턴 제외)
    // ─────────────────────────────────────────────────────────────────

    private void ApplyEffectValue(StatusEffect effect, bool apply)
    {
        switch (effect.effectType)
        {
            case StatusEffectType.Slow:
            case StatusEffectType.MoveSpeedDown:
                if (agent != null)
                    agent.speed = apply ? baseSpeed * (1f - effect.value) : baseSpeed;
                break;

            case StatusEffectType.AtkDown:
                var monsterAttack = GetComponent<MonsterMeleeAttack>();
                if (monsterAttack != null)
                    monsterAttack.attackDamage -= monsterAttack.attackDamage
                        * effect.value * (apply ? 1f : -1f);
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
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

public class StatusEffectHandler : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator anim;
    private AttackBase attackBase;
    private MonsterMeleeAttack monsterMeleeAttack;
    private MonsterRangedAttack monsterRangedAttack;
    private ISkillCaster skillCaster; // EliteMonsterSkillController / BossMonsterSkillController 공용
    private EnemyHp enemyHp;

    // 원본 수치 저장 (디버프 해제 시 정확히 복구)
    private float baseSpeed           = 0f; // Awake 시점 고정 기준 속도 — 이후 절대 덮어쓰지 않음
    private float moveSpeedMultiplier = 1f; // Slow/MoveSpeedDown 중첩 적용 배율
    private float originalAtkDamage   = 0f;
    private float originalDef         = 0f;

    private List<StatusEffect> activeEffects = new List<StatusEffect>();

    public int DebuffCount { get; private set; } = 0;

    public System.Action OnDebuffAdded;
    public System.Action OnDebuffRemoved;
    public System.Action OnStunEnded;
    public System.Action OnShieldChanged;

    // 스턴 전용 타이머
    private float stunTimer = 0f;
    private bool  isStunned = false;

    // 쉴드 수치 풀 — PartyStatusEffectHandler와 공용 로직인 ShieldPool에 위임 (스택 시 수치는
    // 합연산, 지속시간은 최근에 건 스킬 기준으로 갱신되는 단일 풀 + 단일 만료 타이머)
    private ShieldPool _shield;
    public float CurrentShield => _shield.Current;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        agent         = GetComponent<NavMeshAgent>();
        anim          = GetComponent<Animator>();
        attackBase          = GetComponent<AttackBase>();
        monsterMeleeAttack  = GetComponent<MonsterMeleeAttack>();
        monsterRangedAttack = GetComponent<MonsterRangedAttack>();
        skillCaster          = GetComponent<ISkillCaster>();
        enemyHp       = GetComponent<EnemyHp>();
        baseSpeed     = agent != null ? agent.speed : 3f;

        _shield = new ShieldPool(this);
        _shield.OnChanged += () => OnShieldChanged?.Invoke();
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

        // 독이 이미 걸려 있으면 틱 주기는 그대로 두고 수치·남은 시간만 갱신 — 지우고 새로 걸면
        // 1초 틱 타이머도 처음부터 다시 돌아서, 1초보다 자주 재발동되면 독 데미지가 한 번도 안 들어갔음
        if (effect.effectType == StatusEffectType.Poison && _poison != null && activeEffects.Contains(_poison))
        {
            _poison.value   = effect.value;
            _poison.source  = effect.source;
            _poisonRemaining = Mathf.Max(_poisonRemaining, effect.duration);
            return;
        }

        CancelEffect(effect.effectType);
        activeEffects.Add(effect);
        if (effect.effectType == StatusEffectType.Poison)
        {
            _poison          = effect;
            _poisonRemaining = effect.duration;
        }

        if (IsDebuff(effect.effectType))
        {
            DebuffCount++;
            OnDebuffAdded?.Invoke();
        }

        ApplyEffectValue(effect, true);
        effect.routine = StartCoroutine(effect.effectType == StatusEffectType.Poison
            ? PoisonRoutine(effect)
            : EffectRoutine(effect));
    }

    // 독 — 1초마다 value만큼 마법 데미지. 건 사람을 공격자로 넘겨서 처치 판정·데미지 보너스가 그 파티원 기준으로 적용됨.
    // 다시 걸면 ApplyEffect가 수치와 남은 시간만 갱신 (중첩 없음, 틱 주기 유지)
    private StatusEffect _poison;
    private float        _poisonRemaining;
    private static readonly WaitForSeconds PoisonTick = new WaitForSeconds(1f);

    private IEnumerator PoisonRoutine(StatusEffect effect)
    {
        while (_poisonRemaining > 0f)
        {
            yield return PoisonTick;
            _poisonRemaining -= 1f;
            if (enemyHp == null || enemyHp.isDead) break;
            enemyHp.TakeMagicDamage(effect.value, effect.source);
        }

        if (_poison == effect) _poison = null;
        if (!activeEffects.Contains(effect)) yield break;

        activeEffects.Remove(effect);
        if (IsDebuff(effect.effectType))
        {
            DebuffCount = Mathf.Max(0, DebuffCount - 1);
            OnDebuffRemoved?.Invoke();
        }
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

        if (monsterMeleeAttack  != null) monsterMeleeAttack.ResetAttackState();
        if (monsterRangedAttack != null) monsterRangedAttack.ResetAttackState();
        if (attackBase    != null) attackBase.ForceCancelAttack();
        skillCaster?.ForceCancelSkill();
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
    // 쉴드
    // ─────────────────────────────────────────────────────────────────

    public void ApplyShield(float amount, float duration, GameObject source) => _shield.Apply(amount, duration);

    // 데미지 적용 전 쉴드로 먼저 흡수 — EnemyHp.ApplyDamage에서 방어력 경감 이후 호출
    public float AbsorbDamage(float damage) => _shield.Absorb(damage);

    // ─────────────────────────────────────────────────────────────────
    // 상태이상 확인
    // ─────────────────────────────────────────────────────────────────

    public bool HasDebuff(StatusEffectType type)
    {
        if (type == StatusEffectType.Stun) return isStunned;
        return FindEffect(type) != null;
    }

    // 람다(클로저) 할당 없이 타입으로 활성 효과 검색 — HasDebuff는 몬스터 Update에서 자주 불림
    private StatusEffect FindEffect(StatusEffectType type)
    {
        for (int i = 0; i < activeEffects.Count; i++)
            if (activeEffects[i].effectType == type) return activeEffects[i];
        return null;
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

        StatusEffect existing = FindEffect(type);
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
        StatusEffect existing = FindEffect(type);
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
            // Slow/MoveSpeedDown이 중첩돼도 baseSpeed(고정 기준값)는 절대 덮어쓰지 않고,
            // moveSpeedMultiplier에 독립적으로 곱/나누기만 하여 기준값 오염을 방지
            case StatusEffectType.Slow:
            case StatusEffectType.MoveSpeedDown:
                if (agent == null) break;
                float safeValue = Mathf.Clamp(effect.value, 0f, 0.99f); // 100% 감속 시 나누기 0 방지
                if (apply)
                    moveSpeedMultiplier *= (1f - safeValue);
                else
                    moveSpeedMultiplier /= (1f - safeValue);
                agent.speed = baseSpeed * moveSpeedMultiplier;
                break;

            // ── 공격력 감소 ──
            // 100% 감소 시 공격력이 음수로 뒤집히지 않도록 0.99로 클램프 (Slow/MoveSpeedDown과 동일한 이유)
            case StatusEffectType.AtkDown:
                if (attackBase == null) break;
                if (apply)
                {
                    originalAtkDamage       = attackBase.attackDamage; // 원본값 저장
                    float safeAtkValue      = Mathf.Clamp(effect.value, 0f, 0.99f);
                    attackBase.attackDamage = originalAtkDamage * (1f - safeAtkValue);
                }
                else
                {
                    attackBase.attackDamage = originalAtkDamage; // 원본값으로 정확히 복구
                    originalAtkDamage       = 0f;
                }
                break;

            // ── 방어력 감소 ──
            case StatusEffectType.DefDown:
                if (enemyHp == null) break;
                if (apply)
                {
                    originalDef      = enemyHp.def; // 원본값 저장
                    float safeDefValue = Mathf.Clamp(effect.value, 0f, 0.99f);
                    enemyHp.def      = originalDef * (1f - safeDefValue);
                }
                else
                {
                    enemyHp.def = originalDef; // 원본값으로 정확히 복구
                    originalDef = 0f;
                }
                break;
        }
    }

    private bool IsDebuff(StatusEffectType type)
    {
        return type == StatusEffectType.Stun
            || type == StatusEffectType.Slow
            || type == StatusEffectType.AtkDown
            || type == StatusEffectType.MoveSpeedDown
            || type == StatusEffectType.DefDown
            || type == StatusEffectType.Poison;
    }
}
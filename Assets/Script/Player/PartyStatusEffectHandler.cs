using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// 파티원에 붙는 버프/상태이상 처리 컴포넌트
public class PartyStatusEffectHandler : MonoBehaviour
{
    private CharacterStat myStat;
    private AttackBase attackBase;
    private NavMeshAgent agent;
    private Animator anim;
    private SkillManager skillManager;
    private PartyMemberScript partyMember;

    // 쉴드 수치
    public float CurrentShield { get; private set; } = 0f;

    // 디버프 면역 여부 — 참조 카운트 방식 (겹쳐 걸린 면역 버프 중 하나가 먼저 만료돼도
    // 다른 면역 버프가 남아있으면 계속 면역 유지)
    private int debuffImmuneCount = 0;
    public bool IsDebuffImmune => debuffImmuneCount > 0;

    // 버프 이벤트 (UI 갱신용)
    public System.Action OnShieldChanged;
    public System.Action<StatusEffectType, bool> OnBuffChanged;
    public System.Action OnStunEnded;

    // 활성화된 버프 목록
    private List<StatusEffect> activeBuffs = new List<StatusEffect>();

    // 스턴은 activeBuffs 목록이 아니라 Enemy쪽 StatusEffectHandler와 동일하게 전용 타이머로 관리 —
    // 이동/공격을 즉시 멈추고 애니메이션을 재생해야 해서 범용 버프 코루틴 흐름과 분리했다
    private float stunTimer = 0f;
    private bool  isStunned = false;

    // 스턴 걸리기 직전 공격 중이던 대상 — 스턴이 풀리면 자동으로 재개한다
    private Transform _preStunTarget;

    void Awake()
    {
        myStat       = GetComponent<CharacterStat>();
        attackBase   = GetComponent<AttackBase>();
        agent        = GetComponent<NavMeshAgent>();
        anim         = GetComponent<Animator>();
        skillManager = GetComponent<SkillManager>();
        partyMember  = GetComponent<PartyMemberScript>();
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
    // 버프 적용
    // ─────────────────────────────────────────────────────────────────

    public void ApplyBuff(StatusEffect effect)
    {
        // 디버프 면역 중이면 디버프 무시 (스턴 포함)
        if (IsDebuffImmune && IsDebuff(effect.effectType)) return;

        if (effect.effectType == StatusEffectType.Stun)
        {
            ApplyStun(effect);
            return;
        }

        // 같은 타입이라도 교체하지 않고 독립적으로 누적 — 각자 자기 지속시간에 따라 개별 종료
        activeBuffs.Add(effect);
        effect.routine = StartCoroutine(BuffRoutine(effect));
        ApplyBuffValue(effect, true);

        OnBuffChanged?.Invoke(effect.effectType, true);
    }

    // ─────────────────────────────────────────────────────────────────
    // 스턴 전용 로직 — Enemy/StatusEffectHandler.ApplyStun/EndStun과 동일한 패턴
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

        // 스턴 풀린 뒤 자동 재개를 위해 지금 공격 중이던 대상을 기억해둔다 (ForceCancelAttack이
        // currentTarget을 지우기 전에 먼저 읽어야 함)
        _preStunTarget = attackBase != null ? attackBase.currentTarget : null;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity  = Vector3.zero;
        }

        if (anim != null)
        {
            anim.SetBool("isWalking", false);
            anim.SetBool("isStun", true);
        }

        // 하던 공격/스킬을 그 자리에서 즉시 중단 — "지금 하던 동작은 끝까지" 원칙보다 스턴이 우선
        attackBase?.ForceCancelAttack();
        skillManager?.ForceStopCurrentSkill();

        OnBuffChanged?.Invoke(StatusEffectType.Stun, true);
    }

    private void EndStun()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.velocity  = Vector3.zero;
        }

        if (anim != null) anim.SetBool("isStun", false);

        // 스턴 도중 플레이어가 다른 명령(새 타겟 지정 등)을 내리지 않았을 때만 자동 재개 —
        // currentTarget이 여전히 비어있다는 건 아무도 그 사이에 새로 지정하지 않았다는 뜻.
        // 실제 재개 판단(비상 추격 거리 체크 포함)은 PartyMemberScript.TryResumeAttackAfterStun에 위임 —
        // 여기서 바로 SetTargetImmediate를 부르면, 팔로워가 스턴 도중 파티와 너무 멀어진 경우
        // 바로 다음 프레임 emergencyFollowMultiplier 체크가 방금 재개한 공격을 다시 끊어버렸었다
        if (_preStunTarget != null && attackBase != null && attackBase.currentTarget == null)
        {
            if (partyMember != null) partyMember.TryResumeAttackAfterStun(_preStunTarget);
            else                      attackBase.SetTargetImmediate(_preStunTarget); // PartyMemberScript 없는 경우 대비 폴백
        }
        _preStunTarget = null;

        OnBuffChanged?.Invoke(StatusEffectType.Stun, false);
        OnStunEnded?.Invoke();
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
        if (type == StatusEffectType.Stun)
        {
            if (isStunned)
            {
                isStunned = false;
                EndStun();
            }
            return;
        }

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
        if (isStunned)
        {
            isStunned = false;
            EndStun();
        }

        var debuffs = activeBuffs.FindAll(e => IsDebuff(e.effectType));
        foreach (var debuff in debuffs)
            RemoveBuffInstance(debuff);
    }

    public bool HasActiveDebuff()
    {
        return isStunned || activeBuffs.Exists(e => IsDebuff(e.effectType));
    }

    // Enemy용 StatusEffectHandler.HasDebuff와 동일한 시그니처 — AttackBase에서 공용으로 사용
    public bool HasDebuff(StatusEffectType type)
    {
        if (type == StatusEffectType.Stun) return isStunned;
        return activeBuffs.Exists(e => e.effectType == type);
    }

    // 사망 시 호출 — GameObject가 비활성화되면 만료 코루틴이 강제로 죽어 스탯이 복구되지 않으므로,
    // 비활성화되기 전에 활성 버프/디버프를 전부 즉시 원상복구한다.
    public void ClearAllOnDeath()
    {
        // Die()가 곧이어 agent 비활성화·사망 애니메이션을 직접 처리하므로 여기서는
        // 플래그만 정리 (agent.isStopped 등을 건드려 사망 연출과 충돌시키지 않음)
        if (isStunned)
        {
            isStunned = false;
            stunTimer = 0f;
            _preStunTarget = null;
            if (anim != null) anim.SetBool("isStun", false);
        }

        var buffs = new List<StatusEffect>(activeBuffs);
        foreach (var effect in buffs)
            RemoveBuffInstance(effect);

        CurrentShield = 0f;
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
                debuffImmuneCount = Mathf.Max(0, debuffImmuneCount + (apply ? 1 : -1));
                break;

            // 이동속도 감소 (value = 0.3 → 30% 감속)
            // value가 1(100% 감속)이면 해제 시 0으로 나누게 되어 버그가 나므로 0.99로 클램프
            case StatusEffectType.Slow:
            case StatusEffectType.MoveSpeedDown:
                float safeValue = Mathf.Clamp(effect.value, 0f, 0.99f);
                if (apply)
                    status.moveSpeedMultiplier *= (1f - safeValue);
                else
                    status.moveSpeedMultiplier /= (1f - safeValue);
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
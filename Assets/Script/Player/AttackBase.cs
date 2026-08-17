using UnityEngine;
using UnityEngine.AI;
using System;

public abstract class AttackBase : MonoBehaviour
{
    // ─────────────────────────────────────────
    // 이벤트
    // ─────────────────────────────────────────
    public event Action OnAttackStarted;
    public event Action OnAttackEnded;
    public event Action OnAttackExecuted;

    // ─────────────────────────────────────────
    // 공격 설정
    // ─────────────────────────────────────────
    [Header("공격 설정")]
    public float attackDamage   = 10f;
    public float attackRange    = 2.0f;
    public float attackSpeed    = 1.0f;
    public float attackDuration = 1.0f;
    protected float attackCooldown  = 0f;
    public bool IsCastingSkill { get; set; } = false;
    public bool IsAttackAnimPlaying { get; protected set; } = false;
    // 몬스터 기본 공격(Melee/Ranged/Grenade)이 윈드업~후딜레이까지 진행 중인지 —
    // 각자 로컬 프로퍼티로 따로 갖고 있던 걸 공통으로 올려서, EliteMonsterSkillController처럼
    // AttackBase 타입만 들고 있는 쪽에서도 "지금 기본 공격 중이라 스킬을 끼워넣으면 안 된다"를 판단 가능
    public bool IsAttacking { get; protected set; } = false;
    private float firstAttackDelay  = 0f;

    // 타겟이 일정 거리 이상 움직였을 때만 재경로 — 매 프레임 SetDestination 재호출로 인한 회피 벡터 재계산/떨림 방지
    private Vector3 _lastChaseDestination = Vector3.positiveInfinity;
    private const float ChaseDestThreshold = 0.3f;

    // ─────────────────────────────────────────
    // 참조 컴포넌트
    // ─────────────────────────────────────────
    [Header("참조 컴포넌트")]
    protected NavMeshAgent agent;
    protected Animator anim;
    public Transform currentTarget;
    protected EnemyHp targetHealth;
    protected PartyMemberScript targetPartyMember; // 타겟이 플레이어 파티원일 때(몬스터→플레이어) 사망 감지용
    public LayerMask enemyLayer;
    protected StatusEffectHandler statusHandler; // Enemy 전용
    protected PartyStatusEffectHandler partyStatusHandler; // Player 전용
    private Transform _aimPoint; // currentTarget의 AimTarget 자식 (타겟 변경 시만 갱신)
    private Transform _pendingTarget;   // IsAttacking 도중 요청된 타겟 변경 — 공격이 끝나면 반영
    private bool      _hasPendingTarget;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    protected virtual void Start()
    {
        agent              = GetComponent<NavMeshAgent>();
        anim               = GetComponent<Animator>();
        statusHandler      = GetComponent<StatusEffectHandler>();
        partyStatusHandler = GetComponent<PartyStatusEffectHandler>();
    }

    protected virtual void Update()
    {
        if (attackCooldown > 0) attackCooldown -= Time.deltaTime;
        if (firstAttackDelay > 0)
        {
            firstAttackDelay -= Time.deltaTime;
            // 딜레이 중에도 타겟이 사라지거나 이미 죽었으면 즉시 해제
            if (currentTarget == null || IsCurrentTargetDead())
            {
                firstAttackDelay = 0f;
                ClearTarget();
            }
            return;
        }

        if (currentTarget == null) return;

        if (IsCurrentTargetDead())
        {
            ClearTarget();
            return;
        }

        LookAtTarget();
        HandleAttackLogic();
    }

    // targetHealth(EnemyHp, 몬스터 타겟)와 targetPartyMember(PartyMemberScript, 플레이어 타겟) 양쪽을
    // 모두 커버 — 이게 없으면 몬스터가 플레이어를 타겟할 때는 사망 감지가 전혀 안 돼서, 공격 쿨다운이
    // TargetingLogic의 재판정 주기보다 빠르게 돌 경우 죽은 캐릭터를 계속 때리는 상황이 생길 수 있다
    private bool IsCurrentTargetDead()
    {
        if (targetHealth != null && targetHealth.isDead) return true;
        if (targetPartyMember != null && targetPartyMember.CurrentState == PartyMemberScript.MemberState.Dead) return true;
        return false;
    }

    // ─────────────────────────────────────────────────────────────────
    // 공격 흐름
    // ─────────────────────────────────────────────────────────────────

    protected virtual void HandleAttackLogic()
    {
        if (!agent.enabled) return; // 사망으로 agent 비활성화된 경우 차단
        if (IsCastingSkill) return;
        // 공격 모션(IsAttackAnimPlaying) 재생 중에는 사거리를 벗어나도 추격을 시작하지 않는다 —
        // 모션이 끝날 때까지 제자리에서 공격을 마치고 나서야 재추격 여부를 다시 판단한다.
        // 이 가드 덕분에 "추격 중"과 "공격 모션 재생 중"이 겹치는 프레임이 아예 없어져서,
        // 이동 중에 공격이 끼어들거나 공격 중에 이동이 끼어드는 상황도 함께 차단된다
        if (IsAttackAnimPlaying) return;
        // 스턴 중에는 추격도 공격 시작도 하지 않는다 — 스턴이 걸리는 순간
        // StatusEffectHandler/PartyStatusEffectHandler가 이미 ForceCancelAttack으로 하던 공격을
        // 끊어놨으므로, 여기서는 스턴이 풀릴 때까지 새로 추격/공격을 시작하지 않게만 막으면 된다
        if ((statusHandler != null && statusHandler.HasDebuff(StatusEffectType.Stun))
            || (partyStatusHandler != null && partyStatusHandler.HasDebuff(StatusEffectType.Stun)))
            return;

        float distance         = Vector3.Distance(transform.position, currentTarget.position);
        agent.stoppingDistance = 0.1f;

        if (distance <= attackRange)
        {
            StopAndAttack();
        }
        else
        {
            Vector3 dest = currentTarget.position;
            bool destMoved = (_lastChaseDestination - dest).sqrMagnitude > ChaseDestThreshold * ChaseDestThreshold;

            // 목적지가 그대로여도 경로가 없으면(스킬 시전 등으로 ResetPath된 경우) 반드시 재경로
            if (destMoved || !agent.hasPath)
            {
                agent.SetDestination(dest);
                _lastChaseDestination = dest;
            }
        }
    }

    protected virtual void StopAndAttack()
    {
        if (statusHandler != null && statusHandler.HasDebuff(StatusEffectType.Stun)) return;
        if (partyStatusHandler != null && partyStatusHandler.HasDebuff(StatusEffectType.Stun)) return;
        if (IsCastingSkill) return;

        agent.ResetPath();
        agent.velocity = Vector3.zero;

        if (attackCooldown <= 0)
        {
            ExecuteAttack();
            attackCooldown = attackDuration / attackSpeed;
            OnAttackExecuted?.Invoke();
        }
    }

    protected virtual void ExecuteAttack() { }

    // 몬스터 기본 공격(Melee/Ranged/Grenade)이 치명타를 굴릴 때 공용으로 사용 — 플레이어 쪽은
    // CharacterStat.TotalCritRate/TotalCritDamage를 직접 쓰므로 이 헬퍼를 쓰지 않는다
    protected float RollCritDamage(float baseDamage, float critChance, float critDamageMultiplier, out bool isCrit)
    {
        isCrit = UnityEngine.Random.value < critChance;
        return isCrit ? baseDamage * critDamageMultiplier : baseDamage;
    }

    // ─────────────────────────────────────────────────────────────────
    // 타겟 관리
    // ─────────────────────────────────────────────────────────────────

    // 플레이어용 — firstAttackDelay 적용
    public void SetTarget(Transform target)
    {
        if (currentTarget == target) return;

        StopAttackCoroutine();
        attackCooldown = 0f;

        if (anim != null)
            anim.ResetTrigger("doNormalAttack");

        currentTarget = target;
        CacheAimPoint(currentTarget);
        _lastChaseDestination = Vector3.positiveInfinity; // 새 타겟은 즉시 재경로

        if (currentTarget != null)
        {
            targetHealth       = currentTarget.GetComponent<EnemyHp>();
            targetPartyMember  = currentTarget.GetComponent<PartyMemberScript>();
            OnAttackStarted?.Invoke();
            firstAttackDelay = 0.15f;
        }
        else
        {
            targetHealth       = null;
            targetPartyMember  = null;
            firstAttackDelay = 0f;
            OnAttackEnded?.Invoke();
        }
    }

    // 몬스터용 — firstAttackDelay 없이 즉시 공격
    public void SetTargetImmediate(Transform target)
    {
        if (currentTarget == target) { _hasPendingTarget = false; return; }

        // 공격 윈드업/후딜 도중(IsAttacking)에는 지금 당장 바꾸지 않고 대기시켰다가, 이번 공격이
        // 자연스럽게 끝나는 시점(RaiseAttackEnded)에 반영한다. 예전엔 여기서 바로 코루틴을 취소해서
        // 애니메이션이 중간에 끊기는 문제가 있었고, 반대로 아예 막아버리면(TargetingLogic 자체를
        // 스킵) 더 가까운 대상이 나타나도 원래 타겟만 계속 노리는 문제가 있었음 — "지금 하던 공격은
        // 끝까지, 그다음엔 항상 최신(가장 가까운) 타겟으로"를 동시에 만족시키는 절충안
        if (IsAttacking)
        {
            _pendingTarget    = target;
            _hasPendingTarget = true;
            return;
        }

        // 스킬 시전 중이 아닌 상태에서 즉시 적용 — 애니메이션/코루틴이 실제로 진행 중이 아니므로
        // 여기서 취소해도 끊길 게 없음
        StopAttackCoroutine();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = false;

        attackCooldown   = 0f;
        firstAttackDelay = 0f;

        if (anim != null)
            anim.ResetTrigger("doNormalAttack");

        currentTarget = target;
        CacheAimPoint(currentTarget);
        _lastChaseDestination = Vector3.positiveInfinity; // 새 타겟은 즉시 재경로

        if (currentTarget != null)
        {
            targetHealth      = currentTarget.GetComponent<EnemyHp>();
            targetPartyMember = currentTarget.GetComponent<PartyMemberScript>();
            OnAttackStarted?.Invoke();
        }
        else
        {
            targetHealth      = null;
            targetPartyMember = null;
            OnAttackEnded?.Invoke();
        }
    }

    protected void ClearTarget()
    {
        currentTarget      = null;
        targetHealth       = null;
        targetPartyMember  = null;
        OnAttackEnded?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────────────────────────

    protected Vector3 TargetPosition
    {
        get
        {
            if (currentTarget == null) return transform.position + transform.forward;
            return _aimPoint != null ? _aimPoint.position : currentTarget.position;
        }
    }

    private void CacheAimPoint(Transform target)
    {
        _aimPoint = target != null ? target.Find("AimTarget") : null;
    }

    protected void LookAtTarget()
    {
        if (currentTarget == null) return;

        Vector3 direction = (TargetPosition - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 10f);
        }
    }

    public abstract void OnHit();

    public void ForceCancelAttack()
    {
        StopAttackCoroutine();

        if (anim != null)
            anim.ResetTrigger("doNormalAttack");

        if (agent != null && agent.enabled)
        {
            agent.ResetPath();
            agent.velocity         = Vector3.zero;
            agent.stoppingDistance = 0.1f;
        }

        attackCooldown   = 0f;
        firstAttackDelay = 0f;

        // 타겟도 같이 정리 — 안 하면 사망 후에도 LookAtTarget이 시체를 계속 회전시킴
        currentTarget      = null;
        targetHealth       = null;
        targetPartyMember  = null;
    }

    protected virtual void StopAttackCoroutine()
    {
        // 기본 구현은 비워둠 — 자식 클래스에서 override
    }

    public void ResetAttackCooldown()   => attackCooldown   = 0f;
    public void ResetFirstAttackDelay(float delay = 0.15f) => firstAttackDelay = delay;

    protected void RaiseAttackEnded()
    {
        OnAttackEnded?.Invoke();

        // 공격 도중 대기 중이던 타겟 변경(더 가까운 대상 발견 등)이 있으면 지금 반영 —
        // 이 시점엔 IsAttacking이 이미 false라 SetTargetImmediate가 즉시 적용 경로를 탄다
        if (_hasPendingTarget)
        {
            _hasPendingTarget = false;
            SetTargetImmediate(_pendingTarget);
        }
    }

    public void ForceResetTarget()
    {
        currentTarget      = null;
        targetHealth       = null;
        targetPartyMember  = null;
        firstAttackDelay   = 0f;
    }

    public void CancelCurrentAttack()
    {
        StopAttackCoroutine();
        attackCooldown = 0f;
    }
}
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
    public LayerMask enemyLayer;
    protected StatusEffectHandler statusHandler; // Enemy 전용
    protected PartyStatusEffectHandler partyStatusHandler; // Player 전용
    private Transform _aimPoint; // currentTarget의 AimTarget 자식 (타겟 변경 시만 갱신)

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
            if (currentTarget == null || (targetHealth != null && targetHealth.isDead))
            {
                firstAttackDelay = 0f;
                ClearTarget();
            }
            return;
        }

        if (currentTarget == null) return;

        if (targetHealth != null && targetHealth.isDead)
        {
            ClearTarget();
            return;
        }

        LookAtTarget();
        HandleAttackLogic();
    }

    // ─────────────────────────────────────────────────────────────────
    // 공격 흐름
    // ─────────────────────────────────────────────────────────────────

    protected virtual void HandleAttackLogic()
    {
        if (!agent.enabled) return; // 사망으로 agent 비활성화된 경우 차단
        if (IsCastingSkill) return;

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
            targetHealth     = currentTarget.GetComponent<EnemyHp>();
            OnAttackStarted?.Invoke();
            firstAttackDelay = 0.15f;
        }
        else
        {
            targetHealth     = null;
            firstAttackDelay = 0f;
            OnAttackEnded?.Invoke();
        }
    }

    // 몬스터용 — firstAttackDelay 없이 즉시 공격
    public void SetTargetImmediate(Transform target)
    {
        if (currentTarget == target) return;

        attackCooldown   = 0f;
        firstAttackDelay = 0f;

        if (anim != null)
            anim.ResetTrigger("doNormalAttack");

        currentTarget = target;
        CacheAimPoint(currentTarget);
        _lastChaseDestination = Vector3.positiveInfinity; // 새 타겟은 즉시 재경로

        if (currentTarget != null)
        {
            targetHealth = currentTarget.GetComponent<EnemyHp>();
            OnAttackStarted?.Invoke();
        }
        else
        {
            targetHealth = null;
            OnAttackEnded?.Invoke();
        }
    }

    protected void ClearTarget()
    {
        currentTarget = null;
        targetHealth  = null;
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
        currentTarget = null;
        targetHealth  = null;
    }

    protected virtual void StopAttackCoroutine()
    {
        // 기본 구현은 비워둠 — 자식 클래스에서 override
    }

    public void ResetAttackCooldown()   => attackCooldown   = 0f;
    public void ResetFirstAttackDelay(float delay = 0.15f) => firstAttackDelay = delay;

    protected void RaiseAttackEnded()   => OnAttackEnded?.Invoke();

    public void ForceResetTarget()
    {
        currentTarget    = null;
        targetHealth     = null;
        firstAttackDelay = 0f;
    }

    public void CancelCurrentAttack()
    {
        StopAttackCoroutine();
        attackCooldown = 0f;
    }
}
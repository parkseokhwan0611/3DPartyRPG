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
    private float firstAttackDelay  = 0f;

    // ─────────────────────────────────────────
    // 참조 컴포넌트
    // ─────────────────────────────────────────
    [Header("참조 컴포넌트")]
    protected NavMeshAgent agent;
    protected Animator anim;
    public Transform currentTarget;
    protected EnemyHp targetHealth;
    public LayerMask enemyLayer;
    protected StatusEffectHandler statusHandler;
    private Transform _aimPoint; // currentTarget의 AimTarget 자식 (타겟 변경 시만 갱신)

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    protected virtual void Start()
    {
        agent         = GetComponent<NavMeshAgent>();
        anim          = GetComponent<Animator>();
        statusHandler = GetComponent<StatusEffectHandler>();
    }

    protected virtual void Update()
    {
        if (attackCooldown > 0) attackCooldown -= Time.deltaTime;
        if (firstAttackDelay > 0)
        {
            firstAttackDelay -= Time.deltaTime;
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
        agent.stoppingDistance = attackRange;

        if (distance <= attackRange)
        {
            StopAndAttack();
        }
        else
        {
            agent.SetDestination(currentTarget.position);
        }
    }

    protected virtual void StopAndAttack()
    {
        if (statusHandler != null && statusHandler.HasDebuff(StatusEffectType.Stun)) return;
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
using UnityEngine;
using UnityEngine.AI;
using System; // Action을 사용하기 위해 필요

public abstract class AttackBase : MonoBehaviour
{
    // ─────────────────────────────────────────
    // 이벤트 (PartyMemberScript가 구독)
    // ─────────────────────────────────────────
    public event Action OnAttackStarted; // 공격 시작 시 발생
    public event Action OnAttackEnded;   // 공격 종료(타겟 해제) 시 발생

    // ─────────────────────────────────────────
    // 공격 설정
    // ─────────────────────────────────────────
    [Header("공격 설정")]
    public float attackDamage  = 10f;
    public float attackRange   = 2.0f;
    public float attackSpeed   = 1.0f;
    public float attackDuration = 1.0f;
    protected float attackCooldown = 0f;

    // ─────────────────────────────────────────
    // 참조 컴포넌트
    // ─────────────────────────────────────────
    [Header("참조 컴포넌트")]
    protected NavMeshAgent agent;
    protected Animator anim;
    public Transform currentTarget;
    protected EnemyHp targetHealth;
    public LayerMask enemyLayer;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    protected virtual void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim  = GetComponent<Animator>();
    }

    protected virtual void Update()
    {
        if (attackCooldown > 0) attackCooldown -= Time.deltaTime;

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
        float distance = Vector3.Distance(transform.position, currentTarget.position);
        agent.stoppingDistance = attackRange;

        if (distance <= attackRange)
        {
            if (anim != null) anim.SetBool("isWalking", false);
            StopAndAttack();
        }
        else
        {
            agent.SetDestination(currentTarget.position);
            if (anim != null) anim.SetBool("isWalking", true);
        }
    }

    protected virtual void StopAndAttack()
    {
        agent.ResetPath();
        if (anim != null) anim.SetBool("isWalking", false);

        LookAtTarget();

        if (attackCooldown <= 0)
        {
            ExecuteAttack();
            attackCooldown = attackDuration / attackSpeed;
        }
    }

    protected virtual void ExecuteAttack() { }

    // ─────────────────────────────────────────────────────────────────
    // 타겟 관리
    // ─────────────────────────────────────────────────────────────────

    public void SetTarget(Transform target)
    {
        if (currentTarget == target) return;

        currentTarget = target;

        if (currentTarget != null)
        {
            targetHealth = currentTarget.GetComponent<EnemyHp>();
            OnAttackStarted?.Invoke(); // ← 공격 시작 알림
        }
        else
        {
            targetHealth = null;
            OnAttackEnded?.Invoke();   // ← 공격 종료 알림 (타겟 해제)
        }
    }

    protected void ClearTarget()
    {
        currentTarget = null;
        targetHealth  = null;
        OnAttackEnded?.Invoke();       // ← 타겟이 죽어서 종료될 때도 알림
    }

    // ─────────────────────────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────────────────────────

    protected Vector3 TargetPosition
    {
        get
        {
            if (currentTarget == null) return transform.position + transform.forward;
            Transform aimPoint = currentTarget.Find("AimTarget");
            return aimPoint != null ? aimPoint.position : currentTarget.position;
        }
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
        StopAllCoroutines();
        ClearTarget(); // ClearTarget이 OnAttackEnded 이벤트도 발생시킴

        if (anim != null)
        {
            anim.ResetTrigger("doNormalAttack");
            anim.SetBool("isWalking", true);
        }

        agent.stoppingDistance = 0.1f;
    }
}
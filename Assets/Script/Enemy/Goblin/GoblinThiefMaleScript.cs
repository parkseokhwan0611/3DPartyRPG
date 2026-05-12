using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class GoblinThiefMaleScript : MonoBehaviour
{
    [Header("# References")]
    public Animator animator;
    public EnemyHp enemyHp;
    public NavMeshAgent navAgent;

    [Header("# Chase Settings")]
    public float chaseDistance = 10f;
    public float targetSwitchThreshold = 1.0f; // 타겟 전환 거리 차이 기준

    [Header("# NavMesh")]
    public float navSpeed = 3f;

    // 타겟팅 갱신 간격 (매 프레임 X → 0.2초마다)
    private const float TargetingInterval = 0.2f;
    private float targetingTimer = 0f;

    // 컴포넌트 캐싱
    private Rigidbody rigid;
    private AttackBase attackModule;
    private MonsterMeleeAttack monsterMeleeAttack;

    // 공격 상태 (이벤트로 관리)
    private bool isAttacking = false;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        animator            = GetComponent<Animator>();
        rigid               = GetComponent<Rigidbody>();
        navAgent            = GetComponent<NavMeshAgent>();
        attackModule        = GetComponent<AttackBase>();
        monsterMeleeAttack  = GetComponent<MonsterMeleeAttack>();

        // AttackBase 이벤트 구독 (내부 상태 직접 참조 제거)
        if (attackModule != null)
        {
            attackModule.OnAttackStarted += () => isAttacking = true;
            attackModule.OnAttackEnded   += () => isAttacking = false;
        }

        // EnemyHp 사망 이벤트 구독
        if (enemyHp != null)
            enemyHp.OnDied += HandleDeath;

    }

    void Start()
    {
        navAgent.speed = navSpeed;
    }

    void OnDestroy()
    {
        // 구독 해제 (메모리 누수 방지)
        if (attackModule != null)
        {
            attackModule.OnAttackStarted -= () => isAttacking = true;
            attackModule.OnAttackEnded   -= () => isAttacking = false;
        }

        if (enemyHp != null)
            enemyHp.OnDied -= HandleDeath;
    }

    void Update()
    {
        if (GameManager.instance == null || !GameManager.instance.isLive) return;
        if (enemyHp == null || enemyHp.isDead) return;

        // 타겟팅은 매 프레임이 아닌 일정 간격으로만 실행
        targetingTimer += Time.deltaTime;
        if (targetingTimer >= TargetingInterval)
        {
            targetingTimer = 0f;
            TargetingLogic();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 타겟팅 로직
    // ─────────────────────────────────────────────────────────────────
    void TargetingLogic()
    {
        if (PartyManager.instance == null || PartyManager.instance.partyMembers.Count == 0)
        {
            attackModule.SetTarget(null);
            return;
        }

        Transform nearestTarget = GetNearestPartyMember();

        if (nearestTarget == null)
        {
            attackModule.SetTarget(null);
            return;
        }

        float distToTarget = Vector3.Distance(transform.position, nearestTarget.position);

        if (distToTarget <= chaseDistance)
        {
            // 공격 중이더라도 현재 타겟보다 더 가까운 타겟이 있으면 전환
            if (!isAttacking)
            {
                attackModule.SetTarget(nearestTarget);
                navAgent.isStopped = false;
            }
            else if (attackModule.currentTarget != nearestTarget)
            {
                // 공격 중에도 더 가까운 타겟으로 전환
                // 단 현재 타겟과의 거리차가 일정 이상일 때만 전환 (너무 잦은 전환 방지)
                float currentTargetDist = attackModule.currentTarget != null
                    ? Vector3.Distance(transform.position, attackModule.currentTarget.position)
                    : Mathf.Infinity;

                if (distToTarget < currentTargetDist - 1.0f) // 1미터 이상 가까울 때만 전환
                {
                    attackModule.SetTarget(nearestTarget);
                    navAgent.isStopped = false;
                }
            }
        }
        else if (distToTarget > chaseDistance)
        {
            attackModule.SetTarget(null);
        }
    }

    Transform GetNearestPartyMember()
    {
        Transform closest  = null;
        float minDist      = Mathf.Infinity;
        Vector3 currentPos = transform.position;

        foreach (PartyMemberScript member in PartyManager.instance.partyMembers)
        {
            if (member == null) continue;

            // 상태머신으로 사망 여부 체크
            if (member.CurrentState == PartyMemberScript.MemberState.Dead) continue;

            float dist = Vector3.Distance(currentPos, member.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = member.transform;
            }
        }

        return closest;
    }

    // ─────────────────────────────────────────────────────────────────
    // 사망 처리 (EnemyHp.OnDied 이벤트 콜백)
    // ─────────────────────────────────────────────────────────────────

    private void HandleDeath()
    {
        attackModule.SetTarget(null);
        isAttacking = false;

        if (navAgent.enabled)
        {
            navAgent.isStopped = true;
            navAgent.velocity  = Vector3.zero;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 물리
    // ─────────────────────────────────────────────────────────────────

    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            rigid.velocity = Vector3.zero;
    }
}
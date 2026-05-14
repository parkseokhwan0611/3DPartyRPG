using UnityEngine;
using UnityEngine.AI;

public class GoblinThiefMaleScript : MonoBehaviour
{
    [Header("# References")]
    public Animator animator;
    public EnemyHp enemyHp;
    public NavMeshAgent navAgent;

    [Header("# Chase Settings")]
    public float chaseDistance          = 10f;
    public float targetSwitchThreshold  = 1.0f;

    [Header("# NavMesh")]
    public float navSpeed = 3f;

    private const float TargetingInterval = 0.2f;
    private float targetingTimer          = 0f;

    private Rigidbody rigid;
    private AttackBase attackModule;
    private MonsterMeleeAttack monsterMeleeAttack;
    private StatusEffectHandler statusHandler;

    private bool isAttacking = false;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        animator           = GetComponent<Animator>();
        rigid              = GetComponent<Rigidbody>();
        navAgent           = GetComponent<NavMeshAgent>();
        attackModule       = GetComponent<AttackBase>();
        monsterMeleeAttack = GetComponent<MonsterMeleeAttack>();
        statusHandler      = GetComponent<StatusEffectHandler>();

        if (statusHandler != null)
            statusHandler.OnStunEnded += HandleStunEnded;

        if (attackModule != null)
        {
            attackModule.OnAttackStarted += () => isAttacking = true;
            attackModule.OnAttackEnded   += () => isAttacking = false;
        }

        if (enemyHp != null)
            enemyHp.OnDied += HandleDeath;
    }

    void Start()
    {
        navAgent.speed = navSpeed;
    }

    void OnDestroy()
    {
        if (statusHandler != null)
            statusHandler.OnStunEnded -= HandleStunEnded;

        if (enemyHp != null)
            enemyHp.OnDied -= HandleDeath;
    }

    void Update()
    {
        if (GameManager.instance == null || !GameManager.instance.isLive) return;
        if (enemyHp == null || enemyHp.isDead) return;

        // 스턴 중이면 타겟팅 중단
        if (statusHandler != null && statusHandler.HasDebuff(StatusEffectType.Stun))
            return;

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
            attackModule.SetTargetImmediate(null);
            return;
        }

        Transform nearestTarget = GetNearestPartyMember();

        if (nearestTarget == null)
        {
            attackModule.SetTargetImmediate(null);
            return;
        }

        float distToTarget = Vector3.Distance(transform.position, nearestTarget.position);

        if (distToTarget <= chaseDistance)
        {
            if (!isAttacking)
            {
                // 같은 타겟이면 SetTarget 호출 안 함
                if (attackModule.currentTarget != nearestTarget)
                {
                    attackModule.SetTargetImmediate(nearestTarget);
                    navAgent.isStopped = false;
                }
            }
            else if (attackModule.currentTarget != nearestTarget)
            {
                float currentTargetDist = attackModule.currentTarget != null
                    ? Vector3.Distance(transform.position, attackModule.currentTarget.position)
                    : Mathf.Infinity;

                if (distToTarget < currentTargetDist - targetSwitchThreshold)
                {
                    attackModule.SetTargetImmediate(nearestTarget);
                    navAgent.isStopped = false;
                }
            }
        }
        else
        {
            attackModule.SetTargetImmediate(null);
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
    // 스턴 해제
    // ─────────────────────────────────────────────────────────────────

    private void HandleStunEnded()
    {
        if (enemyHp == null || enemyHp.isDead) return;

        if (navAgent.enabled)
        {
            navAgent.isStopped = false;
            navAgent.velocity  = Vector3.zero;
        }

        isAttacking    = false;
        targetingTimer = TargetingInterval;

        // ForceResetTarget + SetTargetImmediate 대신
        // attackCooldown만 초기화하고 타겟팅 로직에 맡김
        attackModule.ResetAttackCooldown();

        // 타겟이 있으면 그대로 유지, 없으면 새로 찾기
        if (attackModule.currentTarget == null)
            TargetingLogic();
    }

    // ─────────────────────────────────────────────────────────────────
    // 사망 처리
    // ─────────────────────────────────────────────────────────────────

    private void HandleDeath()
    {
        attackModule.SetTargetImmediate(null);
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
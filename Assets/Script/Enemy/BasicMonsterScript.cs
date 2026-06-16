using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class BasicMonsterScript : MonoBehaviour
{
    [Header("# References")]
    public Animator animator;
    public EnemyHp enemyHp;
    public NavMeshAgent navAgent;

    [Header("# Chase Settings")]
    public float chaseDistance         = 10f;
    public float targetSwitchThreshold = 1.0f;

    [Header("# NavMesh")]
    public float navSpeed = 3f;

    [Header("# Aggro Settings")]
    public float aggroDecayRate = 5f;
    public float aggroThreshold = 10f;

    private const float TargetingInterval = 0.2f;
    private float targetingTimer          = 0f;

    private Rigidbody rigid;
    private AttackBase attackModule;
    private StatusEffectHandler statusHandler;

    private bool isAttacking = false;

    // ─────────────────────────────────────────────────────────────────
    // 어그로 테이블
    // ─────────────────────────────────────────────────────────────────
    private Dictionary<Transform, float> aggroTable = new Dictionary<Transform, float>();
    private Transform aggroTarget = null;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        animator      = GetComponent<Animator>();
        rigid         = GetComponent<Rigidbody>();
        navAgent      = GetComponent<NavMeshAgent>();
        attackModule  = GetComponent<AttackBase>();
        statusHandler = GetComponent<StatusEffectHandler>();

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
        navAgent.speed           = navSpeed;
        navAgent.autoBraking     = false;
        navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
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
        if (statusHandler != null && statusHandler.HasDebuff(StatusEffectType.Stun)) return;

        if (animator != null)
            animator.SetBool("isWalking", navAgent.velocity.sqrMagnitude > 0.01f);

        DecayAggro();

        targetingTimer += Time.deltaTime;
        if (targetingTimer >= TargetingInterval)
        {
            targetingTimer = 0f;
            TargetingLogic();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 어그로 시스템
    // ─────────────────────────────────────────────────────────────────

    public void AddAggro(Transform target, float amount)
    {
        if (target == null) return;

        if (aggroTable.ContainsKey(target))
            aggroTable[target] += amount;
        else
            aggroTable[target] = amount;

        RefreshAggroTarget();
    }

    private void RefreshAggroTarget()
    {
        float maxAggro      = aggroThreshold;
        Transform topTarget = null;

        foreach (var entry in aggroTable)
        {
            if (entry.Key == null) continue;

            PartyMemberScript member = entry.Key.GetComponent<PartyMemberScript>();
            if (member != null && member.CurrentState == PartyMemberScript.MemberState.Dead)
                continue;

            if (entry.Value > maxAggro)
            {
                maxAggro  = entry.Value;
                topTarget = entry.Key;
            }
        }

        aggroTarget = topTarget;
    }

    private void DecayAggro()
    {
        if (aggroTable.Count == 0) return;

        List<Transform> keys = new List<Transform>(aggroTable.Keys);
        foreach (var key in keys)
        {
            aggroTable[key] -= aggroDecayRate * Time.deltaTime;
            if (aggroTable[key] <= 0f)
                aggroTable.Remove(key);
        }

        RefreshAggroTarget();
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

        Transform target = aggroTarget != null ? aggroTarget : GetNearestPartyMember();

        if (target == null)
        {
            attackModule.SetTargetImmediate(null);
            return;
        }

        float distToTarget = Vector3.Distance(transform.position, target.position);

        if (distToTarget <= chaseDistance)
        {
            if (!isAttacking)
            {
                if (attackModule.currentTarget != target)
                {
                    attackModule.SetTargetImmediate(target);
                    navAgent.isStopped = false;
                }
            }
            else if (attackModule.currentTarget != target)
            {
                float currentTargetDist = attackModule.currentTarget != null
                    ? Vector3.Distance(transform.position, attackModule.currentTarget.position)
                    : Mathf.Infinity;

                if (distToTarget < currentTargetDist - targetSwitchThreshold)
                {
                    attackModule.SetTargetImmediate(target);
                    navAgent.isStopped = false;
                }
            }
        }
        else
        {
            aggroTable.Clear();
            aggroTarget = null;
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

        if (attackModule.currentTarget == null)
            TargetingLogic();
        else
            attackModule.ResetAttackCooldown();
    }

    // ─────────────────────────────────────────────────────────────────
    // 사망 처리
    // ─────────────────────────────────────────────────────────────────

    private void HandleDeath()
    {
        attackModule.SetTargetImmediate(null);
        isAttacking = false;
        aggroTable.Clear();
        aggroTarget = null;

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

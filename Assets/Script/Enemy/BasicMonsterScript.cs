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
    [Tooltip("AudioManager에 등록한 SFX 키. 타겟이 없다가 새로 발견했을 때만 재생 (비워두면 재생 안 함)")]
    public string aggroSfxKey;

    [Header("# Wander Settings")]
    public float wanderRadius   = 5f;
    [Tooltip("배회 이동 속도 (navSpeed보다 느리게 설정 권장)")]
    public float wanderSpeed    = 1.5f;
    [Tooltip("다음 배회 지점으로 이동하는 간격 (초)")]
    public float wanderInterval = 3f;

    private const float TargetingInterval = 0.2f;
    private float targetingTimer          = 0f;

    private Rigidbody rigid;
    private AttackBase attackModule;
    private StatusEffectHandler statusHandler;

    private bool isAttacking = false;

    // 배회
    private Vector3 _spawnPosition;
    private float   _wanderTimer = 0f;

    // ─────────────────────────────────────────────────────────────────
    // 어그로 테이블
    // ─────────────────────────────────────────────────────────────────
    private Dictionary<Transform, float> aggroTable = new Dictionary<Transform, float>();
    // DecayAggro()가 매 프레임 새 List를 할당하지 않도록 재사용하는 스크래치 버퍼
    private readonly List<Transform> _aggroKeysScratch = new List<Transform>();
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
        navAgent.speed                 = navSpeed;
        navAgent.autoBraking           = false;
        navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

        _spawnPosition = transform.position;
        _wanderTimer   = wanderInterval; // 스폰 직후 바로 첫 배회 지점 선택
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
        if (attackModule == null) return;
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

        // 타겟 없을 때만 배회
        if (attackModule.currentTarget == null)
            HandleWander();
        else
            _wanderTimer = 0f; // 전투 중 타이머 리셋 — 전투 종료 후 즉시 배회 시작 방지
    }

    // ─────────────────────────────────────────────────────────────────
    // 배회
    // ─────────────────────────────────────────────────────────────────

    void HandleWander()
    {
        _wanderTimer += Time.deltaTime;

        bool reachedDest = !navAgent.pathPending
                        && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.1f;

        if (_wanderTimer >= wanderInterval || reachedDest)
        {
            _wanderTimer = 0f;
            TrySetWanderDestination();
        }

        // 이동 방향 바라보기
        Vector3 moveDir = navAgent.velocity;
        moveDir.y = 0f;
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
            transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
        }
    }

    void TrySetWanderDestination()
    {
        // 스폰 위치 기준 랜덤 방향·거리 선택
        Vector2 circle    = Random.insideUnitCircle * wanderRadius;
        Vector3 candidate = _spawnPosition + new Vector3(circle.x, 0f, circle.y);

        // NavMesh 위 유효한 위치로 보정
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
        {
            navAgent.speed = wanderSpeed;
            navAgent.SetDestination(hit.position);
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

        _aggroKeysScratch.Clear();
        _aggroKeysScratch.AddRange(aggroTable.Keys);
        foreach (var key in _aggroKeysScratch)
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
        // 기본 공격이든 스킬(정예/보스 포함)이든 시전 중이면 타겟을 바꾸거나 놓지 않는다 —
        // 그렇지 않으면 사거리(또는 추격 범위)를 벗어나는 순간 진행 중이던 공격/스킬 코루틴이
        // SetTargetImmediate에 의해 즉시 취소되어, 애니메이션이 중간에 캔슬된 것처럼 보인다.
        // 시전 중엔 그냥 넘어가고, 다음 판정 주기(TargetingInterval)에 다시 평가한다
        if (attackModule.IsAttacking || attackModule.IsCastingSkill) return;

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
                    // 완전히 새로 발견한 경우(타겟 교체가 아니라 무타겟→유타겟)에만 어그로 사운드 재생
                    bool isFirstSpot = attackModule.currentTarget == null;

                    attackModule.SetTargetImmediate(target);
                    navAgent.speed     = navSpeed; // 배회 속도 → 추격 속도 복귀
                    navAgent.isStopped = false;

                    if (isFirstSpot && !string.IsNullOrEmpty(aggroSfxKey))
                        AudioManager.instance?.PlaySFX(aggroSfxKey);
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
                    navAgent.speed     = navSpeed;
                    navAgent.isStopped = false;
                }
            }
        }
        else
        {
            // 1순위 어그로 대상만 추격 범위를 벗어난 것이므로 그 대상만 어그로 테이블에서 제거하고
            // 다른 어그로 대상(또는 가장 가까운 파티원)으로 전환 — 전체 어그로를 리셋하지 않음
            if (aggroTarget == target)
            {
                aggroTable.Remove(target);
                RefreshAggroTarget();
            }
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
        if (attackModule == null) return;

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
        attackModule?.SetTargetImmediate(null);
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
        if (rigid != null && collision.gameObject.CompareTag("Player"))
            rigid.velocity = Vector3.zero;
    }
}

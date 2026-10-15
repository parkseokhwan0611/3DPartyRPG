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

    [Header("# 최적화")]
    [Tooltip("가장 가까운 파티원이 이 거리(m)보다 멀고 전투 중이 아니면 배회를 멈추고 쉰다 (경로 계산 생략). " +
             "Chase Distance보다 충분히 커야 함. 0이면 항상 배회")]
    public float dormantDistance = 45f;

    private const float TargetingInterval = 0.2f;
    private float targetingTimer          = 0f;

    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    private bool _dormant; // 파티가 멀어서 쉬는 중

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
    // 소환수 도발(SummonSkill Taunt)을 건 대상 → 도발이 끝나는 시각. 어그로 1순위가 이 중 하나면 거리와 상관없이
    // 바로 노리고(소환수 여럿이 같은 몬스터를 도발해도 어느 쪽이 1순위가 되든 적용), 시간이 끝나면 그 어그로를 지운다
    private readonly Dictionary<Transform, float> _tauntUntil = new Dictionary<Transform, float>();
    private readonly List<Transform> _tauntKeysScratch = new List<Transform>();
    private bool _forceRetarget; // 도발이 끝난 직후 — 거리 비교 없이 새 대상으로 바꾸게
    // 도발을 한 번 받은 뒤로는 소환수도 "가장 가까운 대상" 후보에 넣는다 (전투가 끝나면 = 대상이 없어지면 해제).
    // 평소엔 파티원만 노리고 소환수는 도발로만 노림
    private bool _includeSummonsInNearest;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        animator      = GetComponent<Animator>();
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
        if (!GameManager.IsLive) return;
        if (enemyHp == null || enemyHp.isDead) return;
        if (attackModule == null) return;
        if (statusHandler != null && statusHandler.HasDebuff(StatusEffectType.Stun)) return;

        // 문자열 대신 해시로 — 공격·스턴 스크립트도 isWalking을 직접 끄므로 값 캐시 없이 매 프레임 맞춘다
        if (animator != null)
            animator.SetBool(IsWalkingHash, navAgent.velocity.sqrMagnitude > 0.01f);

        DecayAggro();
        UpdateTauntExpiry();

        targetingTimer += Time.deltaTime;
        if (targetingTimer >= TargetingInterval)
        {
            targetingTimer = 0f;
            // 어그로 1순위는 대상 판단 주기에만 다시 계산 — 매 프레임 어그로 대상마다 컴포넌트를 조회하던 비용 절감.
            // 어그로가 새로 쌓이는 순간(AddAggro)과 도발 종료 때는 즉시 다시 계산한다
            RefreshAggroTarget();
            UpdateDormant();
            TargetingLogic();
        }

        // 타겟 없을 때만 배회 (파티가 멀면 쉼)
        if (attackModule.currentTarget == null)
        {
            if (!_dormant) HandleWander();
        }
        else
            _wanderTimer = 0f; // 전투 중 타이머 리셋 — 전투 종료 후 즉시 배회 시작 방지
    }

    // 전투와 무관하고 파티가 멀리 있으면 쉰다 — 플레이어가 볼 수 없는 몬스터까지 계속 배회 경로를 계산하지 않게.
    // 쉬기 시작할 때 가던 길을 멈추고, 다시 깨어나면 바로 새 배회 지점을 고른다
    private void UpdateDormant()
    {
        bool dormant = false;
        if (dormantDistance > 0f && attackModule.currentTarget == null && aggroTable.Count == 0
            && PartyManager.instance != null && PartyManager.instance.partyMembers.Count > 0)
        {
            Transform nearest = GetNearestPartyMember();
            dormant = nearest == null
                   || (nearest.position - transform.position).sqrMagnitude > dormantDistance * dormantDistance;
        }

        if (dormant == _dormant) return;
        _dormant = dormant;

        if (dormant)
        {
            if (navAgent.enabled && navAgent.isOnNavMesh && navAgent.hasPath) navAgent.ResetPath();
        }
        else
        {
            _wanderTimer = wanderInterval;
        }
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

    // 도발 — 어그로를 더하고, 공격 중(첫 공격 전 추격 포함)이라도 "더 가까울 때만 교체" 규칙을 건너뛰고 바로 노리게 한다.
    // 다음 프레임에 대상 선정을 다시 돌려 즉시 반영 (지금 공격 모션 중이면 그 모션이 끝난 뒤 교체 — SetTargetImmediate).
    // duration초 뒤 도발로 준 어그로를 지우고 원래 대상으로 돌아간다 (다시 도발하면 시간 갱신)
    public void Taunt(Transform target, float amount, float duration)
    {
        if (target == null) return;
        AddAggro(target, amount);
        _tauntUntil[target] = Time.time + Mathf.Max(0.1f, duration);
        targetingTimer = TargetingInterval;
    }

    private void UpdateTauntExpiry()
    {
        if (_tauntUntil.Count == 0) return;

        _tauntKeysScratch.Clear();
        foreach (var kv in _tauntUntil)
            if (kv.Key == null || Time.time >= kv.Value) _tauntKeysScratch.Add(kv.Key);
        if (_tauntKeysScratch.Count == 0) return;

        foreach (var key in _tauntKeysScratch)
        {
            _tauntUntil.Remove(key);
            if (key != null) aggroTable.Remove(key);
            if (key == attackModule.currentTarget) _forceRetarget = true;
        }
        // 도발이 끝나면: 남은 어그로(탱커 도발 등)가 있으면 그쪽, 없으면 소환수 포함 가장 가까운 대상
        _includeSummonsInNearest = true;
        RefreshAggroTarget();
        targetingTimer = TargetingInterval; // 바로 다음 대상 판단
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

            // 사망 모션 중인 소환수도 제외 — 오브젝트가 사라지기 전까지 시체를 계속 노리지 않게
            if (member == null && entry.Key.TryGetComponent(out SummonUnit summon) && !summon.IsAlive)
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
            {
                aggroTable.Remove(key);
                _tauntUntil.Remove(key); // 도발 효과도 어그로와 함께 끝
                if (key == aggroTarget) aggroTarget = null; // 1순위가 빠지면 다음 판단 주기에 다시 계산
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 타겟팅 로직
    // ─────────────────────────────────────────────────────────────────

    void TargetingLogic()
    {
        // 스킬 시전 중(윈드업~후딜)에는 타겟을 바꾸지 않는다 — 스킬 컨트롤러가 시전 시작 시점에
        // 캡처해둔 타겟으로 이미 진행 중이고, agent.isStopped 등 스킬 자체의 정지 상태를 여기서
        // 건드리면 안 되기 때문. 기본 공격 중(IsAttacking)인 경우는 막지 않음 — AttackBase.
        // SetTargetImmediate가 내부적으로 대기시켰다가 공격이 끝나는 시점에 안전하게 반영해주므로,
        // 여기서 막으면 오히려 공격이 계속 이어지는 동안(예: 원거리 몬스터가 쉬지 않고 쏘는 경우)
        // 더 가까운 대상이 나타나도 원래 타겟만 계속 노리는 문제가 생긴다
        if (attackModule.IsCastingSkill) return;

        if (PartyManager.instance == null || PartyManager.instance.partyMembers.Count == 0)
        {
            attackModule.SetTargetImmediate(null);
            return;
        }

        Transform target = aggroTarget != null ? aggroTarget : GetNearestTarget(_includeSummonsInNearest);

        if (target == null)
        {
            attackModule.SetTargetImmediate(null);
            _includeSummonsInNearest = false; // 전투 종료
            return;
        }

        float distToTarget = Vector3.Distance(transform.position, target.position);

        // 도발 종료 후 한 번만 쓰는 강제 교체 플래그 — 이번 판단에서 소비
        bool forceRetarget = _forceRetarget;
        _forceRetarget = false;

        if (distToTarget <= chaseDistance)
        {
            if (!isAttacking)
            {
                if (attackModule.currentTarget != target)
                {
                    // 완전히 새로 발견한 경우(타겟 교체가 아니라 무타겟→유타겟)에만 어그로 사운드 재생
                    bool isFirstSpot = attackModule.currentTarget == null;

                    attackModule.SetTargetImmediate(target);
                    navAgent.speed = navSpeed; // 배회 속도 → 추격 속도 복귀
                    // isStopped 해제는 SetTargetImmediate가 담당 — 공격 도중이라 변경이 대기됐다면
                    // (아직 공격 애니메이션이 안 끝났다면) 여기서 강제로 풀면 안 되기 때문에 건드리지 않음

                    if (isFirstSpot && !string.IsNullOrEmpty(aggroSfxKey))
                        AudioManager.instance?.PlaySFX(aggroSfxKey);
                }
            }
            else if (attackModule.currentTarget != target)
            {
                float currentTargetDist = attackModule.currentTarget != null
                    ? Vector3.Distance(transform.position, attackModule.currentTarget.position)
                    : Mathf.Infinity;

                // 도발한 소환수는 거리 비교 없이 바로 교체 — 도발이 끝난 직후에도 원래 대상으로 바로 돌아감
                bool taunted = _tauntUntil.ContainsKey(target);
                if (taunted || forceRetarget || distToTarget < currentTargetDist - targetSwitchThreshold)
                {
                    attackModule.SetTargetImmediate(target);
                    navAgent.speed = navSpeed; // isStopped는 SetTargetImmediate가 담당 (위와 동일 이유)
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
                _tauntUntil.Remove(target);
                RefreshAggroTarget();
            }
            attackModule.SetTargetImmediate(null);
            _includeSummonsInNearest = false; // 추격 범위 밖 = 전투 종료
        }
    }

    // 가장 가까운 대상 — 기본은 살아 있는 파티원, includeSummons면 살아 있는 소환수도 후보
    Transform GetNearestTarget(bool includeSummons)
    {
        Transform closest = GetNearestPartyMember();
        if (!includeSummons) return closest;

        float minSqr = closest != null ? (closest.position - transform.position).sqrMagnitude : Mathf.Infinity;
        foreach (var s in SummonUnit.All)
        {
            if (s == null || !s.IsAlive) continue;
            float sqr = (s.transform.position - transform.position).sqrMagnitude;
            if (sqr < minSqr) { minSqr = sqr; closest = s.transform; }
        }
        return closest;
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
        {
            RefreshAggroTarget();
            TargetingLogic();
        }
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
        aggroTarget  = null;
        _tauntUntil.Clear();
        _forceRetarget = false;
        _includeSummonsInNearest = false;

        if (navAgent.enabled)
        {
            navAgent.isStopped = true;
            navAgent.velocity  = Vector3.zero;
        }
    }
}

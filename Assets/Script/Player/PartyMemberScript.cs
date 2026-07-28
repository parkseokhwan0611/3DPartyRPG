using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class PartyMemberScript : MonoBehaviour
{
    // ─────────────────────────────────────────
    // 상태머신
    // ─────────────────────────────────────────
    public enum MemberState { Idle, Following, Attacking, Dead }
    public MemberState CurrentState { get; private set; } = MemberState.Idle;

    // ─────────────────────────────────────────
    // 컴포넌트 참조 (캐싱)
    // ─────────────────────────────────────────
    [HideInInspector] public NavMeshAgent agent;
    private Animator anim;
    private AttackBase attackComp;
    private SkillManager skillManager;
    private CharacterStat statComp;
    // SkillManager 등 외부에서 매 프레임 GetComponent로 재조회하지 않도록 캐시된 참조 노출
    public CharacterStat StatComp => statComp;
    private PartyStatusEffectHandler statusHandler;
    private int _chainIndex = 0;

    // ─────────────────────────────────────────
    // 파티 체인 설정
    // ─────────────────────────────────────────
    [Header("파티 체인 설정")]
    public bool isLeader = false;
    public Transform targetToFollow;
    [Header("리더 표시 VFX")]
    public GameObject leaderVFX;
    [Header("사망 처리")]
    public string deathAnimTrigger = "Die";
    [Tooltip("사망 애니메이션 후 오브젝트 숨기기까지 대기 시간 (초)")]
    public float deathHideDelay = 2f;

    // ─────────────────────────────────────────
    // 이동 설정
    // ─────────────────────────────────────────
    [Header("이동 설정")]
    public float stopDistance   = 2.0f;
    public float resumeDistance = 3.5f;
    public float rotationSpeed  = 8.0f;
    [Tooltip("NavMeshAgent 가속도 — 방향 전환/출발 시 목표 속도까지 얼마나 빨리 도달하는지. 높을수록 즉각적")]
    public float moveAcceleration = 25f;
    [Tooltip("Idle 진입 후 Following 재진입을 막는 쿨다운 (초)")]
    public float followCooldown = 0.3f;

    private float _followCooldownTimer = 0f;
    private float _walkAnimTimer       = 0f;
    private const float WALK_ANIM_DELAY = 0.12f;

    // 리더가 팔로워 사이를 지날 때 팔로워가 양보하도록 우선순위 차등 (값이 낮을수록 우선순위 높음)
    private const int LEADER_AVOIDANCE_PRIORITY   = 0;
    private const int FOLLOWER_AVOIDANCE_PRIORITY = 50;

    private Vector3 _lastFollowDestination = Vector3.positiveInfinity;
    private const float FOLLOW_DEST_THRESHOLD = 0.3f;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        agent        = GetComponent<NavMeshAgent>();
        anim         = GetComponent<Animator>();
        attackComp   = GetComponent<AttackBase>();
        skillManager = GetComponent<SkillManager>();
        statComp     = GetComponent<CharacterStat>();
        statusHandler = GetComponent<PartyStatusEffectHandler>();

        if (attackComp != null)
        {
            attackComp.OnAttackStarted += HandleAttackStarted;
            attackComp.OnAttackEnded   += HandleAttackEnded;
        }
    }

    void Start()
    {
        agent.acceleration    = moveAcceleration;
        agent.angularSpeed    = 1000f;
        agent.stoppingDistance = stopDistance;
        // 회전은 항상 스크립트가 전담(FaceMovementDirection / SmoothLookAt / AttackBase.LookAtTarget) —
        // NavMeshAgent 자체 회전과 동시에 켜두면 서로 다른 방향으로 매 프레임 덮어써서 빙빙 도는 것처럼 보임
        agent.updateRotation  = false;

        if (leaderVFX != null) leaderVFX.SetActive(isLeader);
    }

    void OnDestroy()
    {
        // 구독 해제 (메모리 누수 방지)
        if (attackComp != null)
        {
            attackComp.OnAttackStarted -= HandleAttackStarted;
            attackComp.OnAttackEnded   -= HandleAttackEnded;
        }
    }

    void Update()
    {
        if (CurrentState == MemberState.Dead) return;

        if (statComp != null) agent.speed = statComp.TotalMoveSpeed;

        UpdateAnimation();

        if (CurrentState == MemberState.Attacking) return;

        if (skillManager != null && skillManager.IsActivatingSkill) return;
        if (attackComp  != null && attackComp.IsCastingSkill) return;

        if (isLeader)
        {
            agent.stoppingDistance = 0.1f;
            HandleLeaderMovement();
        }
        else if (targetToFollow != null)
        {
            agent.stoppingDistance = stopDistance;
            HandleFollowLogic();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 상태 전환 (AttackBase 이벤트 콜백)
    // ─────────────────────────────────────────────────────────────────

    private void HandleAttackStarted()
    {
        if (CurrentState == MemberState.Dead) return;
        ChangeState(MemberState.Attacking);
    }

    private void HandleAttackEnded()
    {
        if (isLeader)
        {
            ChangeState(MemberState.Idle);
            return;
        }

        // 팔로워: 공격 후 현재 거리 체크해서 가까우면 Idle 유지
        if (targetToFollow != null)
        {
            float sqrDist = (transform.position - targetToFollow.position).sqrMagnitude;
            if (sqrDist <= stopDistance * stopDistance)
            {
                ChangeState(MemberState.Idle);
                _followCooldownTimer = followCooldown;
            }
            else
            {
                ChangeState(MemberState.Following);
            }
        }
        else
        {
            ChangeState(MemberState.Idle);
        }
    }

    public void ChangeState(MemberState newState)
    {
        if (CurrentState == newState) return;
        CurrentState = newState;
    }

    // ─────────────────────────────────────────────────────────────────
    // 파티 체인 순서 갱신 (PartyManager에서 호출)
    // ─────────────────────────────────────────────────────────────────
    public void UpdateChainOrder(List<PartyMemberScript> newOrder)
    {
        int myIndex = newOrder.IndexOf(this);

        if (myIndex == 0) // 내가 리더
        {
            isLeader               = true;
            _chainIndex            = 0;
            targetToFollow         = null;
            agent.stoppingDistance = 0.1f;
            agent.avoidancePriority = LEADER_AVOIDANCE_PRIORITY;
            // 팔로워 시절 잔여 경로/속도 초기화 — 남아있으면 리더 첫 이동 명령의 회피 계산이 꼬임
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            ChangeState(MemberState.Idle);

            if (leaderVFX != null) leaderVFX.SetActive(true);
        }
        else // 내가 팔로워
        {
            isLeader               = false;
            _chainIndex            = myIndex;
            targetToFollow         = newOrder[myIndex - 1].transform;
            _lastFollowDestination = Vector3.positiveInfinity; // 타겟 바뀌면 즉시 재경로
            agent.stoppingDistance = stopDistance;
            agent.avoidancePriority = FOLLOWER_AVOIDANCE_PRIORITY;
            // 이전 리더 경로 즉시 초기화 — 남은 경로가 팔로우 로직을 방해하지 않도록
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            ChangeState(MemberState.Following);

            skillManager?.ResetAttackCount();
            if (leaderVFX != null) leaderVFX.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 이동 로직
    // ─────────────────────────────────────────────────────────────────

    void HandleLeaderMovement()
    {
        if (!agent.enabled) return;
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (agent.hasPath)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
        }

        FaceMovementDirection();
    }

    // agent.updateRotation을 안 쓰므로, 이동 중일 때 진행 방향을 보도록 직접 회전
    void FaceMovementDirection()
    {
        Vector3 moveDir = agent.desiredVelocity;
        moveDir.y = 0f;
        if (moveDir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
        transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
    }

    void HandleFollowLogic()
    {
        if (_followCooldownTimer > 0f) _followCooldownTimer -= Time.deltaTime;

        float sqrDist   = (transform.position - targetToFollow.position).sqrMagnitude;
        float sqrResume = resumeDistance * resumeDistance;
        float sqrStop   = stopDistance   * stopDistance;

        if (CurrentState != MemberState.Following && sqrDist > sqrResume)
        {
            // 쿨다운 중이어도 resumeDistance의 2배 이상 멀어지면 즉시 따라감
            if (_followCooldownTimer <= 0f || sqrDist > sqrResume * 4f)
            {
                ChangeState(MemberState.Following);
                _lastFollowDestination = Vector3.positiveInfinity; // 진입 시 강제 재경로
            }
        }
        else if (CurrentState == MemberState.Following && sqrDist <= sqrStop)
        {
            ChangeState(MemberState.Idle);
            _followCooldownTimer   = followCooldown;
            _lastFollowDestination = Vector3.positiveInfinity;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (CurrentState == MemberState.Following)
        {
            Vector3 dest = targetToFollow.position;
            float approachZoneSqr = stopDistance * 1.5f * (stopDistance * 1.5f);

            // 정지 거리 1.5배 이내에 들어오면 목표 재설정 중단 — 도착 직전 경로 재계산으로 인한 빙빙돌기 방지
            if (sqrDist >= approachZoneSqr &&
                (_lastFollowDestination - dest).sqrMagnitude > FOLLOW_DEST_THRESHOLD * FOLLOW_DEST_THRESHOLD)
            {
                agent.SetDestination(dest);
                _lastFollowDestination = dest;
            }
            SmoothLookAt(dest);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────────────────────────

    void UpdateAnimation()
    {
        if (anim == null) return;
        if (skillManager != null && skillManager.IsActivatingSkill)
        {
            _walkAnimTimer = 0f;
            return;
        }
        if (attackComp != null && attackComp.IsCastingSkill)
        {
            _walkAnimTimer = 0f;
            return;
        }

        // 공격 애니메이션 재생 중: 타이머 즉시 초기화 후 walk 강제 해제
        if (attackComp != null && attackComp.IsAttackAnimPlaying)
        {
            _walkAnimTimer = 0f;
            anim.SetBool("isWalking", false);
            return;
        }

        bool isMoving = agent.velocity.sqrMagnitude > 0.01f;

        if (isMoving)
            _walkAnimTimer += Time.deltaTime;
        else
            _walkAnimTimer = 0f;

        anim.SetBool("isWalking", _walkAnimTimer >= WALK_ANIM_DELAY);
        anim.SetFloat("speed", statComp != null ? statComp.MoveSpeedMultiplier : 1f);
    }

    // 버프/힐 스킬 종료 후 상태 복귀 (SkillManager에서 호출)
    public void ResumeAfterSkill()
    {
        if (CurrentState == MemberState.Dead) return;

        if (!isLeader && targetToFollow != null)
        {
            float sqrDist = (transform.position - targetToFollow.position).sqrMagnitude;
            if (sqrDist > resumeDistance * resumeDistance)
                ChangeState(MemberState.Following);
            else
                _followCooldownTimer = followCooldown;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 사망 처리 (CharacterStat에서 호출)
    // ─────────────────────────────────────────────────────────────────

    public void Die()
    {
        ChangeState(MemberState.Dead);

        // 진행 중인 공격·스킬 코루틴 즉시 취소
        attackComp?.ForceCancelAttack();
        skillManager?.ForceStopCurrentSkill();

        // GameObject가 비활성화되기 전에 활성 버프/디버프 스탯을 즉시 원상복구
        statusHandler?.ClearAllOnDeath();

        if (agent != null && agent.enabled)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.enabled  = false;
        }

        if (anim != null && !string.IsNullOrEmpty(deathAnimTrigger))
            anim.SetTrigger(deathAnimTrigger);

        if (leaderVFX != null) leaderVFX.SetActive(false);

        StartCoroutine(HideAfterDeath());
    }

    private IEnumerator HideAfterDeath()
    {
        yield return new WaitForSecondsRealtime(deathHideDelay);
        gameObject.SetActive(false);
    }

    void SmoothLookAt(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0;
        if (dir == Vector3.zero) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
    }
}
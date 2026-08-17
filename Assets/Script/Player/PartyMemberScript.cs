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
    public float resumeDistance = 2.7f;
    public float rotationSpeed  = 8.0f;
    [Tooltip("NavMeshAgent 가속도 — 방향 전환/출발 시 목표 속도까지 얼마나 빨리 도달하는지. 높을수록 즉각적")]
    public float moveAcceleration = 25f;
    [Tooltip("Idle 진입 후 Following 재진입을 막는 쿨다운 (초)")]
    public float followCooldown = 0.3f;
    [Tooltip("전투 중(자기 몬스터를 공격하는 중)에도, 리더와의 거리가 resumeDistance의 이 배수를 넘으면 " +
             "공격을 즉시 끊고 따라붙는다 — 보스 스킬 회피처럼 리더가 갑자기 멀리 움직였을 때, 공격 " +
             "애니메이션이 끝날 때까지 팔로워가 제자리에 묶여있는 것을 막기 위함. 너무 작으면 평소 전투 " +
             "중에도 자주 끊겨 산만해지니 2~3 정도 권장")]
    public float emergencyFollowMultiplier = 2f;

    [Header("이동 속도 (역할별 고정값)")]
    [Tooltip("리더일 때 이동 속도. 클래스별 스탯이 아닌 이 값으로 고정 — 팔로워가 계속 따라올 수 있는 속도로 맞춤")]
    public float leaderMoveSpeed   = 4f;
    [Tooltip("팔로워일 때 이동 속도")]
    public float followerMoveSpeed = 2f;

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

        // 이동 속도는 클래스 스탯이 아닌 리더/팔로워 역할 고정값 기준 — 슬로우/헤이스트 같은
        // 버프/디버프 배율(MoveSpeedMultiplier)만 스탯에서 반영
        float baseSpeed = isLeader ? leaderMoveSpeed : followerMoveSpeed;
        float speedMultiplier = statComp != null ? statComp.MoveSpeedMultiplier : 1f;
        agent.speed = baseSpeed * speedMultiplier;

        UpdateAnimation();

        // 스턴 중에는 공격이든 이동이든 전부 멈춘다 — agent.isStopped 걸기, 하던 공격/스킬 강제
        // 취소, 스턴 애니메이션 재생은 PartyStatusEffectHandler.ApplyStun()이 이미 처리했으므로,
        // 여기서는 이동/추격 재개 판단 로직 자체가 다시 움직이라고 지시하지 않도록 전부 건너뛴다
        if (statusHandler != null && statusHandler.HasDebuff(StatusEffectType.Stun)) return;

        // 파티원 고정 중인 팔로워 — 리더 거리와 무관하게 제자리에서 하던 공격을 계속하거나(Attacking)
        // 가만히 대기한다. 새 공격 명령(적 클릭)은 AttackBase.SetTarget으로 바로 들어오므로 여기서
        // 막을 필요 없음 — 이동 관련 로직(비상 추격/체인 팔로우)만 건너뛴다
        bool holdActive = !isLeader && PartyManager.instance != null && PartyManager.instance.PartyHoldEnabled;

        if (CurrentState == MemberState.Attacking)
        {
            // 평소 전투 중에는 공격을 끝까지 재생하지만, 리더가 스킬 회피 등으로 갑자기 멀리
            // 벗어나면(resumeDistance의 emergencyFollowMultiplier배 이상) 공격을 즉시 끊고 따라붙는다.
            // 그러지 않으면 공격 애니메이션(윈드업+후딜)이 끝날 때까지 팔로워가 위험 지역에 그대로
            // 묶여있게 되어, 평소 이동과 달리 전투 중 회피만 유독 굼떠 보이는 원인이 된다.
            // 단, 파티원 고정 중에는 이 비상 탈출도 걸지 않는다 — 고정의 의도 자체가 "무슨 일이 있어도 제자리"이므로
            if (!isLeader && targetToFollow != null && !holdActive)
            {
                float sqrDist      = (transform.position - targetToFollow.position).sqrMagnitude;
                float emergencyDist = resumeDistance * emergencyFollowMultiplier;
                if (sqrDist > emergencyDist * emergencyDist)
                {
                    attackComp?.ForceCancelAttack();
                    ChangeState(MemberState.Following);
                    _lastFollowDestination = Vector3.positiveInfinity;
                }
            }
            return;
        }

        if (skillManager != null && skillManager.IsActivatingSkill) return;
        if (attackComp  != null && attackComp.IsCastingSkill) return;
        if (holdActive) return;

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

            // 리더만 바뀌었을 뿐 아무도 움직이지 않았는데 체인 순서가 바뀌었다는 이유만으로
            // 즉시 재정렬 이동하는 것을 막기 위해 Idle로 시작 — 새 리더가 실제로 움직여서
            // resumeDistance를 벗어나야 HandleFollowLogic이 알아서 Following으로 전환한다
            ChangeState(MemberState.Idle);

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

            // 리더가 팔로워보다 빠르면 목표 지점이 계속 낡아버려서(멈칫멈칫 원인) — 거리 상관없이
            // 목적지가 실제로 움직였을 때(FOLLOW_DEST_THRESHOLD 이상)마다 계속 갱신한다.
            // 미세한 흔들림으로 인한 반복 재경로는 이 임계값 체크만으로 충분히 막힌다
            if ((_lastFollowDestination - dest).sqrMagnitude > FOLLOW_DEST_THRESHOLD * FOLLOW_DEST_THRESHOLD)
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
        // 스턴 애니메이션(isStun 트리거)은 PartyStatusEffectHandler가 직접 재생 — 여기서는
        // walk 타이머만 리셋해서 걷기 애니메이션이 끼어들지 않게 함
        if (statusHandler != null && statusHandler.HasDebuff(StatusEffectType.Stun))
        {
            _walkAnimTimer = 0f;
            return;
        }
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
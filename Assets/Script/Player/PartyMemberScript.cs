using UnityEngine;
using UnityEngine.AI;
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
    private AttackBase attackComp; // ← Awake에서 한 번만 캐싱

    // ─────────────────────────────────────────
    // 파티 체인 설정
    // ─────────────────────────────────────────
    [Header("파티 체인 설정")]
    public bool isLeader = false;
    public Transform targetToFollow;

    // ─────────────────────────────────────────
    // 이동 설정
    // ─────────────────────────────────────────
    [Header("이동 설정")]
    public float stopDistance   = 2.0f;
    public float resumeDistance = 3.5f;
    public float rotationSpeed  = 8.0f;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        agent      = GetComponent<NavMeshAgent>();
        anim       = GetComponent<Animator>();
        attackComp = GetComponent<AttackBase>();

        // AttackBase의 공격 시작/종료 이벤트를 구독합니다.
        // PartyMemberScript는 이제 AttackBase의 내부 상태를 직접 보지 않습니다.
        if (attackComp != null)
        {
            attackComp.OnAttackStarted += HandleAttackStarted;
            attackComp.OnAttackEnded   += HandleAttackEnded;
        }
    }

    void Start()
    {
        agent.acceleration    = 12f;
        agent.angularSpeed    = 1000f;
        agent.stoppingDistance = stopDistance;
        agent.updateRotation  = isLeader;
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
        UpdateAnimation();

        // Dead 상태면 아무것도 하지 않음
        if (CurrentState == MemberState.Dead) return;

        // Attacking 상태면 이동 로직을 건너뜀
        // (AttackBase가 이동을 직접 제어하므로 여기선 관여하지 않음)
        if (CurrentState == MemberState.Attacking) return;

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
        ChangeState(MemberState.Attacking);
    }

    private void HandleAttackEnded()
    {
        // 공격이 끝나면 리더/팔로워에 맞게 상태 복귀
        ChangeState(isLeader ? MemberState.Idle : MemberState.Following);
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
            isLeader              = true;
            targetToFollow        = null;
            agent.updateRotation  = true;
            agent.stoppingDistance = 0.1f;
            ChangeState(MemberState.Idle);
        }
        else // 내가 팔로워
        {
            isLeader              = false;
            targetToFollow        = newOrder[myIndex - 1].transform;
            agent.updateRotation  = false;
            agent.stoppingDistance = stopDistance;
            ChangeState(MemberState.Following);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 이동 로직
    // ─────────────────────────────────────────────────────────────────

    void HandleLeaderMovement()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (agent.hasPath)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
        }
    }

    void HandleFollowLogic()
    {
        float dist = Vector3.Distance(transform.position, targetToFollow.position);

        if (CurrentState != MemberState.Following && dist > resumeDistance)
        {
            ChangeState(MemberState.Following);
        }
        else if (CurrentState == MemberState.Following && dist <= stopDistance)
        {
            ChangeState(MemberState.Idle);
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (CurrentState == MemberState.Following)
        {
            agent.SetDestination(targetToFollow.position);
            SmoothLookAt(targetToFollow.position);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────────────────────────

    void UpdateAnimation()
    {
        if (anim == null) return;

        bool walking = isLeader
            ? agent.velocity.sqrMagnitude > 0.1f
            : CurrentState == MemberState.Following;

        anim.SetBool("isWalking", walking);
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
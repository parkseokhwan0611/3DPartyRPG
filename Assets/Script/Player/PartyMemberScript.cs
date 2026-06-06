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

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        agent        = GetComponent<NavMeshAgent>();
        anim         = GetComponent<Animator>();
        attackComp   = GetComponent<AttackBase>();
        skillManager = GetComponent<SkillManager>();

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
            isLeader               = true;
            targetToFollow         = null;
            agent.updateRotation   = true;
            agent.stoppingDistance = 0.1f;
            ChangeState(MemberState.Idle);

            if (leaderVFX != null) leaderVFX.SetActive(true); // ← 추가
        }
        else // 내가 팔로워
        {
            isLeader               = false;
            targetToFollow         = newOrder[myIndex - 1].transform;
            agent.updateRotation   = false;
            agent.stoppingDistance = stopDistance;
            ChangeState(MemberState.Following);

            if (leaderVFX != null) leaderVFX.SetActive(false); // ← 추가
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

        if (skillManager != null && skillManager.IsActivatingSkill) return;
        if (attackComp  != null && attackComp.IsCastingSkill) return;

        bool walking = isLeader
            ? agent.velocity.sqrMagnitude > 0.1f
            : CurrentState == MemberState.Following;

        anim.SetBool("isWalking", walking);
    }

    // 버프/힐 스킬 종료 후 상태 복귀 (SkillManager에서 호출)
    public void ResumeAfterSkill()
    {
        if (CurrentState == MemberState.Dead) return;

        // 팔로워: Following 상태로 복귀해 즉시 이동 재개
        if (!isLeader && targetToFollow != null)
        {
            float dist = Vector3.Distance(transform.position, targetToFollow.position);
            if (dist > stopDistance)
                ChangeState(MemberState.Following);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 사망 처리 (CharacterStat에서 호출)
    // ─────────────────────────────────────────────────────────────────

    public void Die()
    {
        ChangeState(MemberState.Dead);

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
        yield return new WaitForSeconds(deathHideDelay);
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
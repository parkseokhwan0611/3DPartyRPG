using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class PartyMemberScript : MonoBehaviour
{
    public NavMeshAgent agent;
    public bool isLeader = false;
    public Animator anim;
    private AttackBase attackComp;
    
    [Header("파티 체인 설정")]
    public Transform targetToFollow; // 내 바로 앞 순서의 캐릭터
    public List<PartyMemberScript> partyMembers; // 파티원 전체 리스트 (매니저에서 할당 권장)
    
    private bool isFollowing = false;

    [Header("자연스러운 이동 설정")]
    public float stopDistance = 2.0f;  // 앞 사람과 이 거리면 멈춤
    public float resumeDistance = 3.5f; // 앞 사람이 이보다 멀어지면 출발
    public float rotationSpeed = 8.0f;
    void Awake() 
    {
        attackComp = GetComponent<AttackBase>(); 
    }
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.acceleration = 12f;
        agent.angularSpeed = 1000f;
        agent.stoppingDistance = stopDistance;
        
        // 리더가 아닐 때 NavMeshAgent의 자동 회전을 꺼야 SmoothLookAt이 먹힙니다.
        agent.updateRotation = isLeader; 

        if (anim == null) anim = GetComponent<Animator>();
    }
    void Update()
    {
        UpdateAnimation();
        // [중요] 공격 중 판정 로직 수정
        // attackComp.currentTarget이 null인 것을 확인하는 것 외에도 
        // 리더가 이동 중일 때는 공격보다 이동을 우선하도록 조건을 확인해야 합니다.
        if (attackComp != null && attackComp.currentTarget != null)
        {
            // 리더가 이동 중이 아닐 때만 공격 로직을 수행하도록 방어 코드를 짤 수 있습니다.
            isFollowing = false;
            return; 
        }

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
    // [중요] 리더가 바뀔 때마다 모든 파티원이 호출해야 하는 함수
    public void UpdateChainOrder(List<PartyMemberScript> newOrder)
    {
        int myIndex = newOrder.IndexOf(this);

        if (myIndex == 0) // 내가 리더
        {
            isLeader = true;
            targetToFollow = null;
            agent.updateRotation = true;
            agent.stoppingDistance = 0.1f; // 리더는 클릭 지점에 정확히 멈춤
        }
        else // 내가 팔로워
        {
            isLeader = false;
            targetToFollow = newOrder[myIndex - 1].transform;
            agent.updateRotation = false;

            // [핵심 추가] 내 앞사람(targetToFollow)이 누구냐에 따라 내 정지 거리를 보정합니다.
            // 앞 사람이 원거리 캐릭터라면, 조금 더 멀리서 멈추게 합니다.
            PartyMemberScript frontMember = newOrder[myIndex - 1];
            
            // 기본 stopDistance가 2.0이라면, 원거리를 쫓을 땐 0.5~1.0을 더해줍니다.
            agent.stoppingDistance = stopDistance; 
            
            // 예: 리더가 원거리(마법사/힐러)라면 근거리는 0.8m 더 뒤에 정지
            // (인스펙터에서 public bool isMelee 같은 변수를 만들어 체크하면 더 좋습니다)
        }
    }

    void HandleFollowLogic()
    {
        float distanceToTarget = Vector3.Distance(transform.position, targetToFollow.position);

        if (!isFollowing && distanceToTarget > resumeDistance)
        {
            isFollowing = true;
        }
        else if (isFollowing && distanceToTarget <= stopDistance)
        {
            isFollowing = false;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (isFollowing)
        {
            agent.SetDestination(targetToFollow.position);
            SmoothLookAt(targetToFollow.position);
        }
    }

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
    void UpdateAnimation()
    {
        if (anim == null) return;
        anim.SetBool("isWalking", isLeader ? agent.velocity.sqrMagnitude > 0.1f : isFollowing);
    }

    void SmoothLookAt(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GoblinThiefMaleScript : MonoBehaviour
{
    public Animator animator;
    Rigidbody rigid;
    public EnemyHp enemyHp;
    public GameObject normalDamageText;
    
    [Header("#Player")]
    public List<PartyMemberScript> partyMembers;
    public float distance;
    public float chaseDistance;
    
    [Header("#NavMesh")]
    public NavMeshAgent navAgent;
    public float navSpeed;
    
    [Header("#Attack")]
    public bool isAttack; // 외부 참조용으로 남겨둠
    
    [Header("#Reference")]
    public Transform hudPos;

    private AttackBase attackModule;
    private MonsterMeleeAttack monsterMeleeAttack;

    void Awake() 
    {
        animator = GetComponent<Animator>();
        rigid = GetComponent<Rigidbody>();
        attackModule = GetComponent<AttackBase>();
        monsterMeleeAttack = GetComponent<MonsterMeleeAttack>();
    }

    void Start() 
    {
        navAgent = GetComponent<NavMeshAgent>();
        navAgent.speed = navSpeed; // NavAgent 속도 설정
    }

    void Update() 
    {
        if(!GameManager.instance.isLive) return;

        // 고블린이 살아있을 때만 타겟팅 진행
        if(enemyHp.hp > 0 && !enemyHp.isDead) 
        {
            TargetingLogic();
        }
        else
        {
            // 죽으면 타겟을 비워서 AttackBase 정지
            attackModule.SetTarget(null);
        }
    }
    void TargetingLogic()
    {
        // 1. 파티원이 없거나 리스트가 비어있으면 타겟 해제
        if (partyMembers == null || partyMembers.Count == 0)
        {
            attackModule.SetTarget(null);
            return;
        }

        // 2. 가장 가까운 파티원 찾기
        Transform nearestTarget = GetNearestPartyMember();

        if (nearestTarget == null)
        {
            attackModule.SetTarget(null);
            return;
        }

        // 3. 거리 계산 및 로직 수행
        float distanceToTarget = Vector3.Distance(transform.position, nearestTarget.position);

        // 공격 중이 아닐 때만 타겟을 갱신하거나 추격함
        if (distanceToTarget <= chaseDistance && !monsterMeleeAttack.isAttacking)
        {
            attackModule.SetTarget(nearestTarget);
        }
        else if (monsterMeleeAttack.isAttacking)
        {
            // 공격 중일 때는 타겟을 유지해서 바라보게 하되, 이동만 멈춤
            // (이전 대화에서 제안한 '공격 중 회전'을 위한 로직)
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }
        else
        {
            // 범위를 벗어난 경우
            attackModule.SetTarget(null);
        }
    }

    // 파티원 중 가장 가까운 대상을 반환하는 함수
    Transform GetNearestPartyMember()
    {
        Transform closest = null;
        float minDistance = Mathf.Infinity; // 초기값은 무한대
        Vector3 currentPos = transform.position;

        foreach (PartyMemberScript member in partyMembers)
        {
            if (member == null) continue;

            // 만약 캐릭터에게 '죽음' 상태 체크가 있다면 여기서 걸러주는 게 좋습니다.
            // if (member.isDead) continue; 

            float dist = Vector3.Distance(currentPos, member.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = member.transform;
            }
        }

        return closest;
    }
    void OnCollisionStay(Collision collision) 
    {
        if (collision.gameObject.CompareTag("Player")) 
        {
            rigid.velocity = Vector3.zero;
        }
    }
}
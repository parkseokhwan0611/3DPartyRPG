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
        if (partyMembers == null || partyMembers.Count == 0 || partyMembers[0] == null) return;

        float distance = Vector3.Distance(transform.position, partyMembers[0].transform.position);

        if (distance <= chaseDistance && !monsterMeleeAttack.isAttacking)
        {
            // 타겟을 설정하면 AttackBase.Update -> HandleAttackLogic이 돌아감
            attackModule.SetTarget(partyMembers[0].transform);
        }
        else
        {
            attackModule.SetTarget(null);
        }
    }
    void OnCollisionStay(Collision collision) 
    {
        if (collision.gameObject.CompareTag("Player")) 
        {
            rigid.velocity = Vector3.zero;
        }
    }
}
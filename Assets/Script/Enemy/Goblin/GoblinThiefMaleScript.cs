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
    [SerializeField] GameObject player;
    public float distance;
    public float chaseDistance;
    [Header("#NavMesh")]
    public NavMeshAgent navAgent;
    public float navSpeed;
    [Header("#Attack")]
    //public BoxCollider meleeArea;
    public bool isAttack;
    [Header("#Reference")]
    public Transform hudPos;
    void Awake() {
        animator = GetComponent<Animator>();
        rigid = GetComponent<Rigidbody>();
    }
    // Start is called before the first frame update
    void Start()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }
        navAgent = GetComponent<NavMeshAgent>();
    }
    // Update is called once per frame
    void Update()
    {
        if(!GameManager.instance.isLive)
            return;
        if(enemyHp.hp > 0) {
            LookAtPlayer();
        }
    }
    void FixedUpdate() {
        if (enemyHp.isDead) return;
        Targeting();
    }
    void Targeting() {
        float targetRadius = 0.4f;
        float targetRange = 0.8f;

        RaycastHit[] rayHits = Physics.SphereCastAll(transform.position, targetRadius, transform.forward, targetRange, LayerMask.GetMask("Player"));

        if(rayHits.Length > 0 && !isAttack && enemyHp.hp > 0 && !enemyHp.isDead) {
            StartCoroutine(Attack());
        }
    }
    IEnumerator Attack() {
        if (navAgent.enabled) {
            navAgent.isStopped = true;
        }
        isAttack = true;

        Vector3 direction = player.transform.position - transform.position;
        direction.y = 0; // y축 고정을 위해 수직 축은 제거
        if (direction != Vector3.zero) {
            transform.rotation = Quaternion.LookRotation(direction);
        }
        if(enemyHp.hp>0 && !enemyHp.isDead)
        {
            animator.SetTrigger("isAttack");
        }
        
        yield return new WaitForSeconds(0.6f);

        if(enemyHp.hp>0 && !enemyHp.isDead) {
            //meleeArea.enabled = true;
            animator.SetBool("isWalk", false);

            yield return new WaitForSeconds(0.1f);
            //meleeArea.enabled = false;

            yield return new WaitForSeconds(1.267f);
        }
        isAttack = false;
        navAgent.isStopped = false;
    }
    void LookAtPlayer() {
        if (navAgent.isOnNavMesh && navAgent.enabled) 
        {
            navAgent.SetDestination(player.transform.position);
        }
        // 플레이어의 위치로 바라보도록 회전
        if (player != null && !enemyHp.isDead && !isAttack && enemyHp.hp > 0 )
        {
            distance = Vector3.Distance(transform.position, player.transform.position);
            if(distance <= chaseDistance && !isAttack && !enemyHp.isDead && enemyHp.hp > 0) {
                animator.SetBool("isWalk", true);
                navAgent.speed = navSpeed;
                Vector3 direction = player.transform.position - transform.position;
                direction.y = 0; // y축 고정을 위해 수직 축은 제거
                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
                }
            }
            else if(!isAttack && distance > chaseDistance && enemyHp.hp > 0){
                animator.SetBool("isWalk", false);
                navAgent.speed = 0;
            }
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

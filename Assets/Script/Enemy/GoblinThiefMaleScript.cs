using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GoblinThiefMaleScript : MonoBehaviour
{
    public Animator animator;
    Rigidbody rigid;
    [Header("#EnemyHp")]
    public float hp;
    public float maxHp;
    public bool isAttacked = false; //평타 피격 확인
    public bool isAttackedMotion = false; //피격 모션 코루틴용 부울 변수
    public bool firstAttacked = false;
    public bool isDead = false;
    public float damage;
    public GameObject normalDamageText;
    [Header("#Player")]
    [SerializeField] GameObject player;
    public float distance;
    public float chaseDistance;
    [Header("#NavMesh")]
    public NavMeshAgent navAgent;
    public float navSpeed;
    [Header("#Attack")]
    public BoxCollider meleeArea;
    public bool isAttack;
    [Header("#Reference")]
    public Transform hudPos;
    public GameObject normalCrystal;
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
        if(hp <= 0 && !isDead) {
            hp = 0;
            animator.SetTrigger("isDead");
            isDead = true;
            StartCoroutine(DeathCor());
        }
        else if(hp > 0) {
            LookAtPlayer();
        }
    }
    void FixedUpdate() {
        if (isDead) return;
        Targeting();
    }
    void Targeting() {
        float targetRadius = 0.4f;
        float targetRange = 0.8f;

        RaycastHit[] rayHits = Physics.SphereCastAll(transform.position, targetRadius, transform.forward, targetRange, LayerMask.GetMask("Player"));

        if(rayHits.Length > 0 && !isAttack && !isAttackedMotion && hp > 0 && !isDead) {
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
        if(!isAttackedMotion && hp>0 && !isDead)
        {
            animator.SetTrigger("isAttack");
        }
        
        yield return new WaitForSeconds(0.6f);

        if(!isAttackedMotion && hp>0 && !isDead) {
            meleeArea.enabled = true;
            animator.SetBool("isWalk", false);

            yield return new WaitForSeconds(0.1f);
            meleeArea.enabled = false;

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
        if (player != null && !isDead && !isAttack && !isAttacked && hp > 0 && !isAttackedMotion)
        {
            distance = Vector3.Distance(transform.position, player.transform.position);
            if((distance <= chaseDistance || firstAttacked) && !isAttack && !isDead && hp > 0 && !isAttackedMotion) {
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
            else if(!isAttack && distance > chaseDistance && hp > 0){
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
    public void OnTriggerEnter(Collider collision) {
        if (collision.gameObject.CompareTag("Sword") && !isDead && !isAttacked)
        {
            if (!firstAttacked) firstAttacked = true;

            int isOccur = Random.Range(0, 100);
            
            if(isOccur < GameManager.instance.crit) { // Cirt
                damage = Mathf.Round(GameManager.instance.attack * (1 + GameManager.instance.crDmg / 100));
            }
            else { // No Cirt
                damage = Mathf.Round(GameManager.instance.attack);
            }
            hp -= damage;
            GameObject hudText = Instantiate(normalDamageText, hudPos.position, Quaternion.Euler(60, 0, 0));
            //hudText.GetComponent<DamageText>().damage = damage;
            isAttacked = true;
                
            if (navAgent.enabled) {
                navAgent.isStopped = true;
            }
            isAttackedMotion = true;
            //CinemachineShake.Instance.ShakeCamera(7f, .2f);
            StartCoroutine(AttackedCor(0.2f / GameManager.instance.attackSpeed));
            
            if(hp > 0) {
                animator.SetTrigger("isAttacked");
            }
        }
    }
    // public void OnTriggerStay(Collider collision)
    // {
    
    // }
    IEnumerator AttackedCor(float cool) {
        rigid.velocity = Vector3.zero;

            Vector3 direction = player.transform.position - transform.position;
            direction.y = 0;
            if (direction != Vector3.zero) {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = targetRotation; 
            }

        meleeArea.enabled = false;
        yield return new WaitForSeconds(cool);
        isAttacked = false;

        // 중첩 피격 대기 시간
        yield return new WaitForSeconds(0.5f);

        isAttackedMotion = false;
        StopCoroutine("Attack");
        navAgent.isStopped = false;
    }
    IEnumerator DeathCor() {
        if(navAgent.enabled)
        {
            navAgent.speed = 0;
            navAgent.isStopped = true;
        }
        yield return new WaitForSeconds(2f);
        GameManager.instance.exp += 5f;
        Instantiate(normalCrystal, transform.position + new Vector3(0, 0.9f, 0), Quaternion.Euler(-90, 0, 0));
        Destroy(gameObject);
    }
}

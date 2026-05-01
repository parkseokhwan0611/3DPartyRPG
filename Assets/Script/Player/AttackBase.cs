using UnityEngine;
using UnityEngine.AI;

public abstract class AttackBase : MonoBehaviour
{
    [Header("공격 설정")]
    public float attackDamage = 10f;
    public float attackRange = 2.0f;
    public float attackSpeed = 1.0f; // 초당 공격 횟수
    protected float attackCooldown = 0f;
    public float attackDuration = 1.0f;

    [Header("참조 컴포넌트")]
    protected NavMeshAgent agent;
    protected Animator anim;
    public Transform currentTarget; // 현재 타겟
    protected EnemyHp targetHealth;
    public LayerMask enemyLayer;

    protected virtual void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
    }
    protected virtual void Update()
    {
        if (attackCooldown > 0) attackCooldown -= Time.deltaTime;

        if (currentTarget != null)
        {
            // [개선] 매 프레임 GetComponent 하지 않고, 저장된 변수만 체크합니다.
            if (targetHealth != null && targetHealth.isDead)
            {
                ClearTarget(); // 타겟 초기화 로직을 따로 빼면 관리하기 편합니다.
                return;
            }

            LookAtTarget();
            HandleAttackLogic();
        }
    }
    protected virtual void HandleAttackLogic()
    {
        float distance = Vector3.Distance(transform.position, currentTarget.position);
        agent.stoppingDistance = attackRange;

        if (distance <= attackRange)
        {
            // 사거리 안: 걷기 끄고 공격 준비
            if (anim != null) anim.SetBool("isWalking", false);
            StopAndAttack();
        }
        else
        {
            // 사거리 밖: 적을 향해 이동하며 걷기 켜기
            agent.SetDestination(currentTarget.position);
            
            if (anim != null)
            {
                // [포인트] 에이전트가 이동 명령을 받았으므로 무조건 걷기 활성화
                anim.SetBool("isWalking", true);
            }
        }
    }
    protected virtual void StopAndAttack()
    {
        // 공격 지점에 왔으므로 이동 중지
        agent.ResetPath();
        
        // 공격해야 하므로 걷기 애니메이션을 확실히 끕니다.
        if (anim != null)
        {
            anim.SetBool("isWalking", false);
        }

        LookAtTarget();

        if (attackCooldown <= 0)
        {
            ExecuteAttack(); 
            attackCooldown = attackDuration / attackSpeed;
        }
    }
    // 자식들이 이 함수를 override해서 각자의 코루틴을 실행하게 합니다.
    protected virtual void ExecuteAttack()
    {
        // 기본 구현은 애니메이션만 트리거 (자식에서 덮어씌울 예정)
        anim.SetTrigger("doNormalAttack");
    }
    protected Vector3 TargetPosition
    {
        get
        {
            if (currentTarget == null) return transform.position + transform.forward;

            // 1. 적에게 "AimTarget"이라는 이름의 자식이 있는지 확인
            Transform aimPoint = currentTarget.Find("AimTarget");
            
            // 2. 있다면 그 위치를, 없다면 원래대로 적의 피벗(발바닥) 위치를 반환
            return aimPoint != null ? aimPoint.position : currentTarget.position;
        }
    }
    protected void LookAtTarget()
    {
        if (currentTarget == null) return;

        // [수정] currentTarget.position 대신 TargetPosition을 사용!
        Vector3 direction = (TargetPosition - transform.position).normalized;
        direction.y = 0; // 여전히 몸은 수평으로만 회전

        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 10f);
        }
    }
    // [핵심] 실제 데미지를 입히는 방식은 자식들이 결정함
    // 애니메이션 이벤트에서 호출될 함수입니다.
    public abstract void OnHit();

    // 타겟 설정 함수
    public void SetTarget(Transform target)
    {
        if (currentTarget == target) return;

        currentTarget = target;

        if (currentTarget != null)
        {
            // 여기서 딱 한 번만 GetComponent를 실행합니다!
            targetHealth = currentTarget.GetComponent<EnemyHp>();
        }
        else
        {
            targetHealth = null;
        }
    }
    protected void ClearTarget()
    {
        currentTarget = null;
        targetHealth = null;
    }
    public void ForceCancelAttack()
    {
        // 1. 실행 중인 모든 공격 코루틴(AttackRoutine 등)을 즉시 종료
        StopAllCoroutines();

        // 2. 타겟을 비워 공격 루프에서 탈출
        currentTarget = null;

        // 3. 애니메이션 초기화
        if (anim != null)
        {
            anim.ResetTrigger("doNormalAttack"); // 예약된 공격 트리거 삭제
            anim.SetBool("isWalking", true);    // 즉시 이동 모션으로 전환 준비
        }

        // 4. 내비게이션 정지 거리 초기화
        agent.stoppingDistance = 0.1f;
    }
}
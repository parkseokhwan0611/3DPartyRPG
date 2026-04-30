using UnityEngine;
using UnityEngine.AI;

public abstract class AttackBase : MonoBehaviour
{
    [Header("공격 설정")]
    public float attackDamage = 10f;
    public float attackRange = 2.0f;
    public float attackSpeed = 1.0f; // 초당 공격 횟수
    protected float attackCooldown = 0f;

    [Header("참조 컴포넌트")]
    protected NavMeshAgent agent;
    protected Animator anim;
    public Transform currentTarget; // 현재 타겟
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
            // [추가] 타겟이 있다면 이동 중이든 공격 중이든 항상 적을 바라봅니다.
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
            attackCooldown = 1f / attackSpeed;
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

        // 1. 타겟 방향 계산 (내 위치에서 적 위치를 뺀 벡터)
        // Vector3 direction = (currentTarget.position - transform.position).normalized;

        // // 2. Y축만 바라보게 고정 (캐릭터가 위아래로 기우는 것 방지)
        // direction.y = 0;

        // // 3. 해당 방향으로의 회전값(Quaternion) 계산
        // if (direction != Vector3.zero)
        // {
        //     Quaternion lookRotation = Quaternion.LookRotation(direction);
            
        //     // 4. 즉시 회전시키거나, 부드럽게 회전 (Slerp)
        //     // 10월 포트폴리오의 자연스러움을 위해 부드러운 회전을 추천합니다.
        //     transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 10f);
        // }
    }
    // [핵심] 실제 데미지를 입히는 방식은 자식들이 결정함
    // 애니메이션 이벤트에서 호출될 함수입니다.
    public abstract void OnHit();

    // 타겟 설정 함수
    public void SetTarget(Transform target)
    {
        currentTarget = target;
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
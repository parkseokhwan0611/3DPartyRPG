using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterMeleeAttack : AttackBase
{
    private EnemyHp enemyHp;
    [Header("근접 공격 판정 설정")]
    public float hitRadius = 1.5f;
    public float hitOffset = 1.0f;
    public bool isAttacking = false;
    [Header("타이밍 설정 (초 단위)")]
    public float damageDelay = 0.33f; // 애니메이션 시작 후 타격판정까지 걸리는 시간
    void Awake()
    {
        enemyHp = GetComponent<EnemyHp>();
    }
    protected override void Start()
    {
        base.Start();
    }
    protected override void Update()
    {
        base.Update();
    }
    // AttackBase의 StopAndAttack에서 호출됨
    protected override void ExecuteAttack()
    {
        // 이미 공격 코루틴이 돌고 있지 않을 때만 실행
        StartCoroutine(MonsterAttackRoutine());
    }

    private IEnumerator MonsterAttackRoutine()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            isAttacking = true;
        }

        // --- 추가: 공격 시작 시 플레이어를 즉시 바라봄 ---
        if (currentTarget != null)
        {
            Vector3 direction = (currentTarget.position - transform.position).normalized;
            direction.y = 0; // 수평 회전만
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
        // ------------------------------------------

        anim.SetTrigger("doNormalAttack");
        anim.SetBool("isWalking", false);

        yield return new WaitForSeconds(damageDelay / attackSpeed);
        if(enemyHp.hp > 0)
        {
            OnHit();
        }

        yield return new WaitForSeconds(attackDuration - damageDelay / attackSpeed);

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            isAttacking = false;
        }
    }
    public override void OnHit()
    {
        // 1. 판정 위치 계산 (기존 기즈모 범위용)
        Vector3 hitPos = transform.position + (transform.forward * hitOffset);
        Collider[] hitEnemies = Physics.OverlapSphere(hitPos, hitRadius, enemyLayer);

        // if (hitEnemies.Length > 0)
        // {
        //     Debug.Log($"{hitEnemies.Length}명의 대상을 감지함");
        // }
        // else
        // {
        //      Debug.Log("NotFound");
        // }

        // 3. 데미지 판정 및 로그 출력
        foreach (Collider enemy in hitEnemies)
        {
            // 1. 해당 오브젝트에서 IDamageable 인터페이스를 가져옵니다.
            IDamageable target = enemy.GetComponent<IDamageable>();

            // 2. 인터페이스가 존재한다면 데미지를 입힙니다.
            if (target != null)
            {
                // AttackBase에 정의된 attackDamage를 전달합니다.
                target.TakeDamage(attackDamage, gameObject);
            }
        }
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 hitPos = transform.position + (transform.forward * hitOffset);
        Gizmos.DrawWireSphere(hitPos, hitRadius);
    }
}
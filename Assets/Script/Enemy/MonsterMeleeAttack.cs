using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterMeleeAttack : AttackBase
{
    [Header("근접 공격 판정 설정")]
    public float hitRadius = 1.5f;
    public float hitOffset = 1.0f;
    public bool isAttacking = false;
    protected override void Start()
    {
        base.Start();
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

        yield return new WaitForSeconds(attackDuration / attackSpeed);

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            isAttacking = false;
        }
    }
    public override void OnHit()
    {
        // 실제 데미지 판정 (애니메이션 이벤트용)
        Vector3 hitPos = transform.position + (transform.forward * hitOffset);
        Collider[] hitPlayers = Physics.OverlapSphere(hitPos, hitRadius, LayerMask.GetMask("Player"));

        foreach (var playerCol in hitPlayers)
        {
            var damageable = playerCol.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(attackDamage, gameObject);
            }
        }
    }
}
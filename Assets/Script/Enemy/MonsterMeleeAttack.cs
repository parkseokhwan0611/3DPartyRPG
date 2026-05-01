using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterMeleeAttack : AttackBase
{
// 몬스터는 애니메이션 이벤트(OnHit)를 통해 플레이어에게 데미지를 줍니다.
    public override void OnHit()
    {
        // 몬스터 앞쪽 범위 체크
        Vector3 hitPos = transform.position + (transform.forward * 1.0f);
        Collider[] hitPlayers = Physics.OverlapSphere(hitPos, 1.5f, LayerMask.GetMask("Player"));

        foreach (var playerCol in hitPlayers)
        {
            var damageable = playerCol.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(attackDamage, gameObject);
            }
        }
    }

    protected override void ExecuteAttack()
    {
        // 몬스터 공격 애니메이션 실행
        anim.SetTrigger("isAttack");
    }
}

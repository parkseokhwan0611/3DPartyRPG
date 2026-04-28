using UnityEngine;
using System.Collections;

public class MeleeAttack : AttackBase
{
    [Header("근접 공격 판정 설정")]
    public float hitRadius = 1.5f;
    public float hitOffset = 1.0f;
    
    [Header("타이밍 설정 (초 단위)")]
    public float damageDelay = 0.33f; // 애니메이션 시작 후 타격판정까지 걸리는 시간
    public string hitEffectName = "Yellow Sword Slash 1";

    // 부모의 StopAndAttack을 오버라이드하여 코루틴을 실행합니다.
    protected override void Update()
    {
        base.Update();
    }

    // 부모 클래스의 공격 실행 부분을 코루틴 호출로 변경하기 위해 
    // AttackBase의 StopAndAttack 로직을 코루틴화하여 새로 정의합니다.
    // (AttackBase에서 virtual로 선언되어 있지 않다면, 부모 코드를 조금 수정하거나 
    // 아래처럼 새로 정의해서 사용할 수 있습니다.)

    public void StartAttackSequence()
    {
        if (attackCooldown <= 0)
        {
            StartCoroutine(AttackRoutine());
            attackCooldown = 1.267f / attackSpeed;
        }
    }
    private IEnumerator AttackRoutine()
    {
        // 1. 공격 준비 (정지 및 회전)
        agent.ResetPath();
        anim.SetBool("isWalking", false);
        LookAtTarget();

        // 2. 애니메이션 실행
        anim.SetTrigger("doNormalAttack");

        // 3. 판정 타이밍까지 대기 (선딜레이)
        yield return new WaitForSeconds(damageDelay);

        // 4. 실제 타격 판정 실행
        OnHit();
    }
    public override void OnHit()
    {
        // 1. 판정 위치 계산 (기존 기즈모 범위용)
        Vector3 hitPos = transform.position + (transform.forward * hitOffset);
        Collider[] hitEnemies = Physics.OverlapSphere(hitPos, hitRadius, enemyLayer);

        // 2. 적을 한 명이라도 맞췄다면 캐릭터의 0.3m 앞 위치에 이펙트 생성
        if (hitEnemies.Length > 0)
        {
            // 캐릭터 위치에서 앞방향으로 0.3만큼 더한 좌표 계산
            Vector3 effectPos = transform.position + (transform.forward * 0.3f);
            
            // 높이 조절이 필요하다면(예: 허리 높이) y값을 살짝 더해줄 수 있습니다.
            // effectPos.y += 1.0f; 

            SpawnHitEffect(effectPos);
        }

        // 3. 데미지 판정 및 로그 출력
        foreach (Collider enemy in hitEnemies)
        {
            //Debug.Log(enemy.name + " 타격!");
            // enemy.GetComponent<IDamageable>()?.TakeDamage(attackDamage);
        }
    }
    private void SpawnHitEffect(Vector3 pos)
    {
        if (ObjectPoolManager.instance != null)
        {
            var effect = ObjectPoolManager.instance.GetGo("Yellow Sword Slash 1");
            if (effect != null) effect.transform.position = pos;
        }
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 hitPos = transform.position + (transform.forward * hitOffset);
        Gizmos.DrawWireSphere(hitPos, hitRadius);
    }
    protected override void ExecuteAttack()
    {
        StartCoroutine(AttackRoutine());
    }
}
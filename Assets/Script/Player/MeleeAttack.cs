using UnityEngine;
using System.Collections;

public class MeleeAttack : AttackBase
{
    private CharacterStat myStat;
    [Header("근접 공격 판정 설정")]
    public float hitRadius = 1.5f;
    public float hitOffset = 1.0f;
    
    [Header("타이밍 설정 (초 단위)")]
    public float damageDelay = 0.33f; // 애니메이션 시작 후 타격판정까지 걸리는 시간
    public string hitEffectName = "Yellow Sword Slash 1";
    void Awake()
    {
        // 같은 오브젝트에 붙어있는 스탯 스크립트를 참조
        myStat = GetComponent<CharacterStat>();
    }
    // 부모의 StopAndAttack을 오버라이드하여 코루틴을 실행합니다.
    protected override void Update()
    {
        base.Update();
    }

    // 부모 클래스의 공격 실행 부분을 코루틴 호출로 변경하기 위해 
    // AttackBase의 StopAndAttack 로직을 코루틴화하여 새로 정의합니다.
    // (AttackBase에서 virtual로 선언되어 있지 않다면, 부모 코드를 조금 수정하거나 
    // 아래처럼 새로 정의해서 사용할 수 있습니다.)

    // public void StartAttackSequence()
    // {
    //     if (attackCooldown <= 0)
    //     {
    //         StartCoroutine(AttackRoutine());
    //         attackCooldown = 1.267f / attackSpeed;
    //     }
    // }
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
        // 0. 스탯 참조 확인
        if (myStat == null) return;

        // 1. 판정 위치 계산
        Vector3 hitPos = transform.position + (transform.forward * hitOffset);
        Collider[] hitEnemies = Physics.OverlapSphere(hitPos, hitRadius, enemyLayer);

        // 2. 이펙트 생성 (적을 한 명이라도 맞췄을 때)
        if (hitEnemies.Length > 0)
        {
            Vector3 effectPos = transform.position + (transform.forward * 0.3f);
            SpawnHitEffect(effectPos);
        }

        float damage = myStat.TotalAtk;
        bool isTargetSet = false; // UI 타겟 설정을 한 번만 하기 위한 변수

        // 3. 데미지 판정
        foreach (Collider enemy in hitEnemies)
        {
            // 최적화: 한 번만 가져와서 사용
            var enemyStat = enemy.GetComponent<EnemyHp>();
            
            if (enemyStat != null)
            {
                // 인터페이스 방식 데미지 전달
                enemyStat.TakeDamage(damage, gameObject);

                // 중앙 상단 UI 설정 (첫 번째 맞은 적만 표시)
                if (!isTargetSet && TargetHpScript.instance != null)
                {
                    TargetHpScript.instance.SetTarget(enemyStat);
                    isTargetSet = true;
                }
            }
    }
}
    private void SpawnHitEffect(Vector3 pos)
    {
        if (ObjectPoolManager.instance != null)
        {
            var effect = ObjectPoolManager.instance.GetGo("Yellow Sword Slash 1");
            if (effect != null)
            {
                // 1. 위치 설정
                effect.transform.position = pos;

                // 2. 방향 설정 (캐릭터가 바라보는 정면 방향으로 회전)
                // 만약 적을 향해 더 정확히 날리고 싶다면 currentTarget.position - transform.position을 사용하세요.
                effect.transform.rotation = transform.rotation; 
            }
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
        // 스킬 발동 로직
        StartCoroutine(AttackRoutine());
    }
}
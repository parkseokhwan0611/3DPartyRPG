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
    private Coroutine attackCoroutine;
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
    private IEnumerator AttackRoutine()
    {
        agent.ResetPath();
        anim.SetBool("isWalking", false);
        LookAtTarget();

        // 이전 트리거 초기화
        anim.ResetTrigger("doNormalAttack");

        // 애니메이터가 현재 프레임 처리를 완전히 마칠 때까지 대기
        yield return null; // 1프레임
        yield return null; // 2프레임 (확실하게)

        anim.SetTrigger("doNormalAttack");

        yield return new WaitForSeconds(damageDelay);
        OnHit();
    }
    public override void OnHit()
    {
        // 0. 스탯 참조 확인
        if (myStat == null) return;

        // 1. 판정 위치 계산
        Vector3 hitPos = transform.position + (transform.forward * hitOffset);
        Collider[] hitEnemies = Physics.OverlapSphere(hitPos, hitRadius, enemyLayer);

        // 2. 이펙트 생성 + 적중 시 체력 회복 (적을 한 명이라도 맞췄을 때)
        if (hitEnemies.Length > 0)
        {
            Vector3 effectPos = transform.position + (transform.forward * 0.3f);
            SpawnHitEffect(effectPos);

            if (myStat.HpOnHit > 0f)
                myStat.HealHp(myStat.HpOnHit);
        }

        float damage = myStat.TotalAtk * (1f + myStat.PhysDmgBonus);
        Color damageColor = myStat.GetDamageColor(); // CharacterStat에서 색상 가져옴
        bool isTargetSet = false; // UI 타겟 설정을 한 번만 하기 위한 변수

        if (Random.value <= myStat.TotalCritRate)
        {
            damage *= myStat.TotalCritDamage;
            CinemachineShake.Instance.ShakeCamera(10f, .2f);
            // 나중에 치명타 전용 색상이나 연출 추가 가능
        }

        // 3. 데미지 판정
        foreach (Collider enemy in hitEnemies)
        {
            // 최적화: 한 번만 가져와서 사용
            var enemyStat = enemy.GetComponent<EnemyHp>();
            
            if (enemyStat != null)
            {
                // 인터페이스 방식 데미지 전달
                enemyStat.TakeDamage(damage, gameObject, damageColor); // 색상 전달

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
        attackCoroutine = StartCoroutine(AttackRoutine());
    }

    protected override void StopAttackCoroutine()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
    }
}
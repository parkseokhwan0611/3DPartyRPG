using System.Collections;
using UnityEngine;

public class HealerAttack : AttackBase
{
    private CharacterStat myStat;
    [Header("Ranged Settings")]
    public string projectileName = "HealerNormalAtk"; // 풀 매니저에서 식별할 이름
    public Transform firePoint;               // 발사 위치 (지팡이 끝, 손 등)
    public float damageDelay = 0.35f;         // 애니메이션 중 발사체가 생성되는 타이밍
    void Awake()
    {
        // 같은 오브젝트에 붙어있는 스탯 스크립트를 참조
        myStat = GetComponent<CharacterStat>();
    }
    protected override void ExecuteAttack()
    {
        StartCoroutine(AttackRoutine());
    }
    private IEnumerator AttackRoutine()
    {
        if (currentTarget == null) yield break;

        // 1. 애니메이션 재생
        anim.SetTrigger("doNormalAttack");
        
        // 2. 발사 타이밍까지 대기
        yield return new WaitForSeconds(damageDelay);

        if (currentTarget == null) yield break;

        // [핵심 수정] 3. 정밀 조준 벡터 계산
        // 캐릭터의 회전(transform.forward)을 무시하고, 
        // 오직 '지팡이 끝'에서 '적의 중심'을 잇는 순수한 직선을 만듭니다.
        
        Vector3 spawnPos = (firePoint != null) ? firePoint.position : transform.position;
        
        Vector3 preciseDir = (TargetPosition - spawnPos).normalized;
        Quaternion preciseRotation = Quaternion.LookRotation(preciseDir);

        // 4. 투사체 생성
        var effect = ObjectPoolManager.instance.GetGo(projectileName);

        if (effect != null)
        {
            // 위치와 회전을 계산된 정밀 값으로 즉시 강제 세팅
            effect.transform.position = spawnPos;
            effect.transform.rotation = preciseRotation;

            float damage = myStat.TotalAp;

            // --- 데미지 데이터 설정 추가 ---
            // [수정] 스탯의 TotalAp를 가져와서 투사체에 전달
            ProjectileScript proj = effect.GetComponent<ProjectileScript>();
            if (proj != null)
            {
                // 실시간 계산된 전체 공격력(TotalAp)을 전달합니다.
                proj.SetProjectileData(myStat.TotalAp, gameObject);
            }
            // ----------------------------

            Rigidbody rb = effect.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // 물리 엔진 위치와 회전도 동기화
                rb.position = spawnPos;
                rb.rotation = preciseRotation;
                
                // 이전 속도 잔상 제거 (매우 중요)
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    // 근접 판정이 아니므로 OnHit은 비워둡니다.
    public override void OnHit() { }

    public void OnProjectileHit(EnemyHp enemyStat)
    {
        // 중앙 상단 UI 매니저 호출
        if (TargetHpScript.instance != null)
        {
            TargetHpScript.instance.SetTarget(enemyStat);
        }
    }
}

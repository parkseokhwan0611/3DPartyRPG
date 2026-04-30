using System.Collections;
using UnityEngine;

public class HealerAttack : AttackBase
{
[Header("Ranged Settings")]
    public string projectileName = "MagicBall"; // 풀 매니저에서 식별할 이름
    public Transform firePoint;               // 발사 위치 (지팡이 끝, 손 등)
    public float damageDelay = 0.35f;         // 애니메이션 중 발사체가 생성되는 타이밍

    protected override void ExecuteAttack()
    {
        StartCoroutine(AttackRoutine());
    }

private IEnumerator AttackRoutine()
    {
        if (currentTarget == null) yield break;

        // 애니메이션 재생
        anim.SetTrigger("doNormalAttack");
        
        // 발사 타이밍까지 대기
        yield return new WaitForSeconds(damageDelay);

        // 만약 firePoint를 인스펙터에서 할당하지 않았다면 
        // 캐릭터의 위치를 기본값으로 사용하도록 방어 코드를 추가합니다.
        Transform spawnPoint = (firePoint != null) ? firePoint : transform;

        // 1. 적을 향한 방향 계산 (firePoint 위치 기준)
        // 적의 가슴 높이(Vector3.up * 1.0f)를 조준합니다.
        if (currentTarget == null) yield break;
        Vector3 targetDir = (currentTarget.position + Vector3.up * 1.0f) - spawnPoint.position;
        
        
        // 투사체가 땅으로 박히거나 하늘로 솟지 않게 Y축을 고정하고 싶다면 사용 (선택 사항)
        // targetDir.y = 0; 

        if (targetDir != Vector3.zero)
        {
            // 2. 방향을 회전값(Quaternion)으로 변환
            Quaternion lookRotation = Quaternion.LookRotation(targetDir);

            // 3. 풀에서 투사체 소환
            var effect = ObjectPoolManager.instance.GetGo(projectileName);
            if (effect != null)
            {
                // 4. firePoint의 위치와 계산된 회전값을 적용
                effect.transform.position = spawnPoint.position;
                effect.transform.rotation = lookRotation;
            }
        }
    }

    // 근접 판정이 아니므로 OnHit은 비워둡니다.
    public override void OnHit() { }
}

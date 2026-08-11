using UnityEngine;

// MonsterSkillBase 구현 — 직선으로 날아가는 화살/투사체 스킬.
// MonsterRangedAttack.FireProjectile()과 동일한 발사 로직(ProjectileScript 풀링) 재사용.
public class MonsterArrowSkill : MonsterSkillBase
{
    [Header("# 화살 투사체 설정")]
    [Tooltip("ObjectPoolManager에 등록된 투사체 풀 키 (ProjectileScript 컴포넌트 포함)")]
    public string projectilePoolKey = "MonsterProjectile";
    [Tooltip("발사 위치 (없으면 자신 위치 + 1m 위)")]
    public Transform firePoint;
    public float damage = 15f;
    [Tooltip("체크하면 마법 피해(마법저항력으로 경감), 해제하면 물리 피해(방어력으로 경감)")]
    public bool isMagicDamage = false;

    public override void ExecuteSkill(Transform target)
    {
        if (target == null) return;
        if (ObjectPoolManager.instance == null || string.IsNullOrEmpty(projectilePoolKey)) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + Vector3.up;

        // AttackBase.TargetPosition과 동일하게, 타겟의 AimTarget 자식이 있으면 그쪽을 조준
        Transform aimPoint = target.Find("AimTarget");
        Vector3   targetPos = aimPoint != null ? aimPoint.position : target.position;
        Vector3   dir        = (targetPos - spawnPos).normalized;
        if (dir == Vector3.zero) dir = transform.forward;

        Quaternion rot = Quaternion.LookRotation(dir);

        var projectile = ObjectPoolManager.instance.GetGo(projectilePoolKey);
        if (projectile == null) return;

        projectile.transform.position = spawnPos;
        projectile.transform.rotation = rot;

        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position        = spawnPos;
            rb.rotation        = rot;
            rb.velocity        = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        ProjectileScript proj = projectile.GetComponent<ProjectileScript>();
        if (proj != null)
            proj.SetProjectileData(damage, gameObject, isMagicDamage);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}

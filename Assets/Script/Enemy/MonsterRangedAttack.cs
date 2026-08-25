using UnityEngine;

public class MonsterRangedAttack : MonsterAttackBase
{
    [Header("원거리 공격 설정")]
    [Tooltip("ObjectPoolManager에 등록된 투사체 풀 키")]
    public string projectileName = "MonsterProjectile";
    [Tooltip("투사체 발사 위치 (없으면 자신 위치 + 1m 위)")]
    public Transform firePoint;

    protected override void ExecuteAttackPayload()
    {
        if (currentTarget != null) FireProjectile();
    }

    // ─────────────────────────────────────────────────────────────────
    // 투사체 발사
    // ─────────────────────────────────────────────────────────────────

    private void FireProjectile()
    {
        if (ObjectPoolManager.instance == null) return;
        if (string.IsNullOrEmpty(projectileName)) return;

        Vector3 spawnPos = firePoint != null
            ? firePoint.position
            : transform.position + Vector3.up;

        // AimTarget 자식 우선으로 조준점 계산
        Vector3 targetPos = TargetPosition;
        Vector3 dir       = (targetPos - spawnPos).normalized;
        if (dir == Vector3.zero) dir = transform.forward;

        Quaternion rot = Quaternion.LookRotation(dir);

        var projectile = ObjectPoolManager.instance.GetGo(projectileName);
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

        // isMagicAttack에 따라 맞는 대상이 방어력/마법저항력으로 알아서 경감 처리
        ProjectileScript proj = projectile.GetComponent<ProjectileScript>();
        if (proj != null)
        {
            float finalDamage = RollCritDamage(attackDamage, critChance, critDamageMultiplier, out bool isCrit);
            proj.SetProjectileData(finalDamage, gameObject, isMagicAttack, isCrit);
        }
    }

    public override void OnHit() { }

    // ─────────────────────────────────────────────────────────────────
    // 에디터 기즈모
    // ─────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

using UnityEngine;

public class MonsterMeleeAttack : MonsterAttackBase
{
    private EnemyHp enemyHp;

    [Header("근접 공격 판정 설정")]
    public float hitRadius = 1.5f;
    public float hitOffset = 1.0f;

    void Awake()
    {
        enemyHp = GetComponent<EnemyHp>();
    }

    protected override void ExecuteAttackPayload()
    {
        if (enemyHp != null && enemyHp.hp > 0) OnHit();
    }

    // ─────────────────────────────────────────────────────────────────
    // 타격 판정
    // ─────────────────────────────────────────────────────────────────

    private static readonly Collider[] _hitBuffer = new Collider[16];

    public override void OnHit()
    {
        Vector3 hitPos = transform.position + (transform.forward * hitOffset);
        int hitCount = Physics.OverlapSphereNonAlloc(hitPos, hitRadius, _hitBuffer, enemyLayer);

        for (int i = 0; i < hitCount; i++)
        {
            IDamageable damageable = _hitBuffer[i].GetComponent<IDamageable>();
            if (damageable == null) continue;

            float finalDamage = RollCritDamage(attackDamage, critChance, critDamageMultiplier, out bool isCrit);

            if (isMagicAttack) damageable.TakeMagicDamage(finalDamage, gameObject, isCrit);
            else                damageable.TakeDamage(finalDamage, gameObject, isCrit);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 에디터 기즈모
    // ─────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 hitPos = transform.position + (transform.forward * hitOffset);
        Gizmos.DrawWireSphere(hitPos, hitRadius);
    }
}

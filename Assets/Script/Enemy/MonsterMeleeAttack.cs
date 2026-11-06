using UnityEngine;

public class MonsterMeleeAttack : MonsterAttackBase
{
    private EnemyHp enemyHp;

    [Header("근접 공격 판정 설정")]
    public float hitRadius = 1.5f;
    public float hitOffset = 1.0f;

    [Header("# 이펙트 (선택 — 비워두면 없음)")]
    [Tooltip("휘두르기 이펙트의 ObjectPoolManager 풀 키. 피해 판정 순간에 몬스터 앞에 한 번 생성 " +
             "(프리팹 루트에 PoolableObject 등 풀 반환 컴포넌트가 있어야 함)")]
    public string swingVfxPoolKey;
    [Tooltip("휘두르기 이펙트 위치 — 몬스터 기준 로컬 좌표 (x 오른쪽, y 위, z 앞)")]
    public Vector3 swingVfxOffset = new Vector3(0f, 1f, 1f);
    [Tooltip("휘두르기 이펙트 회전 보정 (몬스터가 보는 방향 기준, 도 단위)")]
    public Vector3 swingVfxRotation = Vector3.zero;
    [Tooltip("휘두르기 이펙트 크기 배율 (보스처럼 큰 몬스터용)")]
    public float swingVfxScale = 1f;

    [Tooltip("타격 이펙트의 풀 키. 실제로 맞은 대상마다 그 위치(몸 중심)에 생성")]
    public string hitVfxPoolKey;

    void Awake()
    {
        enemyHp = GetComponent<EnemyHp>();
    }

    protected override void ExecuteAttackPayload()
    {
        if (enemyHp == null || enemyHp.hp <= 0) return;
        SpawnSwingVfx();
        OnHit();
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

            SpawnHitVfx(_hitBuffer[i]);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 이펙트
    // ─────────────────────────────────────────────────────────────────

    private void SpawnSwingVfx()
    {
        if (string.IsNullOrEmpty(swingVfxPoolKey)) return;

        Vector3    pos = transform.TransformPoint(swingVfxOffset);
        Quaternion rot = transform.rotation * Quaternion.Euler(swingVfxRotation);
        SpawnVfx(swingVfxPoolKey, pos, rot, swingVfxScale);
    }

    private void SpawnHitVfx(Collider hit)
    {
        if (string.IsNullOrEmpty(hitVfxPoolKey) || hit == null) return;

        // 발밑이 아니라 몸 중심에 — 콜라이더가 자식이어도 맞은 콜라이더 기준
        Vector3 pos = hit.bounds.center;
        Vector3 dir = pos - transform.position; dir.y = 0f;
        Quaternion rot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : transform.rotation;
        SpawnVfx(hitVfxPoolKey, pos, rot, 1f);
    }

    private static void SpawnVfx(string key, Vector3 pos, Quaternion rot, float scale)
    {
        if (ObjectPoolManager.instance == null) return;

        var fx = ObjectPoolManager.instance.GetGo(key);
        if (fx == null) return;

        fx.transform.SetPositionAndRotation(pos, rot);
        fx.transform.localScale = Vector3.one * scale;

        // 풀에서 꺼내며 켜질 때 이전 위치에서 이미 재생을 시작했으므로, 옮긴 뒤 처음부터 다시 재생
        var ps = fx.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Clear(true);
            ps.Play(true);
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

        // 휘두르기 이펙트가 생길 위치
        if (!string.IsNullOrEmpty(swingVfxPoolKey))
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(swingVfxOffset), 0.2f);
        }
    }
}

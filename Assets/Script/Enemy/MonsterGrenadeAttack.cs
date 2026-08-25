using UnityEngine;

// 포물선을 그리며 폭탄/수류탄류 투사체를 던지는 원거리 몬스터 공격.
// MonsterRangedAttack(직선 투사체)과 동일한 골격이지만, 투사체가 물리 충돌이 아니라
// 던지는 순간의 목표 위치에 고정 착지해 범위 폭발한다는 점이 달라 별도 클래스로 분리했다.
public class MonsterGrenadeAttack : MonsterAttackBase
{
    [Header("투척 공격 설정")]
    [Tooltip("ObjectPoolManager에 등록된 수류탄 투사체 풀 키 (GrenadeProjectile 컴포넌트 포함)")]
    public string grenadePoolKey = "MonsterGrenade";
    [Tooltip("투척 시작 위치 (없으면 자신 위치 + 1m 위)")]
    public Transform throwPoint;
    [Tooltip("수류탄이 목표 지점까지 도달하는 데 걸리는 시간(초) — 포물선 궤적의 속도를 결정")]
    public float flightDuration = 0.8f;
    [Tooltip("포물선 최고 높이(m)")]
    public float arcHeight = 3f;
    [Tooltip("착지 시 폭발 판정 반경")]
    public float explosionRadius = 2.5f;
    [Tooltip("착지 지점의 실제 지면 높이를 찾기 위한 레이캐스트 대상 레이어. " +
             "비워두면(Nothing) 보정 없이 타겟의 조준점 높이 그대로 사용 (MonsterDropTable과 동일한 방식)")]
    public LayerMask groundLayer;

    protected override void ExecuteAttackPayload()
    {
        if (currentTarget != null) ThrowGrenade();
    }

    // ─────────────────────────────────────────────────────────────────
    // 투척
    // ─────────────────────────────────────────────────────────────────

    private void ThrowGrenade()
    {
        if (ObjectPoolManager.instance == null || string.IsNullOrEmpty(grenadePoolKey)) return;

        Vector3 spawnPos = throwPoint != null ? throwPoint.position : transform.position + Vector3.up;

        // 던지는 순간의 목표 위치를 그대로 착지 지점으로 고정 — 비행 중 타겟이 움직여도
        // 궤적이 흔들리지 않고, 플레이어는 착지 전에 그 자리를 피할 수 있다
        Vector3 landingPos = TargetPosition;

        // TargetPosition은 캐릭터의 조준점(가슴 높이 등)이라 그대로 쓰면 허공에서 터진 것처럼
        // 보일 수 있음 — 아래로 레이캐스트해서 실제 지면 높이에 맞춘다 (MonsterDropTable과 동일한 방식)
        if (groundLayer.value != 0)
        {
            Vector3 rayOrigin = landingPos + Vector3.up * 10f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, 30f, groundLayer, QueryTriggerInteraction.Ignore))
                landingPos = groundHit.point;
        }

        var grenadeGo = ObjectPoolManager.instance.GetGo(grenadePoolKey);
        if (grenadeGo == null) return;

        grenadeGo.transform.position = spawnPos;
        grenadeGo.transform.rotation = Quaternion.identity;

        GrenadeProjectile grenade = grenadeGo.GetComponent<GrenadeProjectile>();
        if (grenade == null)
        {
            Debug.LogWarning($"[MonsterGrenadeAttack] '{grenadePoolKey}' 프리팹에 GrenadeProjectile 컴포넌트가 없습니다.");
            return;
        }

        grenade.Launch(spawnPos, landingPos, flightDuration, arcHeight, attackDamage, explosionRadius, enemyLayer, gameObject, isMagicAttack,
            onHitTarget: null, critChance: critChance, critDamageMultiplier: critDamageMultiplier);
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

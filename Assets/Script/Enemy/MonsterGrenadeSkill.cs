using UnityEngine;

// MonsterSkillBase 구현 — 포물선을 그리며 날아가 착지 지점에서 범위 폭발하는 투척 스킬.
// MonsterGrenadeAttack.ThrowGrenade()와 동일한 투척 로직(GrenadeProjectile 풀링) 재사용.
public class MonsterGrenadeSkill : MonsterSkillBase
{
    [Header("# 투척 설정")]
    [Tooltip("ObjectPoolManager에 등록된 수류탄 투사체 풀 키 (GrenadeProjectile 컴포넌트 포함)")]
    public string grenadePoolKey = "MonsterGrenade";
    [Tooltip("투척 시작 위치 (없으면 자신 위치 + 1m 위)")]
    public Transform throwPoint;
    public float damage = 20f;
    [Tooltip("수류탄이 목표 지점까지 도달하는 데 걸리는 시간(초) — 포물선 궤적의 속도를 결정")]
    public float flightDuration = 0.8f;
    [Tooltip("포물선 최고 높이(m)")]
    public float arcHeight = 3f;
    [Tooltip("착지 시 폭발 판정 반경")]
    public float explosionRadius = 2.5f;
    [Tooltip("폭발 판정에 맞을 대상 레이어 (파티원 레이어)")]
    public LayerMask targetLayer;
    [Tooltip("착지 지점의 실제 지면 높이를 찾기 위한 레이캐스트 대상 레이어. " +
             "비워두면(Nothing) 보정 없이 타겟의 조준점 높이 그대로 사용 (MonsterDropTable과 동일한 방식)")]
    public LayerMask groundLayer;
    [Tooltip("체크하면 마법 피해(마법저항력으로 경감), 해제하면 물리 피해(방어력으로 경감)")]
    public bool isMagicDamage = false;
    [Tooltip("치명타 확률 (0~1)")]
    [Range(0f, 1f)] public float critChance = 0.1f;
    [Tooltip("치명타 시 피해 배율 (1.5 = 150%)")]
    public float critDamageMultiplier = 1.5f;

    [Header("# 인디케이터 (선택 — 비워두면 표시 안 함)")]
    [Tooltip("ObjectPoolManager에 등록한 CircleSkillIndicator 프리팹의 풀 키")]
    public string indicatorPoolKey;

    private CircleSkillIndicator _activeIndicator;
    private Vector3 _lockedLandingPos;

    // 던지는 순간(예고 시작 시점)의 목표 위치를 그대로 착지 지점으로 고정 — 비행 중 타겟이 움직여도
    // 궤적이 흔들리지 않고, 플레이어는 인디케이터를 보고 착지 전에 그 자리를 피할 수 있다
    public override void OnWindupStart(Transform target)
    {
        Vector3 landingPos = transform.position;
        if (target != null)
        {
            Transform aimPoint = target.Find("AimTarget");
            landingPos = aimPoint != null ? aimPoint.position : target.position;
        }

        if (groundLayer.value != 0)
        {
            Vector3 rayOrigin = landingPos + Vector3.up * 10f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, 30f, groundLayer, QueryTriggerInteraction.Ignore))
                landingPos = groundHit.point;
        }

        _lockedLandingPos = landingPos;

        if (string.IsNullOrEmpty(indicatorPoolKey) || ObjectPoolManager.instance == null) return;

        var go = ObjectPoolManager.instance.GetGo(indicatorPoolKey);
        if (go == null) return;

        _activeIndicator = go.GetComponent<CircleSkillIndicator>();
        if (_activeIndicator == null) return;

        _activeIndicator.Show(_lockedLandingPos, explosionRadius);
    }

    public override void OnWindupEnd()
    {
        if (_activeIndicator == null) return;
        _activeIndicator.Hide();
        _activeIndicator = null;
    }

    public override void ExecuteSkill(Transform target)
    {
        if (ObjectPoolManager.instance == null || string.IsNullOrEmpty(grenadePoolKey)) return;

        Vector3 spawnPos   = throwPoint != null ? throwPoint.position : transform.position + Vector3.up;
        Vector3 landingPos = _lockedLandingPos;

        var grenadeGo = ObjectPoolManager.instance.GetGo(grenadePoolKey);
        if (grenadeGo == null) return;

        grenadeGo.transform.position = spawnPos;
        grenadeGo.transform.rotation = Quaternion.identity;

        GrenadeProjectile grenade = grenadeGo.GetComponent<GrenadeProjectile>();
        if (grenade == null)
        {
            Debug.LogWarning($"[MonsterGrenadeSkill] '{grenadePoolKey}' 프리팹에 GrenadeProjectile 컴포넌트가 없습니다.");
            return;
        }

        grenade.Launch(spawnPos, landingPos, flightDuration, arcHeight, damage, explosionRadius, targetLayer, gameObject, isMagicDamage,
            ApplyDebuff, critChance, critDamageMultiplier);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}

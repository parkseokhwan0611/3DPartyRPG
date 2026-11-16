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

    [Header("# 착지 예고 (선택 — 몬스터 투척 스킬과 같은 방식)")]
    [Tooltip("CircleSkillIndicator 프리팹의 풀 키. 넣으면 공격 모션이 시작되는 순간 착지 지점을 고정하고 " +
             "그 자리에 폭발 범위 원을 띄운다(던지는 순간 사라짐). 비우면 예고 없이 던지는 순간의 위치로 착지")]
    public string indicatorPoolKey;

    [Header("# 디버프 (선택 — 폭발에 맞은 대상에게, 필요 없으면 None)")]
    public MonsterSkillBase.MonsterSkillDebuff debuff = MonsterSkillBase.MonsterSkillDebuff.None;
    [Tooltip("Slow 전용 감속률 (0~1, 0.3 = 30% 감속). Stun엔 사용되지 않음")]
    [Range(0f, 0.99f)] public float debuffValue = 0.3f;
    [Tooltip("디버프 지속시간(초)")]
    public float debuffDuration = 2f;

    private CircleSkillIndicator _activeIndicator;
    private bool    _hasLockedLanding;
    private Vector3 _lockedLanding;

    protected override void ExecuteAttackPayload()
    {
        HideIndicator();
        if (currentTarget != null || _hasLockedLanding) ThrowGrenade();
        _hasLockedLanding = false;
    }

    // 예고를 쓰면 공격 모션 시작 순간의 착지 지점을 고정하고 원을 띄운다 (MonsterGrenadeSkill.OnWindupStart와 같음)
    protected override void OnAttackStart()
    {
        _hasLockedLanding = false;
        if (string.IsNullOrEmpty(indicatorPoolKey) || currentTarget == null) return;

        _lockedLanding    = GetLandingPos();
        _hasLockedLanding = true;

        if (ObjectPoolManager.instance == null) return;
        var go = ObjectPoolManager.instance.GetGo(indicatorPoolKey);
        if (go == null) return;
        _activeIndicator = go.GetComponent<CircleSkillIndicator>();
        if (_activeIndicator != null) _activeIndicator.Show(_lockedLanding, explosionRadius);
    }

    protected override void OnAttackCancelled()
    {
        HideIndicator();
        _hasLockedLanding = false;
    }

    void OnDisable() => HideIndicator();

    private void HideIndicator()
    {
        if (_activeIndicator == null) return;
        _activeIndicator.Hide();
        _activeIndicator = null;
    }

    // 착지 지점 — 대상 조준점을 지면 높이로 내린 위치
    private Vector3 GetLandingPos()
    {
        Vector3 landingPos = TargetPosition;

        // TargetPosition은 캐릭터의 조준점(가슴 높이 등)이라 그대로 쓰면 허공에서 터진 것처럼
        // 보일 수 있음 — 아래로 레이캐스트해서 실제 지면 높이에 맞춘다 (MonsterDropTable과 동일한 방식)
        if (groundLayer.value != 0)
        {
            Vector3 rayOrigin = landingPos + Vector3.up * 10f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, 30f, groundLayer, QueryTriggerInteraction.Ignore))
                landingPos = groundHit.point;
        }
        return landingPos;
    }

    // ─────────────────────────────────────────────────────────────────
    // 투척
    // ─────────────────────────────────────────────────────────────────

    private void ThrowGrenade()
    {
        if (ObjectPoolManager.instance == null || string.IsNullOrEmpty(grenadePoolKey)) return;

        Vector3 spawnPos = throwPoint != null ? throwPoint.position : transform.position + Vector3.up;

        // 착지 지점 고정 — 예고를 쓰면 모션 시작 때 고정한 자리, 아니면 던지는 순간의 목표 위치.
        // 비행 중 타겟이 움직여도 궤적이 흔들리지 않고, 플레이어는 착지 전에 그 자리를 피할 수 있다
        Vector3 landingPos = _hasLockedLanding ? _lockedLanding : GetLandingPos();

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

        System.Action<GameObject> onHit = debuff != MonsterSkillBase.MonsterSkillDebuff.None ? ApplyDebuffTo : null;
        grenade.Launch(spawnPos, landingPos, flightDuration, arcHeight, attackDamage, explosionRadius, enemyLayer, gameObject, isMagicAttack,
            onHitTarget: onHit, critChance: critChance, critDamageMultiplier: critDamageMultiplier);
    }

    // 폭발에 맞은 대상에게 디버프 — 수류탄이 날아가는 동안 몬스터가 죽어 파괴됐으면 건너뜀
    private void ApplyDebuffTo(GameObject target)
    {
        if (this == null) return;
        MonsterSkillBase.ApplyMonsterDebuff(gameObject, target, debuff, debuffValue, debuffDuration);
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

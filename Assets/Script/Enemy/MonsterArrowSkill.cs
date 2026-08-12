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

    [Header("# 인디케이터 (선택 — 비워두면 표시 안 함)")]
    [Tooltip("ObjectPoolManager에 등록한 LineSkillIndicator 프리팹의 풀 키")]
    public string indicatorPoolKey;
    [Tooltip("인디케이터/판정 라인의 폭 (월드 단위)")]
    public float indicatorWidth = 1.5f;

    private LineSkillIndicator _activeIndicator;
    private Vector3 _lockedSpawnPos;
    private Vector3 _lockedDir;

    // 예고 시작 시점에 조준 방향을 고정 — 실행 시점에 타겟의 최신 위치로 다시 조준하면
    // 인디케이터가 보여준 궤적과 실제 발사 방향이 어긋나(플레이어가 이동한 경우) 회피가 무의미해진다
    public override void OnWindupStart(Transform target)
    {
        _lockedSpawnPos = firePoint != null ? firePoint.position : transform.position + Vector3.up;

        Vector3 targetPos = _lockedSpawnPos + transform.forward * range;
        if (target != null)
        {
            Transform aimPoint = target.Find("AimTarget");
            targetPos = aimPoint != null ? aimPoint.position : target.position;
        }

        _lockedDir = (targetPos - _lockedSpawnPos).normalized;
        if (_lockedDir == Vector3.zero) _lockedDir = transform.forward;

        if (string.IsNullOrEmpty(indicatorPoolKey) || ObjectPoolManager.instance == null) return;

        var go = ObjectPoolManager.instance.GetGo(indicatorPoolKey);
        if (go == null) return;

        _activeIndicator = go.GetComponent<LineSkillIndicator>();
        if (_activeIndicator == null) return;

        _activeIndicator.Show(_lockedSpawnPos, _lockedDir, range, indicatorWidth);
    }

    public override void OnWindupEnd()
    {
        if (_activeIndicator == null) return;
        _activeIndicator.Hide();
        _activeIndicator = null;
    }

    public override void ExecuteSkill(Transform target)
    {
        if (ObjectPoolManager.instance == null || string.IsNullOrEmpty(projectilePoolKey)) return;

        Vector3 spawnPos = _lockedSpawnPos;
        Quaternion rot   = Quaternion.LookRotation(_lockedDir);

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

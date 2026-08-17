using System.Collections;
using UnityEngine;

// MonsterSkillBase 예시 구현 — 타겟 위치를 중심으로 범위 피해를 준다.
// 소환 등 다른 효과가 필요하면 MonsterSkillBase를 상속하는 별도 스크립트를 새로 만들어서
// EliteMonsterSkillController(또는 보스용 컨트롤러)의 skill 슬롯에 갈아끼우면 된다.
public class MonsterAoeSkill : MonsterSkillBase
{
    [Header("# 범위 피해 설정")]
    public float damage = 20f;
    public float radius = 3f;
    public LayerMask targetLayer;
    public bool isMagicDamage = false;
    [Tooltip("ObjectPoolManager에 등록한 이펙트 풀 키 (선택 — 비워두면 이펙트 없이 피해만 적용)")]
    public string effectPoolKey;
    [Tooltip("이펙트 스폰 위치의 Y축 보정(m) — 데미지 판정 중심(지면 높이)에는 영향 없이 " +
             "이펙트 비주얼만 위/아래로 띄운다. 프리팹 자체의 Y 위치는 스폰 시 덮어써지므로 무시됨")]
    public float effectHeightOffset = 0f;

    [Header("# 인디케이터 (선택 — 비워두면 표시 안 함)")]
    [Tooltip("ObjectPoolManager에 등록한 CircleSkillIndicator 프리팹의 풀 키")]
    public string indicatorPoolKey;

    private readonly Collider[] _hitBuffer = new Collider[16];
    private CircleSkillIndicator _activeIndicator;
    private Vector3 _lockedCenter;
    private Coroutine _damageRoutine;

    // 예고 시작 시점의 위치를 고정 — 실행 시점에 타겟의 최신 위치를 다시 읽으면 인디케이터가
    // 보여준 곳과 실제 판정 위치가 어긋나(플레이어가 이동한 경우) 회피가 무의미해진다
    public override void OnWindupStart(Transform target)
    {
        _lockedCenter = target != null ? target.position : transform.position;

        if (string.IsNullOrEmpty(indicatorPoolKey) || ObjectPoolManager.instance == null) return;

        var go = ObjectPoolManager.instance.GetGo(indicatorPoolKey);
        if (go == null) return;

        _activeIndicator = go.GetComponent<CircleSkillIndicator>();
        if (_activeIndicator == null) return;

        _activeIndicator.Show(_lockedCenter, radius);
    }

    public override void OnWindupEnd()
    {
        if (_activeIndicator == null) return;
        _activeIndicator.Hide();
        _activeIndicator = null;
    }

    // 시전 이펙트는 여기서 즉시 스폰하되, 실제 데미지 판정은 attackDuration만큼 늦춰서 적용한다 —
    // 플레이어 스킬(DamageSkill의 effectSpawnDelay/SkillZone)처럼 "이펙트가 먼저 보이고 데미지는 나중에"
    // 패턴을 맞추기 위함. attackDuration이 0이면 기존처럼 즉시 판정
    public override void ExecuteSkill(Transform target)
    {
        Vector3 center = _lockedCenter;

        if (!string.IsNullOrEmpty(effectPoolKey) && ObjectPoolManager.instance != null)
        {
            var vfx = ObjectPoolManager.instance.GetGo(effectPoolKey);
            if (vfx != null)
                vfx.transform.SetPositionAndRotation(center + Vector3.up * effectHeightOffset, Quaternion.identity);
        }

        if (attackDuration > 0f)
            _damageRoutine = StartCoroutine(ApplyDamageAfterDelay(center, attackDuration));
        else
            ApplyDamage(center);
    }

    // 몬스터가 죽거나 스턴당해 시전이 강제 취소되면, 아직 안 터진 지연 데미지도 함께 취소
    public override void OnForceCancelled()
    {
        if (_damageRoutine == null) return;
        StopCoroutine(_damageRoutine);
        _damageRoutine = null;
    }

    private IEnumerator ApplyDamageAfterDelay(Vector3 center, float delay)
    {
        yield return new WaitForSeconds(delay);
        _damageRoutine = null;
        ApplyDamage(center);
    }

    private void ApplyDamage(Vector3 center)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(center, radius, _hitBuffer, targetLayer);
        for (int i = 0; i < hitCount; i++)
        {
            IDamageable damageable = _hitBuffer[i].GetComponent<IDamageable>();
            if (damageable == null) continue;

            if (isMagicDamage) damageable.TakeMagicDamage(damage, gameObject);
            else                damageable.TakeDamage(damage, gameObject);

            ApplyDebuff(_hitBuffer[i].gameObject);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}

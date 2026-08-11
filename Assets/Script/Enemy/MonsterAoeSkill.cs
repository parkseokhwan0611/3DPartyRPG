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

    private readonly Collider[] _hitBuffer = new Collider[16];

    public override void ExecuteSkill(Transform target)
    {
        if (target == null) return;
        Vector3 center = target.position;

        if (!string.IsNullOrEmpty(effectPoolKey) && ObjectPoolManager.instance != null)
        {
            var vfx = ObjectPoolManager.instance.GetGo(effectPoolKey);
            if (vfx != null) vfx.transform.SetPositionAndRotation(center, Quaternion.identity);
        }

        int hitCount = Physics.OverlapSphereNonAlloc(center, radius, _hitBuffer, targetLayer);
        for (int i = 0; i < hitCount; i++)
        {
            IDamageable damageable = _hitBuffer[i].GetComponent<IDamageable>();
            if (damageable == null) continue;

            if (isMagicDamage) damageable.TakeMagicDamage(damage, gameObject);
            else                damageable.TakeDamage(damage, gameObject);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}

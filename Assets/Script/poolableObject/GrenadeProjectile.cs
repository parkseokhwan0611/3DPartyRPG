using UnityEngine;

// 포물선을 그리며 목표 지점까지 날아간 뒤 착지 시 범위 폭발하는 투사체.
// Rigidbody 물리 없이 매 프레임 시작점→착지점을 보간하고, 그 위에 포물선 높이값만 더해서
// 이동시킨다 — 던지는 순간의 목표 위치에 항상 정확히 착지한다 (MonsterGrenadeAttack에서 발사).
public class GrenadeProjectile : PoolAble
{
    [Tooltip("착지 시 재생할 폭발 이펙트의 ObjectPoolManager 풀 키 (비워두면 이펙트 없이 판정만)")]
    public string explosionPoolKey = "GrenadeExplosion";
    [Tooltip("착지 시 재생할 AudioManager SFX 키 (비워두면 재생 안 함)")]
    public string explosionSfxKey;
    [Tooltip("폭발 이펙트 스폰 위치의 Y축 보정(m) — 데미지 판정 중심(착지 지점)에는 영향 없이 " +
             "이펙트 비주얼만 위/아래로 띄운다. 프리팹 자체의 Y 위치는 스폰 시 덮어써지므로 무시됨")]
    public float explosionHeightOffset = 0f;

    private Vector3 _start;
    private Vector3 _landing;
    private float   _duration;
    private float   _arcHeight;
    private float   _damage;
    private float   _explosionRadius;
    private LayerMask _targetLayer;
    private GameObject _owner;
    private bool _isMagicDamage;
    private float _elapsed;
    private bool  _launched;
    private System.Action<GameObject> _onHitTarget; // 폭발 판정에 맞은 대상마다 호출 (몬스터 스킬 디버프용)

    private static readonly Collider[] _hitBuffer = new Collider[16];

    public void Launch(Vector3 start, Vector3 landingPos, float duration, float arcHeight,
        float damage, float explosionRadius, LayerMask targetLayer, GameObject owner, bool isMagicDamage = false,
        System.Action<GameObject> onHitTarget = null)
    {
        _start           = start;
        _landing         = landingPos;
        _duration        = Mathf.Max(0.01f, duration);
        _arcHeight       = arcHeight;
        _damage          = damage;
        _explosionRadius = explosionRadius;
        _targetLayer     = targetLayer;
        _owner           = owner;
        _isMagicDamage   = isMagicDamage;
        _onHitTarget     = onHitTarget;
        _elapsed         = 0f;
        _launched        = true;

        transform.position = start;
    }

    void Update()
    {
        if (!_launched) return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        Vector3 pos = Vector3.Lerp(_start, _landing, t);
        pos.y += _arcHeight * 4f * t * (1f - t); // t=0.5(중간 지점)에서 최고 높이, 양 끝에서 0
        transform.position = pos;

        if (t >= 1f)
            Explode();
    }

    private void Explode()
    {
        _launched = false;

        if (!string.IsNullOrEmpty(explosionPoolKey) && ObjectPoolManager.instance != null)
        {
            var vfx = ObjectPoolManager.instance.GetGo(explosionPoolKey);
            if (vfx != null)
            {
                vfx.transform.position = _landing + Vector3.up * explosionHeightOffset;
                vfx.transform.rotation = Quaternion.identity;
            }
        }

        if (!string.IsNullOrEmpty(explosionSfxKey))
            AudioManager.instance?.PlaySFX(explosionSfxKey);

        int hitCount = Physics.OverlapSphereNonAlloc(_landing, _explosionRadius, _hitBuffer, _targetLayer);
        for (int i = 0; i < hitCount; i++)
        {
            IDamageable damageable = _hitBuffer[i].GetComponent<IDamageable>();
            if (damageable == null) continue;

            if (_isMagicDamage) damageable.TakeMagicDamage(_damage, _owner);
            else                 damageable.TakeDamage(_damage, _owner);

            _onHitTarget?.Invoke(_hitBuffer[i].gameObject);
        }

        ReleaseObject();
    }

    private void OnDrawGizmosSelected()
    {
        if (!_launched) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_landing, _explosionRadius);
    }
}

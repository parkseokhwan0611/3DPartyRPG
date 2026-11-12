using UnityEngine;
using System.Collections;
using System;

public class ProjectileScript : PoolAble
{
    [SerializeField] protected float destroyTime = 3f;
    [SerializeField] protected float speed = 15f;
    [SerializeField] protected float hitOffset = 0f;
    [SerializeField] protected bool UseFirePointRotation;
    [SerializeField] protected Vector3 rotationOffset = Vector3.zero;
    [SerializeField] protected GameObject hit;
    [SerializeField] protected ParticleSystem hitPS;
    [SerializeField] protected GameObject flash;
    [SerializeField] protected Rigidbody rb;
    [SerializeField] protected Collider col;
    [SerializeField] protected Light lightSourse;
    [SerializeField] protected GameObject[] Detached;
    [SerializeField] protected ParticleSystem projectilePS;

    [Header("총구 섬광 (선택)")]
    [Tooltip("발사 순간 발사 위치(총구)에 띄울 이펙트 프리팹 — 건슬링어 평타 등.\n" +
             "hit과 같은 방식으로 ObjectPoolManager에 프리팹 이름과 같은 키로 등록해야 하고,\n" +
             "프리팹 루트에 PoolableObject(destroyTime = 섬광 길이)를 붙여야 풀로 돌아감.\n" +
             "위의 flash는 에셋 원본 투사체의 자식 오브젝트용이라 이것과 별개")]
    [SerializeField] protected GameObject muzzleFlash;

    // 맞았을 때 데미지를 누가 주는지
    public enum HitDamageMode
    {
        Projectile, // 투사체에 직접 맞은 대상 하나만 (기존 방식)
        Explosion,  // 맞은 지점의 폭발 범위 안 대상 전부 (직접 맞은 대상도 범위 안이면 포함)
        Both,       // 직접 맞은 대상 + 폭발 범위 안 나머지 대상 (직접 맞은 대상은 한 번만)
    }

    [Header("맞으면 폭발 (선택)")]
    [Tooltip("체크하면 맞는 순간 맞은 지점에 폭발 이펙트를 띄운다.\n" +
             "데미지 방식은 아래 Explosion Damage Mode — 단, 데미지 스킬로 쏠 때는 스킬 데이터의 설정이 우선")]
    [SerializeField] protected bool explodeOnHit;
    [Tooltip("폭발 이펙트 풀 키 (비우면 이펙트 없이 판정만). 프리팹에 PoolableObject(destroyTime)를 붙여야 풀로 돌아감")]
    [SerializeField] protected string explosionPoolKey;
    [Tooltip("폭발 시 재생할 AudioManager SFX 키 (비우면 없음)")]
    [SerializeField] protected string explosionSfxKey;
    [Tooltip("폭발 범위 반경(m) — 데미지 스킬로 쏠 때는 스킬 범위(Base Range)를 대신 사용")]
    [SerializeField] protected float explosionRadius = 2f;
    [Tooltip("폭발 이펙트 Y 위치 보정(m) — 판정 중심에는 영향 없음")]
    [SerializeField] protected float explosionHeightOffset = 0f;
    [Tooltip("평타·몬스터처럼 스킬 데이터 없이 쏠 때의 데미지 방식")]
    [SerializeField] protected HitDamageMode explosionDamageMode = HitDamageMode.Explosion;

    // 데미지 스킬이 발사마다 지정하는 값 (SetProjectileData가 매번 초기화 — 풀에서 재사용돼도 이전 스킬 설정이 남지 않게)
    private bool           _hasHitConfig;
    private HitDamageMode  _configMode;
    private float          _configRadius;
    private float          _debuffBonusPerStack; // 처형형 — 맞는 적의 디버프 1개당 데미지 증가 (스킬만, SetProjectileData가 0으로 초기화)
    private int            _maxDebuffStacks;
    // 쏜 쪽이 몬스터인지 — 발사 시점에 정해 둔다 (폭발 전에 쏜 몬스터가 죽어 사라져도 폭발이 아군 몬스터를 때리지 않게)
    private bool           _ownerIsMonster;

    private static readonly Collider[] _explosionBuffer = new Collider[32];

    public float damage;
    public GameObject owner;

    private Action<EnemyHp> onHitCallback;
    private Action<GameObject> onHitTargetCallback; // EnemyHp 유무와 무관하게 실제 데미지가 들어간 대상을 알려줌 (몬스터 스킬 디버프용)
    private bool isMagicDamage = false; // 물리/마법 판정 — 맞는 대상이 알아서 방어력/마법저항력으로 경감하고 자기 색으로 표시
    private bool isCrit = false; // 발사 시점에 미리 굴려서 들고 있다가, 맞는 순간 데미지 텍스트에 반영

    private Coroutine disableCoroutine;
    // 이미 무언가에 맞았는지 — 콜라이더를 꺼도 같은 물리 프레임에 대기 중이던 충돌은 또 들어오므로 직접 막는다
    private bool _hasHit;

    // ─────────────────────────────────────────────────────────────────
    // 초기화
    // ─────────────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (col == null) col = GetComponent<Collider>();
        if (hit != null && hitPS == null) hitPS = hit.GetComponent<ParticleSystem>();
    }

    protected virtual void OnEnable()
    {
        if (rb != null)
        {
            rb.velocity        = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints     = RigidbodyConstraints.None;
            rb.WakeUp();
        }

        if (col != null) col.enabled = true;
        if (lightSourse != null) lightSourse.enabled = true;

        _hasHit = false;
        ResetProjectile();

        if (disableCoroutine != null) StopCoroutine(disableCoroutine);
        disableCoroutine = StartCoroutine(DisableTimer(destroyTime));
    }

    private void ResetProjectile()
    {
        if (rb != null) rb.constraints = RigidbodyConstraints.None;
        if (hit != null) hit.SetActive(false);

        if (projectilePS != null)
        {
            projectilePS.Clear();
            projectilePS.Play();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 데이터 설정
    // ─────────────────────────────────────────────────────────────────

    // 콜백 없는 버전 (기존 호환용) — isMagic 생략 시 물리로 취급
    public void SetProjectileData(float dmg, GameObject attacker, bool isMagic = false, bool crit = false)
    {
        _hasHitConfig = false;
        _debuffBonusPerStack = 0f;
        _ownerIsMonster = attacker != null && attacker.GetComponentInParent<EnemyHp>() != null;
        damage        = dmg;
        owner         = attacker;
        isMagicDamage = isMagic;
        isCrit        = crit;
        onHitCallback = null;
        onHitTargetCallback = null;
    }

    // 콜백 받는 버전 (파티원 원거리 평타 RangedAttack에서 사용)
    public void SetProjectileData(float dmg, GameObject attacker, Action<EnemyHp> hitCallback, bool isMagic, bool crit = false)
    {
        _hasHitConfig = false;
        _debuffBonusPerStack = 0f;
        _ownerIsMonster = attacker != null && attacker.GetComponentInParent<EnemyHp>() != null;
        damage        = dmg;
        owner         = attacker;
        onHitCallback = hitCallback;
        isMagicDamage = isMagic;
        isCrit        = crit;
        onHitTargetCallback = null;
    }

    // 맞은 대상이 EnemyHp가 아니어도(플레이어 파티원 등) 실제로 데미지가 적용됐을 때 호출되는
    // 범용 콜백 버전 — 몬스터 스킬(MonsterArrowSkill)이 맞은 대상에게 디버프를 걸 때 사용
    public void SetProjectileData(float dmg, GameObject attacker, bool isMagic, Action<GameObject> onAnyHit, bool crit = false)
    {
        _hasHitConfig = false;
        _debuffBonusPerStack = 0f;
        _ownerIsMonster = attacker != null && attacker.GetComponentInParent<EnemyHp>() != null;
        damage        = dmg;
        owner         = attacker;
        isMagicDamage = isMagic;
        isCrit        = crit;
        onHitCallback = null;
        onHitTargetCallback = onAnyHit;
    }

    // 데미지 스킬이 SetProjectileData 다음에 호출 — 이번 발의 데미지 방식과 폭발 반경을 스킬 데이터대로 지정.
    // Projectile이 아니면 프리팹의 explodeOnHit이 꺼져 있어도 폭발 판정을 한다 (이펙트는 explosionPoolKey가 있을 때만)
    public void ConfigureHitDamage(HitDamageMode mode, float radius, float debuffBonusPerStack = 0f, int maxDebuffStacks = 0)
    {
        _hasHitConfig        = true;
        _configMode          = mode;
        _configRadius        = radius;
        _debuffBonusPerStack = debuffBonusPerStack;
        _maxDebuffStacks     = maxDebuffStacks;
    }

    private HitDamageMode EffectiveMode
        => _hasHitConfig ? _configMode : (explodeOnHit ? explosionDamageMode : HitDamageMode.Projectile);
    private float EffectiveRadius => _hasHitConfig ? _configRadius : explosionRadius;

    // 발사하는 쪽이 투사체 위치·방향을 잡은 뒤 호출 — OnEnable은 풀에서 꺼내는 순간(위치 지정 전)이라
    // 거기서 띄우면 이전 위치에 섬광이 뜬다
    public void SpawnMuzzleFlash(Vector3 position, Quaternion rotation)
    {
        if (muzzleFlash == null || ObjectPoolManager.instance == null) return;

        var flashGo = ObjectPoolManager.instance.GetGo(muzzleFlash.name);
        if (flashGo == null) return;

        flashGo.transform.SetPositionAndRotation(position, rotation);

        // 풀에서 꺼내며 켜질 때 이전 위치에서 이미 재생을 시작했으므로, 옮긴 뒤 처음부터 다시 재생
        var ps = flashGo.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Clear(true);
            ps.Play(true);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 물리 / 충돌
    // ─────────────────────────────────────────────────────────────────

    protected virtual void FixedUpdate()
    {
        if (speed != 0 && rb != null)
            rb.velocity = transform.forward * speed;
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        // 한 발은 한 번만 맞는다 (겹쳐 있는 대상 여럿과 같은 프레임에 충돌해도 데미지는 처음 하나에게만)
        if (_hasHit) return;
        _hasHit = true;

        // 1. 데미지 처리 — 물리/마법 여부에 따라 맞는 대상이 알아서 경감·색상 처리
        // 맞은 콜라이더가 자식(무기·몸 부위 등)이어도 본체에 데미지 — 자식만 보면 데미지 없이 투사체만 사라졌음
        // 맞으면 폭발 옵션 — Projectile: 직접 맞은 대상만 / Explosion: 폭발 범위만 / Both: 둘 다 (직접 맞은 대상은 한 번만)
        HitDamageMode mode = EffectiveMode;
        bool explode = explodeOnHit || mode != HitDamageMode.Projectile;

        IDamageable directTarget = null;
        if (mode != HitDamageMode.Explosion)
            directTarget = DamageTarget(collision.collider);

        if (explode)
        {
            Vector3 center = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Explode(center, mode != HitDamageMode.Projectile, directTarget);
        }

        // 2. 투사체 물리 정지
        if (rb != null) rb.constraints = RigidbodyConstraints.FreezeAll;
        if (lightSourse != null) lightSourse.enabled = false;
        if (col != null) col.enabled = false;

        if (projectilePS != null)
            projectilePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 3. 히트 이펙트 스폰
        if (hit != null && ObjectPoolManager.instance != null && collision.contactCount > 0)
        {
            ContactPoint contact = collision.GetContact(0); // contacts는 호출마다 배열을 새로 만들어서 GetContact 사용
            Vector3 hitPos       = contact.point + contact.normal * hitOffset;

            var hitEffect = ObjectPoolManager.instance.GetGo(hit.name);
            if (hitEffect != null)
            {
                hitEffect.transform.position = hitPos;

                if (UseFirePointRotation)
                    hitEffect.transform.rotation = transform.rotation * Quaternion.Euler(0, 180f, 0);
                else if (rotationOffset != Vector3.zero)
                    hitEffect.transform.rotation = Quaternion.Euler(rotationOffset);
                else
                    hitEffect.transform.LookAt(contact.point + contact.normal);

                hitEffect.SetActive(true);

                var ps = hitEffect.GetComponent<ParticleSystem>();
                if (ps != null) ps.Play();
            }
        }

        // 4. 투사체 반환
        if (disableCoroutine != null) StopCoroutine(disableCoroutine);
        disableCoroutine = StartCoroutine(DisableTimer(0.2f));
    }

    // 콜라이더의 주인(EnemyHp / IDamageable)에게 투사체 데미지를 주고 콜백 호출. 맞은 대상이 없으면 null
    // (맞은 콜라이더가 자식(무기·몸 부위 등)이어도 본체에 데미지)
    private IDamageable DamageTarget(Collider hitCollider)
    {
        GameObject hitTarget = null;
        IDamageable damaged  = null;

        EnemyHp enemyStat = hitCollider.GetComponentInParent<EnemyHp>();
        if (enemyStat != null)
        {
            float dmg = damage * enemyStat.DebuffDamageMultiplier(_debuffBonusPerStack, _maxDebuffStacks);
            if (isMagicDamage) enemyStat.TakeMagicDamage(dmg, owner, isCrit);
            else                enemyStat.TakeDamage(dmg, owner, isCrit);
            onHitCallback?.Invoke(enemyStat);
            hitTarget = enemyStat.gameObject;
            damaged   = enemyStat;
        }
        else
        {
            // EnemyHp가 없어도 IDamageable이면 데미지는 줌 (예: 플레이어가 맞은 몬스터 투사체)
            IDamageable target = hitCollider.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                if (isMagicDamage) target.TakeMagicDamage(damage, owner, isCrit);
                else                target.TakeDamage(damage, owner, isCrit);
                hitTarget = target is Component c ? c.gameObject : hitCollider.gameObject;
                damaged   = target;
            }
        }

        if (hitTarget != null)
            onHitTargetCallback?.Invoke(hitTarget);
        return damaged;
    }

    // 폭발 — 이펙트·효과음 후, dealDamage면 반경 안의 상대편 전부에게 같은 데미지(치명타 여부는 발사 때 굴린 값 그대로).
    // 상대편: 쏜 쪽이 몬스터(EnemyHp)면 Player 레이어, 아니면 Enemy 레이어. alreadyHit은 직접 맞아 이미 데미지를 받은 대상
    private void Explode(Vector3 center, bool dealDamage, IDamageable alreadyHit)
    {
        if (!string.IsNullOrEmpty(explosionPoolKey) && ObjectPoolManager.instance != null)
        {
            var vfx = ObjectPoolManager.instance.GetGo(explosionPoolKey);
            if (vfx != null)
                vfx.transform.SetPositionAndRotation(center + Vector3.up * explosionHeightOffset, Quaternion.identity);
        }

        if (!string.IsNullOrEmpty(explosionSfxKey))
            AudioManager.instance?.PlaySFX(explosionSfxKey);

        if (!dealDamage) return;

        // 몬스터 폭발은 파티원(Player)과 소환수(Summon) 모두 맞는다
        int  mask        = _ownerIsMonster ? LayerMask.GetMask("Player", "Summon") : LayerMask.GetMask("Enemy");
        int  count       = Physics.OverlapSphereNonAlloc(center, EffectiveRadius, _explosionBuffer, mask);

        var damagedSet = new System.Collections.Generic.HashSet<IDamageable>();
        if (alreadyHit != null) damagedSet.Add(alreadyHit);

        for (int i = 0; i < count; i++)
        {
            // 같은 대상의 콜라이더가 여러 개여도 한 번만
            IDamageable target = _explosionBuffer[i].GetComponentInParent<IDamageable>();
            if (target == null || !damagedSet.Add(target)) continue;
            DamageTarget(_explosionBuffer[i]);
        }
    }

    protected virtual IEnumerator DisableTimer(float time)
    {
        yield return new WaitForSeconds(time);
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (gameObject.activeSelf)
            Pool.Release(gameObject);
    }
}
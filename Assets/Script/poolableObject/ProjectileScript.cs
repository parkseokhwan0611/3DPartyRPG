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

    public float damage;
    public GameObject owner;

    private Action<EnemyHp> onHitCallback;
    private bool isMagicDamage = false; // 물리/마법 판정 — 맞는 대상이 알아서 방어력/마법저항력으로 경감하고 자기 색으로 표시

    private Coroutine disableCoroutine;

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
    public void SetProjectileData(float dmg, GameObject attacker, bool isMagic = false)
    {
        damage        = dmg;
        owner         = attacker;
        isMagicDamage = isMagic;
        onHitCallback = null;
    }

    // 콜백 받는 버전 (HealerAttack, RangedAttack에서 사용)
    public void SetProjectileData(float dmg, GameObject attacker, Action<EnemyHp> hitCallback, bool isMagic)
    {
        damage        = dmg;
        owner         = attacker;
        onHitCallback = hitCallback;
        isMagicDamage = isMagic;
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
        // 1. 데미지 처리 — 물리/마법 여부에 따라 맞는 대상이 알아서 경감·색상 처리
        EnemyHp enemyStat = collision.gameObject.GetComponent<EnemyHp>();
        if (enemyStat != null)
        {
            if (isMagicDamage) enemyStat.TakeMagicDamage(damage, owner);
            else                enemyStat.TakeDamage(damage, owner);
            onHitCallback?.Invoke(enemyStat);
        }
        else
        {
            // EnemyHp가 없어도 IDamageable이면 데미지는 줌 (예: 플레이어가 맞은 몬스터 투사체)
            IDamageable target = collision.gameObject.GetComponent<IDamageable>();
            if (target != null)
            {
                if (isMagicDamage) target.TakeMagicDamage(damage, owner);
                else                target.TakeDamage(damage, owner);
            }
        }

        // 2. 투사체 물리 정지
        if (rb != null) rb.constraints = RigidbodyConstraints.FreezeAll;
        if (lightSourse != null) lightSourse.enabled = false;
        if (col != null) col.enabled = false;

        if (projectilePS != null)
            projectilePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 3. 히트 이펙트 스폰
        if (hit != null && ObjectPoolManager.instance != null)
        {
            ContactPoint contact = collision.contacts[0];
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
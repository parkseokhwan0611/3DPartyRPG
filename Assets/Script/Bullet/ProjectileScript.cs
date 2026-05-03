using UnityEngine;
using System.Collections;

public class ProjectileScript : PoolAble
{
    [SerializeField] protected float destroyTime = 3f; // 풀로 돌아갈 시간
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
    public float damage;       // 투사체가 입힐 데미지
    public GameObject owner;
    
    private Coroutine disableCoroutine; // 코루틴 중복 방지용

    protected virtual void Awake()
    {
        // 컴포넌트 참조는 Awake에서 한 번만 합니다.
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (col == null) col = GetComponent<Collider>();
        if (hit != null && hitPS == null) hitPS = hit.GetComponent<ParticleSystem>();
    }
    protected virtual void OnEnable()
    {
        // 1. 물리 및 콜라이더 즉시 초기화
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.None; // 고정 해제
            rb.WakeUp(); // 물리 엔진 깨우기
        }

        if (col != null) 
        {
            col.enabled = true; // [핵심] 여기서 확실히 켜줍니다.
        }

        // 2. 비주얼 초기화
        ResetProjectile();

        // 3. 반환 타이머 리셋
        if (disableCoroutine != null) StopCoroutine(disableCoroutine);
        disableCoroutine = StartCoroutine(DisableTimer(destroyTime));
    }
    private void ResetProjectile()
    {
        if (rb != null)
        {
            // 이전 충돌 때 걸어잠근 모든 축 고정을 해제합니다.
            // 이게 없으면 생성된 자리에 '얼음' 상태로 멈춰있게 됩니다.
            rb.constraints = RigidbodyConstraints.None; 
        }
        // 히트 이펙트가 자식이라면 반드시 비활성화
        if (hit != null) hit.SetActive(false); 
        
        if (projectilePS != null)
        {
            projectilePS.Clear(); // 이전 파티클 잔상 삭제
            projectilePS.Play();
        }
    }
    public void SetProjectileData(float dmg, GameObject attacker)
    {
        damage = dmg;
        owner = attacker;
    }
    protected virtual IEnumerator DisableTimer(float time)
    {
        yield return new WaitForSeconds(time);
        ReturnToPool();
    }

    protected virtual void FixedUpdate()
    {
        if (speed != 0 && rb != null)
        {
            rb.velocity = transform.forward * speed;
        }
    }
    protected virtual void OnCollisionEnter(Collision collision)
    {
        IDamageable target = collision.gameObject.GetComponent<IDamageable>();
        if (target != null)
        {
            // 인터페이스를 통해 데미지 전달
            target.TakeDamage(damage, gameObject);

            // 2. [추가] 나를 쏜 주인(HealerAttack 등)에게 적 정보 전달
            if (owner != null)
            {
                // AttackBase를 상속받은 스크립트라면 OnProjectileHit 호출
                var attacker = owner.GetComponent<HealerAttack>();
                if (attacker != null)
                {
                    var enemyStat = collision.gameObject.GetComponent<EnemyHp>();
                    if (enemyStat != null) attacker.OnProjectileHit(enemyStat);
                }
            }
        }
        // 1. 투사체 물리 정지
        rb.constraints = RigidbodyConstraints.FreezeAll;
        if (lightSourse != null) lightSourse.enabled = false;
        col.enabled = false;

        if (projectilePS != null) projectilePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 2. 충돌 지점 계산
        ContactPoint contact = collision.contacts[0];
        Vector3 hitPos = contact.point + contact.normal * hitOffset;

        // 3. [핵심 수정] 외부 프리팹 소환 로직
        if (hit != null)
        {
            // 프리팹의 이름을 사용하여 풀 매니저에서 오브젝트를 가져옵니다.
            var hitEffect = ObjectPoolManager.instance.GetGo(hit.name);

            if (hitEffect != null)
            {
                hitEffect.transform.position = hitPos;

                // 회전 설정
                if (UseFirePointRotation) 
                    hitEffect.transform.rotation = transform.rotation * Quaternion.Euler(0, 180f, 0);
                else if (rotationOffset != Vector3.zero) 
                    hitEffect.transform.rotation = Quaternion.Euler(rotationOffset);
                else 
                    hitEffect.transform.LookAt(contact.point + contact.normal);

                // [참고] 가져온 이펙트가 자동으로 풀에 반환되도록 
                // 해당 히트 프리팹에도 PoolAble이나 유사한 스크립트가 붙어있어야 합니다.
                hitEffect.SetActive(true); 
                
                // 만약 히트 이펙트 안에 파티클이 있다면 재생
                var ps = hitEffect.GetComponent<ParticleSystem>();
                if (ps != null) ps.Play();
            }
        }

        // 4. 투사체 본체 반환
        if (disableCoroutine != null) StopCoroutine(disableCoroutine);
        // 이펙트가 소환되었으므로 본체는 즉시 혹은 아주 짧은 시간 뒤 반환
        disableCoroutine = StartCoroutine(DisableTimer(0.2f)); 
    }
    private void ReturnToPool()
    {
        // Destroy 대신 반드시 Pool.Release를 사용해야 함
        if (this.gameObject.activeSelf)
        {
            Pool.Release(this.gameObject);
        }
    }
}
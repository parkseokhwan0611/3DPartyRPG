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
        // 충돌 시 물리 멈춤
        rb.constraints = RigidbodyConstraints.FreezeAll;
        if (lightSourse != null) lightSourse.enabled = false;
        col.enabled = false;

        if (projectilePS != null) projectilePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 충돌 이펙트 처리
        ContactPoint contact = collision.contacts[0];
        if (hit != null)
        {
            hit.transform.position = contact.point + contact.normal * hitOffset;
            
            if (UseFirePointRotation) 
                hit.transform.rotation = transform.rotation * Quaternion.Euler(0, 180f, 0);
            else if (rotationOffset != Vector3.zero) 
                hit.transform.rotation = Quaternion.Euler(rotationOffset);
            else 
                hit.transform.LookAt(contact.point + contact.normal);

            if (hitPS != null) hitPS.Play();
        }

        // 충돌했으므로 일정 시간 뒤 풀로 반환 (이펙트가 끝날 시간)
        if (disableCoroutine != null) StopCoroutine(disableCoroutine);
        float waitTime = (hitPS != null) ? hitPS.main.duration : 1.0f;
        disableCoroutine = StartCoroutine(DisableTimer(waitTime));
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
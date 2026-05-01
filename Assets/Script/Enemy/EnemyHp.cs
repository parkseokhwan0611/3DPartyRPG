using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHp : MonoBehaviour, IDamageable
{
    [Header("# HP Settings")]
    public float hp;
    public float maxHp;
    public bool isDead = false;

    [Header("# Death Settings")]
    public float expReward = 5f; // 죽었을 때 주는 경험치
    public float destroyDelay = 2f; // 시체가 사라질 때까지의 시간

    private Animator animator;
    private NavMeshAgent navAgent;
    private Collider col;

    void Awake()
    {
        animator = GetComponent<Animator>();
        navAgent = GetComponent<NavMeshAgent>();
        col = GetComponent<Collider>();
    }

    void Start()
    {
        hp = maxHp;
    }

    // IDamageable 인터페이스 구현
    public void TakeDamage(float damage, GameObject attacker)
    {
        if (isDead) return;

        hp -= damage;
        Debug.Log($"{gameObject.name}이 {damage}의 데미지를 입음! 남은 체력: {hp}");

        if (hp <= 0)
        {
            hp = 0;
            Die();
        }
    }
    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // 1. 죽음 애니메이션 재생
        if (animator != null) animator.SetTrigger("isDead");

        // 2. 물리 판정 제거 (플레이어가 시체를 때리지 못하게 함)
        if (col != null) col.enabled = false;

        // 3. 내비게이션 중지
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.isStopped = true;
            navAgent.speed = 0;
        }

        // 4. 경험치 지급 및 풀 반환(또는 파괴)
        StartCoroutine(DeathRoutine());
    }
    IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(destroyDelay);
        
        if (GameManager.instance != null)
            GameManager.instance.exp += expReward;

        // 오브젝트 풀링을 사용하신다면 Pool.Release(gameObject)로 변경하세요.
        Destroy(gameObject);
    }
}

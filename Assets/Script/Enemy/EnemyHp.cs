using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHp : MonoBehaviour, IDamageable
{
    [Header("# HP Settings")]
    public float hp;
    public float maxHp;
    public bool isDead = false;

    [Header("# References")]
    public string enemyName;
    public string damageTextPoolKey = "NormalDamageText"; // 오브젝트 풀 키
    public Transform hudPos;

    [Header("# Death Settings")]
    public float expReward   = 5f;
    public float destroyDelay = 2f;

    // 이벤트
    public System.Action<float, float> OnHpChanged;
    public System.Action OnDied; // 사망 이벤트 추가 (외부 구독용)

    // 컴포넌트 캐싱
    private Animator animator;
    private NavMeshAgent navAgent;
    private Collider col;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        animator = GetComponent<Animator>();
        navAgent = GetComponent<NavMeshAgent>();
        col      = GetComponent<Collider>();
    }

    void Start()
    {
        hp = maxHp;
    }

    // ─────────────────────────────────────────────────────────────────
    // IDamageable 구현
    // ─────────────────────────────────────────────────────────────────

    public void TakeDamage(float damage, GameObject attacker)
    {
        TakeDamage(damage, attacker, Color.white); // 기존 호환용
    }

    public void TakeDamage(float damage, GameObject attacker, Color damageColor)
    {
        if (isDead) return;

        hp = Mathf.Clamp(hp - damage, 0, maxHp);
        SpawnDamageText(damage, damageColor); // 색상 전달
        OnHpChanged?.Invoke(hp, maxHp);
        if (hp <= 0) Die();
    }

    // ─────────────────────────────────────────────────────────────────
    // 내부 로직
    // ─────────────────────────────────────────────────────────────────

    private void SpawnDamageText(float damage, Color color)
    {
        // ObjectPoolManager가 있으면 풀링, 없으면 Instantiate 폴백
        GameObject textObj = null;

        if (ObjectPoolManager.instance != null)
        {
            textObj = ObjectPoolManager.instance.GetGo(damageTextPoolKey);
        }

        if (textObj == null)
        {
            Debug.LogWarning($"[EnemyHp] 풀에서 {damageTextPoolKey}를 가져오지 못했습니다.");
            return;
        }

        float randomX = Random.Range(-0.4f, 0.4f);
        Vector3 spawnPos = hudPos != null ? hudPos.position : transform.position + Vector3.up * 2f;
        spawnPos += new Vector3(randomX, 0f, 0f);

        //Vector3 spawnPos      = hudPos != null ? hudPos.position : transform.position + Vector3.up * 2f;
        textObj.transform.position = spawnPos;
        textObj.transform.rotation = Quaternion.Euler(60f, 0f, 0f);

        DamageText dt = textObj.GetComponent<DamageText>();
        if (dt != null) dt.Setup(damage, color); // 색상 전달
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // 사망 이벤트 발생 (GoblinThiefMaleScript 등이 구독)
        OnDied?.Invoke();

        // 애니메이션
        if (animator != null) animator.SetTrigger("isDead");

        // 콜라이더 비활성화
        if (col != null) col.enabled = false;

        // 내비게이션 중지
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.isStopped = true;
            navAgent.speed     = 0;
        }

        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(destroyDelay);

        if (GameManager.instance != null)
            GameManager.instance.exp += expReward;

        Destroy(gameObject);
    }
}
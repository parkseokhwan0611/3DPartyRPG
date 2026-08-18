using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHp : MonoBehaviour, IDamageable
{
    // 씬 내 모든 몬스터 인스턴스 — 미니맵 등이 매번 FindObjectsOfType으로 스캔하지 않도록 자체 등록
    // (NpcInteractable.AllInstances와 동일한 패턴)
    public static readonly List<EnemyHp> AllInstances = new List<EnemyHp>();

    void OnEnable()  => AllInstances.Add(this);
    void OnDisable() => AllInstances.Remove(this);

    public enum MonsterGrade { Normal, Elite, Boss }

    [Header("# HP Settings")]
    public float hp;
    public float maxHp;
    public bool isDead = false;

    [Header("# 몬스터 등급")]
    [Tooltip("일반/정예/보스 — 타겟 HP UI 등에서 등급별 추가 표시(별 등)에 사용")]
    public MonsterGrade grade = MonsterGrade.Normal;

    [Header("# 방어 스탯")]
    [Tooltip("물리 피해 경감에 사용 (경감율 = def / (def + 100))")]
    public float def = 0f;
    [Tooltip("마법 피해 경감에 사용 (경감율 = magicRes / (magicRes + 100))")]
    public float magicRes = 0f;

    [Header("# 피격 텍스트 색상")]
    public Color physicalDamageColor = new Color32(0xFF, 0x6E, 0x01, 0xFF); // #FF6E01
    public Color magicDamageColor    = new Color32(0x00, 0xC1, 0xFF, 0xFF); // #00C1FF

    [Header("# References")]
    public string enemyName;
    [Tooltip("퀘스트(몬스터 처치)에서 몬스터 종류를 구분하는 고유 ID. 같은 종류 몬스터는 같은 값 사용")]
    public string monsterId;
    public string damageTextPoolKey = "NormalDamageText"; // 오브젝트 풀 키
    public Transform hudPos;

    [Header("# Death Settings")]
    public float expReward    = 5f;
    public int   goldReward   = 10;
    public float destroyDelay = 2f;
    [Tooltip("AudioManager에 등록한 SFX 키. 사망 시 재생 (비워두면 재생 안 함)")]
    public string deathSfxKey;

    [Header("# Drop Settings")]
    [Tooltip("이 몬스터의 드랍 테이블 (없으면 드랍 없음)")]
    public MonsterDropTable dropTable;

    // 이벤트
    public System.Action<float, float> OnHpChanged;
    public System.Action OnDied; // 사망 이벤트 추가 (외부 구독용)

    // 몬스터 처치 퀘스트 등, 개별 인스턴스를 구독하기 어려운 전역 리스너용
    public static event System.Action<EnemyHp> OnAnyEnemyDied;

    // 컴포넌트 캐싱
    private Animator animator;
    private NavMeshAgent navAgent;
    private Collider col;
    private StatusEffectHandler statusHandler;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        animator      = GetComponent<Animator>();
        navAgent      = GetComponent<NavMeshAgent>();
        col           = GetComponent<Collider>();
        statusHandler = GetComponent<StatusEffectHandler>();
        if (col == null) col = GetComponentInChildren<Collider>(); // 콜라이더가 자식 오브젝트에 있는 프리팹도 대응
    }

    void Start()
    {
        hp = maxHp;
    }

    // ─────────────────────────────────────────────────────────────────
    // IDamageable 구현
    // ─────────────────────────────────────────────────────────────────

    // 물리 피해 (방어력으로 경감)
    public void TakeDamage(float damage, GameObject attacker, bool isCrit = false) => TakeDamage(damage, attacker, physicalDamageColor, isCrit);

    public void TakeDamage(float damage, GameObject attacker, Color damageColor, bool isCrit = false)
    {
        if (isDead) return;

        float reduction   = def / (def + 100f);
        float finalDamage = damage * (1f - reduction);
        ApplyDamage(finalDamage, damageColor, isCrit);
    }

    // 마법 피해 (마법저항력으로 경감)
    public void TakeMagicDamage(float damage, GameObject attacker, bool isCrit = false) => TakeMagicDamage(damage, attacker, magicDamageColor, isCrit);

    public void TakeMagicDamage(float damage, GameObject attacker, Color damageColor, bool isCrit = false)
    {
        if (isDead) return;

        float reduction   = magicRes / (magicRes + 100f);
        float finalDamage = damage * (1f - reduction);
        ApplyDamage(finalDamage, damageColor, isCrit);
    }

    private void ApplyDamage(float finalDamage, Color damageColor, bool isCrit = false)
    {
        if (statusHandler != null)
            finalDamage = statusHandler.AbsorbDamage(finalDamage);

        if (finalDamage <= 0f) return; // 쉴드가 전부 흡수한 경우 — 데미지 텍스트/HP 변화 없음

        hp = Mathf.Clamp(hp - finalDamage, 0, maxHp);
        SpawnDamageText(finalDamage, damageColor, isCrit);
        OnHpChanged?.Invoke(hp, maxHp);
        if (hp <= 0) Die();
    }

    // ─────────────────────────────────────────────────────────────────
    // 내부 로직
    // ─────────────────────────────────────────────────────────────────

    private void SpawnDamageText(float damage, Color color, bool isCrit = false)
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
        if (dt != null) dt.Setup(damage, color, isCrit: isCrit); // 색상 전달
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // 사망 이벤트 발생 (GoblinThiefMaleScript 등이 구독)
        OnDied?.Invoke();
        OnAnyEnemyDied?.Invoke(this);

        if (!string.IsNullOrEmpty(deathSfxKey))
            AudioManager.instance?.PlaySFX(deathSfxKey);

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
        if (DataManager.instance != null)
        {
            DataManager.instance.AddExp(expReward);
            DataManager.instance.AddGold(goldReward);
            dropTable?.SpawnDrops(transform.position);
        }

        yield return new WaitForSeconds(destroyDelay);

        Destroy(gameObject);
    }
}
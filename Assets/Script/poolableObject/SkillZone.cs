using UnityEngine;
using System.Collections;

/// <summary>
/// 장판형 스킬 이펙트 오브젝트에 부착하는 컴포넌트.
/// DamageSkill이 Setup()으로 파라미터를 전달하면,
/// 오브젝트가 활성화된 동안 일정 간격으로 범위 내 적에게 데미지를 줍니다.
/// 오브젝트 반납(OnDisable)시 자동으로 판정을 중단합니다.
/// </summary>
public class SkillZone : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    // 런타임 파라미터 (DamageSkill이 Setup()으로 주입)
    // ─────────────────────────────────────────────────────────────────

    private float  damage;
    private float  range;
    private float  interval;
    private bool   useAp;
    private GameObject attacker;
    private Color  damageColor;

    private int    enemyLayer;
    private Coroutine tickCoroutine;

    // ─────────────────────────────────────────────────────────────────
    // 초기화 (DamageSkill에서 스폰 직후 호출)
    // ─────────────────────────────────────────────────────────────────

    public void Setup(float damage, float range, float interval,
                      GameObject attacker, Color damageColor)
    {
        this.damage      = damage;
        this.range       = range;
        this.interval    = interval;
        this.attacker    = attacker;
        this.damageColor = damageColor;
        this.enemyLayer  = LayerMask.GetMask("Enemy");

        // OnEnable이 Setup보다 먼저 호출될 수 있으므로 여기서도 시작
        if (tickCoroutine == null && gameObject.activeInHierarchy)
            tickCoroutine = StartCoroutine(TickRoutine());
    }

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void OnDisable()
    {
        if (tickCoroutine != null)
        {
            StopCoroutine(tickCoroutine);
            tickCoroutine = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 데미지 틱
    // ─────────────────────────────────────────────────────────────────

    private IEnumerator TickRoutine()
    {
        var wait = new WaitForSeconds(interval);

        while (true)
        {
            ApplyDamage();
            yield return wait;
        }
    }

    private void ApplyDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, range, enemyLayer);
        foreach (var col in hits)
        {
            EnemyHp enemyHp = col.GetComponent<EnemyHp>();
            if (enemyHp == null) continue;
            enemyHp.TakeDamage(damage, attacker, damageColor);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 에디터 기즈모
    // ─────────────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}

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
    private static readonly Collider[] _hitBuffer = new Collider[16];

    // ─────────────────────────────────────────────────────────────────
    // 런타임 파라미터 (DamageSkill이 Setup()으로 주입)
    // ─────────────────────────────────────────────────────────────────

    private float  damage;
    private float  range;
    private float  interval;
    private float  activationDelay;
    private bool   hitOnce;
    private GameObject attacker;
    private bool   isMagicDamage;
    private float  duration;

    private int    enemyLayer;
    private Coroutine tickCoroutine;

    // ─────────────────────────────────────────────────────────────────
    // 초기화 (DamageSkill에서 스폰 직후 호출)
    // ─────────────────────────────────────────────────────────────────

    public void Setup(float damage, float range, float interval, float activationDelay,
                      bool hitOnce, GameObject attacker, bool isMagicDamage, float duration)
    {
        this.damage          = damage;
        this.range           = range;
        this.interval        = interval;
        this.activationDelay = activationDelay;
        this.hitOnce         = hitOnce;
        this.attacker        = attacker;
        this.isMagicDamage   = isMagicDamage;
        this.duration        = duration;
        this.enemyLayer      = LayerMask.GetMask("Enemy");

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
        // 선딜: 이펙트가 먼저 보이고 데미지는 나중에
        if (activationDelay > 0f)
            yield return new WaitForSeconds(activationDelay);

        if (hitOnce)
        {
            // 단발: 한 번 판정 후 종료
            ApplyDamage();
            tickCoroutine = null;
            yield break;
        }

        // 반복: interval마다 판정, duration이 지나면 스스로 중단
        // (PoolableObject 설정이 누락/오류인 경우에도 무한 루프로 데미지가 계속 나가는 것을 방지)
        var wait = new WaitForSeconds(interval);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            ApplyDamage();
            yield return wait;
            elapsed += interval;
        }

        tickCoroutine = null;
    }

    private void ApplyDamage()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, range, _hitBuffer, enemyLayer);
        for (int i = 0; i < hitCount; i++)
        {
            EnemyHp enemyHp = _hitBuffer[i].GetComponent<EnemyHp>();
            if (enemyHp == null) continue;
            if (isMagicDamage) enemyHp.TakeMagicDamage(damage, attacker);
            else                enemyHp.TakeDamage(damage, attacker);
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

using System.Collections;
using UnityEngine;

public class MonsterMeleeAttack : AttackBase
{
    private EnemyHp enemyHp;

    [Header("근접 공격 판정 설정")]
    public float hitRadius = 1.5f;
    public float hitOffset = 1.0f;
    [Tooltip("체크하면 마법 피해(마법저항력으로 경감), 해제하면 물리 피해(방어력으로 경감)")]
    public bool isMagicAttack = false;

    [Header("# 사운드")]
    [Tooltip("AudioManager에 등록한 SFX 키. 타격 판정과 같은 타이밍에 재생 (비워두면 재생 안 함)")]
    public string attackSfxKey;

    [Header("타이밍 설정 (초 단위)")]
    public float damageDelay = 0.33f;
    [Tooltip("타격 판정 이후 애니메이션 후딜레이 — 이 시간이 끝나야 이동을 재개하고, " +
             "정예/보스라면 이 시간이 끝나야 스킬도 고려한다. 공격 애니메이션 클립 길이에서 " +
             "damageDelay를 뺀 만큼으로 맞추면 됨")]
    public float recoveryDuration = 0.5f;
    private Coroutine attackCoroutine;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        enemyHp = GetComponent<EnemyHp>();
    }

    protected override void Start()
    {
        base.Start();
    }

    protected override void Update()
    {
        base.Update();
    }

    // ─────────────────────────────────────────────────────────────────
    // 공격 실행
    // ─────────────────────────────────────────────────────────────────

    protected override void HandleAttackLogic()
    {
        if (IsAttacking) return;
        base.HandleAttackLogic();
    }

    protected override void ExecuteAttack()
    {
        if (IsAttacking) return;
        
        // 혹시 남아있는 이전 코루틴 강제 정지
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        
        attackCoroutine = StartCoroutine(MonsterAttackRoutine());
    }

    protected override void StopAttackCoroutine()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        IsAttacking = false;
    }

    private IEnumerator MonsterAttackRoutine()
    {
        IsAttacking = true;

        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = true;

        if (currentTarget != null)
        {
            Vector3 direction = (currentTarget.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);
        }

        if (anim != null)
        {
            anim.SetTrigger("doNormalAttack");
            anim.SetBool("isWalking", false);
        }

        yield return new WaitForSeconds(damageDelay);

        if (enemyHp != null && enemyHp.hp > 0)
            OnHit();

        if (!string.IsNullOrEmpty(attackSfxKey))
            AudioManager.instance?.PlaySFXAtPosition(attackSfxKey, transform.position);

        yield return new WaitForSeconds(recoveryDuration);

        bool isStunned = statusHandler != null && statusHandler.HasDebuff(StatusEffectType.Stun);

        if (!isStunned && agent != null && agent.isOnNavMesh)
            agent.isStopped = false;

        IsAttacking    = false;
        attackCoroutine = null;
        RaiseAttackEnded();
        attackCooldown = 0f;
    }

    // ─────────────────────────────────────────────────────────────────
    // 타격 판정
    // ─────────────────────────────────────────────────────────────────

    private static readonly Collider[] _hitBuffer = new Collider[16];

    public override void OnHit()
    {
        Vector3 hitPos = transform.position + (transform.forward * hitOffset);
        int hitCount = Physics.OverlapSphereNonAlloc(hitPos, hitRadius, _hitBuffer, enemyLayer);

        for (int i = 0; i < hitCount; i++)
        {
            IDamageable damageable = _hitBuffer[i].GetComponent<IDamageable>();
            if (damageable == null) continue;

            if (isMagicAttack) damageable.TakeMagicDamage(attackDamage, gameObject);
            else                damageable.TakeDamage(attackDamage, gameObject);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 에디터 기즈모
    // ─────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 hitPos = transform.position + (transform.forward * hitOffset);
        Gizmos.DrawWireSphere(hitPos, hitRadius);
    }
    public void ResetAttackState()
    {
        IsAttacking = false;
    }
}
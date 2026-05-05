using System.Collections;
using UnityEngine;

public class MonsterMeleeAttack : AttackBase
{
    private EnemyHp enemyHp;

    [Header("근접 공격 판정 설정")]
    public float hitRadius = 1.5f;
    public float hitOffset = 1.0f;

    [Header("타이밍 설정 (초 단위)")]
    public float damageDelay = 0.33f;

    // 외부에서 읽기만 가능하도록 프로퍼티로 변경
    public bool IsAttacking { get; private set; } = false;

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

    protected override void ExecuteAttack()
    {
        // isAttacking 플래그로 코루틴 중복 실행 방지
        if (IsAttacking) return;
        StartCoroutine(MonsterAttackRoutine());
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

        yield return new WaitForSeconds(attackDuration - damageDelay);

        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = false;

        IsAttacking = false;
        RaiseAttackEnded();

        // 코루틴이 끝난 시점에 쿨다운을 0으로 리셋
        // AttackBase가 걸어놓은 쿨다운을 무효화
        attackCooldown = 0f;
    }

    // ─────────────────────────────────────────────────────────────────
    // 타격 판정
    // ─────────────────────────────────────────────────────────────────

    public override void OnHit()
    {
        Vector3 hitPos = transform.position + (transform.forward * hitOffset);
        Collider[] hitTargets = Physics.OverlapSphere(hitPos, hitRadius, enemyLayer);

        foreach (Collider target in hitTargets)
        {
            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
                damageable.TakeDamage(attackDamage, gameObject);
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
}
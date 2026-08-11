using UnityEngine;
using System.Collections;

public class MonsterRangedAttack : AttackBase
{
    [Header("원거리 공격 설정")]
    [Tooltip("ObjectPoolManager에 등록된 투사체 풀 키")]
    public string projectileName = "MonsterProjectile";
    [Tooltip("투사체 발사 위치 (없으면 자신 위치 + 1m 위)")]
    public Transform firePoint;
    [Tooltip("애니메이션 시작 후 투사체 발사까지 딜레이 (초)")]
    public float damageDelay = 0.35f;
    [Tooltip("투사체 발사 이후 애니메이션 후딜레이 — 이 시간이 끝나야 이동을 재개하고, " +
             "정예/보스라면 이 시간이 끝나야 스킬도 고려한다. 발사 애니메이션 클립 길이에서 " +
             "damageDelay를 뺀 만큼으로 맞추면 됨")]
    public float recoveryDuration = 0.5f;
    [Tooltip("체크하면 마법 피해(마법저항력으로 경감), 해제하면 물리 피해(방어력으로 경감)")]
    public bool isMagicAttack = false;

    [Header("# 사운드")]
    [Tooltip("AudioManager에 등록한 SFX 키. 투사체 발사와 같은 타이밍에 재생 (비워두면 재생 안 함)")]
    public string attackSfxKey;

    private Coroutine attackCoroutine;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    protected override void Start()
    {
        base.Start();
    }

    protected override void Update()
    {
        base.Update();
    }

    // ─────────────────────────────────────────────────────────────────
    // 공격 중 이동 차단
    // ─────────────────────────────────────────────────────────────────

    protected override void HandleAttackLogic()
    {
        if (IsAttacking) return;
        base.HandleAttackLogic();
    }

    // ─────────────────────────────────────────────────────────────────
    // 공격 실행
    // ─────────────────────────────────────────────────────────────────

    protected override void ExecuteAttack()
    {
        if (IsAttacking) return;

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        attackCoroutine = StartCoroutine(AttackRoutine());
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

    private IEnumerator AttackRoutine()
    {
        IsAttacking = true;

        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = true;

        // 타겟 방향 조준
        if (currentTarget != null)
        {
            Vector3 dir = (currentTarget.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        if (anim != null)
        {
            anim.SetTrigger("doNormalAttack");
            anim.SetBool("isWalking", false);
        }

        // 투사체 발사 타이밍 대기
        yield return new WaitForSeconds(damageDelay);

        if (currentTarget != null)
            FireProjectile();

        if (!string.IsNullOrEmpty(attackSfxKey))
            AudioManager.instance?.PlaySFX(attackSfxKey);

        // 공격 지속시간 나머지 대기
        yield return new WaitForSeconds(recoveryDuration);

        bool isStunned = statusHandler != null && statusHandler.HasDebuff(StatusEffectType.Stun);
        if (!isStunned && agent != null && agent.isOnNavMesh)
            agent.isStopped = false;

        IsAttacking     = false;
        attackCoroutine = null;
        RaiseAttackEnded();
        attackCooldown  = 0f;
    }

    // ─────────────────────────────────────────────────────────────────
    // 투사체 발사
    // ─────────────────────────────────────────────────────────────────

    private void FireProjectile()
    {
        if (ObjectPoolManager.instance == null) return;
        if (string.IsNullOrEmpty(projectileName)) return;

        Vector3 spawnPos = firePoint != null
            ? firePoint.position
            : transform.position + Vector3.up;

        // AimTarget 자식 우선으로 조준점 계산
        Vector3 targetPos = TargetPosition;
        Vector3 dir       = (targetPos - spawnPos).normalized;
        if (dir == Vector3.zero) dir = transform.forward;

        Quaternion rot = Quaternion.LookRotation(dir);

        var projectile = ObjectPoolManager.instance.GetGo(projectileName);
        if (projectile == null) return;

        projectile.transform.position = spawnPos;
        projectile.transform.rotation = rot;

        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position        = spawnPos;
            rb.rotation        = rot;
            rb.velocity        = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // isMagicAttack에 따라 맞는 대상이 방어력/마법저항력으로 알아서 경감 처리
        ProjectileScript proj = projectile.GetComponent<ProjectileScript>();
        if (proj != null)
            proj.SetProjectileData(attackDamage, gameObject, isMagicAttack);
    }

    public override void OnHit() { }

    // ─────────────────────────────────────────────────────────────────
    // 에디터 기즈모
    // ─────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    public void ResetAttackState()
    {
        IsAttacking = false;
    }
}

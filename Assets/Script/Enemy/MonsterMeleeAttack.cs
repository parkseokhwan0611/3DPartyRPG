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
    [Tooltip("사운드 재생 시점 보정(초). 기본은 타격 판정(damageDelay)과 동시에 재생되는데, " +
             "사운드 클립 앞부분에 무음이 있어 실제로 들리는 시점이 밀리면 음수 값을 넣어 더 일찍 재생되게 조절")]
    public float attackSfxTimingOffset = 0f;

    [Header("타이밍 설정 (초 단위)")]
    public float damageDelay = 0.33f;
    [Tooltip("타격 판정 이후 애니메이션 후딜레이 — 이 시간이 끝나야 이동을 재개하고, " +
             "정예/보스라면 이 시간이 끝나야 스킬도 고려한다. 공격 애니메이션 클립 길이에서 " +
             "damageDelay를 뺀 만큼으로 맞추면 됨")]
    public float recoveryDuration = 0.5f;
    private Coroutine attackCoroutine;
    private Coroutine sfxCoroutine;

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
        if (sfxCoroutine != null)
        {
            StopCoroutine(sfxCoroutine);
            sfxCoroutine = null;
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

        if (!string.IsNullOrEmpty(attackSfxKey))
            sfxCoroutine = StartCoroutine(PlayAttackSfxDelayed());

        yield return new WaitForSeconds(damageDelay);

        if (enemyHp != null && enemyHp.hp > 0)
            OnHit();

        yield return new WaitForSeconds(recoveryDuration);

        bool isStunned = statusHandler != null && statusHandler.HasDebuff(StatusEffectType.Stun);

        if (!isStunned && agent != null && agent.isOnNavMesh)
            agent.isStopped = false;

        IsAttacking    = false;
        attackCoroutine = null;
        RaiseAttackEnded();
    }

    private IEnumerator PlayAttackSfxDelayed()
    {
        float delay = Mathf.Max(0f, damageDelay + attackSfxTimingOffset);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        AudioManager.instance?.PlaySFX(attackSfxKey);
        sfxCoroutine = null;
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
using UnityEngine;
using System.Collections;

// MonsterRangedAttack/MonsterGrenadeAttack/MonsterMeleeAttack 공용 기지 — 조준 → 애니메이션
// 트리거 → SFX 재생 → damageDelay 대기 → 실제 피해 판정 → recoveryDuration 대기 → 이동 재개라는
// 동일한 흐름을 공유하고, "실제 피해 판정"만 서브클래스마다 다르게 구현한다.
public abstract class MonsterAttackBase : AttackBase
{
    [Header("공격 타이밍")]
    [Tooltip("애니메이션 시작 후 실제 피해 판정까지 딜레이 (초)")]
    public float damageDelay = 0.35f;
    [Tooltip("피해 판정 이후 애니메이션 후딜레이 — 이 시간이 끝나야 이동을 재개하고, " +
             "정예/보스라면 이 시간이 끝나야 스킬도 고려한다. 공격 애니메이션 클립 길이에서 " +
             "damageDelay를 뺀 만큼으로 맞추면 됨")]
    public float recoveryDuration = 0.5f;
    [Tooltip("체크하면 마법 피해(마법저항력으로 경감), 해제하면 물리 피해(방어력으로 경감)")]
    public bool isMagicAttack = false;

    [Header("# 치명타")]
    [Tooltip("치명타 확률 (0~1)")]
    [Range(0f, 1f)] public float critChance = 0.1f;
    [Tooltip("치명타 시 피해 배율 (1.5 = 150%)")]
    public float critDamageMultiplier = 1.5f;

    [Header("# 사운드")]
    [Tooltip("AudioManager에 등록한 SFX 키. 피해 판정과 같은 타이밍에 재생 (비워두면 재생 안 함)")]
    public string attackSfxKey;
    [Tooltip("사운드 재생 시점 보정(초). 기본은 피해 판정(damageDelay)과 동시에 재생되는데, " +
             "사운드 클립 앞부분에 무음이 있어 실제로 들리는 시점이 밀리면 음수 값을 넣어 더 일찍 재생되게 조절")]
    public float attackSfxTimingOffset = 0f;

    private Coroutine attackCoroutine;
    private Coroutine sfxCoroutine;

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
        if (sfxCoroutine != null)
        {
            StopCoroutine(sfxCoroutine);
            sfxCoroutine = null;
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

        if (!string.IsNullOrEmpty(attackSfxKey))
            sfxCoroutine = StartCoroutine(PlayAttackSfxDelayed());

        yield return new WaitForSeconds(damageDelay);

        ExecuteAttackPayload();

        // 공격 지속시간 나머지 대기
        yield return new WaitForSeconds(recoveryDuration);

        bool isStunned = statusHandler != null && statusHandler.HasDebuff(StatusEffectType.Stun);
        if (!isStunned && agent != null && agent.isOnNavMesh)
            agent.isStopped = false;

        IsAttacking     = false;
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

    // damageDelay 경과 후 실제 피해를 발생시키는 지점 — 발사체 소환/투척/근접 판정 등
    // 서브클래스마다 다르게 구현
    protected abstract void ExecuteAttackPayload();

    public void ResetAttackState()
    {
        IsAttacking = false;
    }
}

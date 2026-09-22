using UnityEngine;
using System.Collections;

// 파티원 근접 평타 — 판정 범위·타이밍·이펙트·효과음·물리/마법은 현재 클래스 SO(ClassData)에서 읽는다
public class MeleeAttack : AttackBase
{
    private static readonly Collider[] _hitBuffer = new Collider[16];

    private CharacterStat myStat;
    private Coroutine attackCoroutine;
    private bool _isAttacking = false;

    void Awake()
    {
        myStat = GetComponent<CharacterStat>();
    }

    private IEnumerator AttackRoutine()
    {
        ClassData cls = CurrentClass;
        if (cls == null) yield break;

        _isAttacking = true;
        IsAttackAnimPlaying = true;

        LookAtTarget();
        if (anim != null) anim.ResetTrigger("doNormalAttack");

        yield return null;
        yield return null;

        // 공격속도 보너스만큼 모션과 타격·후딜 타이밍을 같이 빠르게
        float speed = AttackAnimSpeed;
        if (anim != null)
        {
            ApplyAttackAnimSpeed();
            anim.SetTrigger("doNormalAttack");
        }
        PlaySfx(cls.normalAttackSfxKey);

        yield return new WaitForSeconds(cls.damageDelay / speed);
        OnHit();

        yield return new WaitForSeconds(cls.recoveryDuration / speed);

        IsAttackAnimPlaying = false;
        _isAttacking = false;
        attackCoroutine = null;
    }

    public override void OnHit()
    {
        ClassData cls = CurrentClass;
        if (myStat == null || cls == null) return;

        // 1. 판정
        Vector3 hitPos = transform.position + (transform.forward * cls.meleeHitOffset);
        int hitCount = Physics.OverlapSphereNonAlloc(hitPos, cls.meleeHitRadius, _hitBuffer, enemyLayer);

        // 2. 이펙트 생성 + 적중 시 체력 회복 (적을 한 명이라도 맞췄을 때)
        if (hitCount > 0)
        {
            SpawnHitEffect(cls.meleeHitEffectKey, transform.position + (transform.forward * 0.3f) + Vector3.up);

            if (myStat.HpOnHit > 0f)
                myStat.HealHp(myStat.HpOnHit, showAura: false); // 흡혈은 생명 흡수 버프 아우라만 표시

            if (myStat.MpOnHit > 0f)
                myStat.RecoverMp(myStat.MpOnHit, showAura: false, showText: false);
        }

        bool  isMagic = IsMagicBasicAttack;
        float damage  = isMagic ? myStat.TotalAp * (1f + myStat.MagicDmgBonus)
                                : myStat.TotalAtk * (1f + myStat.PhysDmgBonus);
        bool  isCrit  = Random.value < myStat.TotalCritRate;

        if (isCrit)
        {
            damage *= myStat.TotalCritDamage;
            if (CinemachineShake.Instance != null)
                CinemachineShake.Instance.ShakeCamera(10f, .2f);
        }

        // 3. 데미지 판정
        EnemyHp primary = null; // 발동형 패시브를 적용할 대표 대상 — 지금 노리던 적이 맞았으면 그 적, 아니면 처음 맞은 적
        for (int i = 0; i < hitCount; i++)
        {
            var enemyStat = _hitBuffer[i].GetComponent<EnemyHp>();
            if (enemyStat == null) continue;

            if (isMagic) enemyStat.TakeMagicDamage(damage, gameObject, isCrit);
            else         enemyStat.TakeDamage(damage, gameObject, isCrit);
            if (primary == null || enemyStat == targetHealth) primary = enemyStat;
        }

        // 4. 발동형 패시브 (공격속도 증가·독·치명타 번개·쿨 초기화) — 한 번 휘두를 때 한 번만
        if (primary != null)
            myStat.NotifyBasicAttackHit(primary, isCrit, isMagic);
    }

    // 캐릭터 정면 방향으로 히트 이펙트 스폰
    private void SpawnHitEffect(string poolKey, Vector3 pos)
    {
        if (string.IsNullOrEmpty(poolKey) || ObjectPoolManager.instance == null) return;

        var effect = ObjectPoolManager.instance.GetGo(poolKey);
        if (effect != null)
            effect.transform.SetPositionAndRotation(pos, transform.rotation);
    }

    // 플레이 중에만 판정 범위 표시 (범위 값이 클래스 SO에 있어서 편집 모드에서는 알 수 없음)
    private void OnDrawGizmosSelected()
    {
        ClassData cls = CurrentClass;
        if (cls == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + (transform.forward * cls.meleeHitOffset), cls.meleeHitRadius);
    }

    protected override void ExecuteAttack()
    {
        if (_isAttacking) return;
        attackCoroutine = StartCoroutine(AttackRoutine());
    }

    protected override void StopAttackCoroutine()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        IsAttackAnimPlaying = false;
        _isAttacking = false;
    }
}

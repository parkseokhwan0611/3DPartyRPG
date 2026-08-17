using UnityEngine;
using System.Collections;

public class MeleeAttack : AttackBase
{
    private static readonly Collider[] _hitBuffer = new Collider[16];

    private CharacterStat myStat;
    [Header("근접 공격 판정 설정")]
    public float hitRadius = 1.5f;
    public float hitOffset = 1.0f;

    [Header("타이밍 설정 (초 단위)")]
    public float damageDelay = 0.33f;
    [Tooltip("타격 판정 이후 애니메이션 후딜레이 — 이 시간이 끝나야 걷는 모션으로 전환되며 이동을 재개함. " +
             "공격 애니메이션 클립 길이에서 damageDelay를 뺀 만큼으로 맞추면 됨")]
    public float recoveryDuration = 0.3f;
    public string hitEffectName = "Yellow Sword Slash 1";
    private Coroutine attackCoroutine;
    private bool _isAttacking = false;

    void Awake()
    {
        myStat = GetComponent<CharacterStat>();
    }

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        IsAttackAnimPlaying = true;

        LookAtTarget();
        anim.ResetTrigger("doNormalAttack");

        yield return null;
        yield return null;

        anim.SetTrigger("doNormalAttack");
        AudioManager.instance?.PlaySFX("Tanker_NormalAtk");

        yield return new WaitForSeconds(damageDelay);
        OnHit();

        yield return new WaitForSeconds(recoveryDuration);

        IsAttackAnimPlaying = false;
        _isAttacking = false;
        attackCoroutine = null;
    }
    public override void OnHit()
    {
        // 0. 스탯 참조 확인
        if (myStat == null) return;

        // 1. 판정 위치 계산
        Vector3 hitPos = transform.position + (transform.forward * hitOffset);
        int hitCount = Physics.OverlapSphereNonAlloc(hitPos, hitRadius, _hitBuffer, enemyLayer);

        // 2. 이펙트 생성 + 적중 시 체력 회복 (적을 한 명이라도 맞췄을 때)
        if (hitCount > 0)
        {
            Vector3 effectPos = transform.position + (transform.forward * 0.3f) + Vector3.up;
            SpawnHitEffect(effectPos);

            if (myStat.HpOnHit > 0f)
                myStat.HealHp(myStat.HpOnHit);

            if (myStat.MpOnHit > 0f)
                myStat.RecoverMp(myStat.MpOnHit, showAura: false, showText: false);
        }

        float damage = myStat.TotalAtk * (1f + myStat.PhysDmgBonus);
        bool  isCrit = Random.value < myStat.TotalCritRate;

        if (isCrit)
        {
            damage *= myStat.TotalCritDamage;
            if (CinemachineShake.Instance != null)
                CinemachineShake.Instance.ShakeCamera(10f, .2f);
        }

        // 3. 데미지 판정
        for (int i = 0; i < hitCount; i++)
        {
            // 최적화: 한 번만 가져와서 사용
            var enemyStat = _hitBuffer[i].GetComponent<EnemyHp>();

            if (enemyStat != null)
                enemyStat.TakeDamage(damage, gameObject, isCrit); // 근접 = 물리 피해
        }
    }
    private void SpawnHitEffect(Vector3 pos)
    {
        if (ObjectPoolManager.instance != null)
        {
            var effect = ObjectPoolManager.instance.GetGo(hitEffectName);
            if (effect != null)
            {
                // 1. 위치 설정
                effect.transform.position = pos;

                // 2. 방향 설정 (캐릭터가 바라보는 정면 방향으로 회전)
                // 만약 적을 향해 더 정확히 날리고 싶다면 currentTarget.position - transform.position을 사용하세요.
                effect.transform.rotation = transform.rotation; 
            }
        }
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 hitPos = transform.position + (transform.forward * hitOffset);
        Gizmos.DrawWireSphere(hitPos, hitRadius);
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
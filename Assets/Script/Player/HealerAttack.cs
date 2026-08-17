using UnityEngine;
using System.Collections;

public class HealerAttack : AttackBase
{
    private CharacterStat myStat;

    [Header("Ranged Settings")]
    public string projectileName = "MagicBall";
    public Transform firePoint;
    public float damageDelay = 0.35f;
    [Tooltip("투사체 발사 이후 애니메이션 후딜레이 — 이 시간이 끝나야 걷는 모션으로 전환되며 이동을 재개함. " +
             "공격 애니메이션 클립 길이에서 damageDelay를 뺀 만큼으로 맞추면 됨")]
    public float recoveryDuration = 0.2f;

    private Coroutine attackCoroutine;
    private bool _isAttacking = false;

    protected override void Start()
    {
        base.Start();
        myStat = GetComponent<CharacterStat>();

        if (myStat == null)
            Debug.LogError($"[HealerAttack] {gameObject.name}에 CharacterStat이 없습니다!");
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

    private IEnumerator AttackRoutine()
    {
        if (currentTarget == null) yield break;

        _isAttacking = true;
        IsAttackAnimPlaying = true;

        if (agent != null)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (anim != null)
        {
            anim.ResetTrigger("doNormalAttack");
            yield return null;
            yield return null;
            anim.SetTrigger("doNormalAttack");
        }
        else
        {
            yield return null;
            yield return null;
        }
        AudioManager.instance?.PlaySFX("Healer_NormalAtk");

        yield return new WaitForSeconds(damageDelay);

        if (currentTarget == null)
        {
            IsAttackAnimPlaying = false;
            _isAttacking = false;
            attackCoroutine = null;
            yield break;
        }

        Vector3 spawnPos      = firePoint != null ? firePoint.position : transform.position;
        Vector3 preciseDir    = (TargetPosition - spawnPos).normalized;
        Quaternion preciseRot = Quaternion.LookRotation(preciseDir);

        if (ObjectPoolManager.instance == null)
        {
            IsAttackAnimPlaying = false;
            _isAttacking = false;
            attackCoroutine = null;
            yield break;
        }

        var effect = ObjectPoolManager.instance.GetGo(projectileName);
        if (effect == null)
        {
            IsAttackAnimPlaying = false;
            _isAttacking = false;
            attackCoroutine = null;
            yield break;
        }

        effect.transform.position = spawnPos;
        effect.transform.rotation = preciseRot;

        float damage = myStat.TotalAp * (1f + myStat.MagicDmgBonus);

        if (Random.value < myStat.TotalCritRate)
        {
            damage *= myStat.TotalCritDamage;
            if (CinemachineShake.Instance != null)
                CinemachineShake.Instance.ShakeCamera(10f, .2f);
        }

        ProjectileScript proj = effect.GetComponent<ProjectileScript>();
        if (proj != null)
            proj.SetProjectileData(damage, gameObject, OnProjectileHit, isMagic: true);

        Rigidbody rb = effect.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position        = spawnPos;
            rb.rotation        = preciseRot;
            rb.velocity        = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 투사체 발사 후 애니메이션이 자연스럽게 마무리되는 후딜레이 — 이 시간이 끝나야
        // IsAttackAnimPlaying이 풀려서 걷는 모션으로 전환되며 이동을 재개한다
        yield return new WaitForSeconds(recoveryDuration);

        IsAttackAnimPlaying = false;
        _isAttacking = false;
        attackCoroutine = null;
    }

    private void OnProjectileHit(EnemyHp enemyStat)
    {
        if (enemyStat == null) return;

        if (myStat != null && myStat.HpOnHit > 0f)
            myStat.HealHp(myStat.HpOnHit);

        if (myStat != null && myStat.MpOnHit > 0f)
            myStat.RecoverMp(myStat.MpOnHit, showAura: false, showText: false);
    }

    public override void OnHit() { }
}

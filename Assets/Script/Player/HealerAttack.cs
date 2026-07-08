using UnityEngine;
using System.Collections;

public class HealerAttack : AttackBase
{
    private CharacterStat myStat;

    [Header("Ranged Settings")]
    public string projectileName = "MagicBall";
    public Transform firePoint;
    public float damageDelay = 0.35f;

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

        yield return new WaitForSeconds(damageDelay);

        IsAttackAnimPlaying = false;

        if (currentTarget == null)
        {
            _isAttacking = false;
            attackCoroutine = null;
            yield break;
        }

        Vector3 spawnPos      = firePoint != null ? firePoint.position : transform.position;
        Vector3 preciseDir    = (TargetPosition - spawnPos).normalized;
        Quaternion preciseRot = Quaternion.LookRotation(preciseDir);

        if (ObjectPoolManager.instance == null)
        {
            _isAttacking = false;
            attackCoroutine = null;
            yield break;
        }

        var effect = ObjectPoolManager.instance.GetGo(projectileName);
        if (effect == null)
        {
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
            proj.SetProjectileData(damage, gameObject, OnProjectileHit, myStat.GetDamageColor(false));

        Rigidbody rb = effect.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position        = spawnPos;
            rb.rotation        = preciseRot;
            rb.velocity        = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        _isAttacking = false;
        attackCoroutine = null;
    }

    private void OnProjectileHit(EnemyHp enemyStat)
    {
        if (enemyStat == null) return;

        if (myStat != null && myStat.HpOnHit > 0f)
            myStat.HealHp(myStat.HpOnHit);

        if (myStat != null && myStat.MpOnHit > 0f)
            myStat.RecoverMp(myStat.MpOnHit);
    }

    public override void OnHit() { }
}

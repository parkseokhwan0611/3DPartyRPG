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

    protected override void Start()
    {
        base.Start();
        myStat = GetComponent<CharacterStat>();

        if (myStat == null)
            Debug.LogError($"[HealerAttack] {gameObject.name}에 CharacterStat이 없습니다!");
    }

    protected override void ExecuteAttack()
    {
        attackCoroutine = StartCoroutine(AttackRoutine());
    }

    // ─────────────────────────────────────────────────────────────────
    // 공격 코루틴만 정확히 멈춤 (StatusEffectHandler 코루틴 건드리지 않음)
    // ─────────────────────────────────────────────────────────────────
    protected override void StopAttackCoroutine()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
    }

    private IEnumerator AttackRoutine()
    {
        if (currentTarget == null) yield break;

        // 공격 중 이동 중단
        if (agent != null)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        anim.SetTrigger("doNormalAttack");

        yield return new WaitForSeconds(damageDelay);

        if (currentTarget == null) yield break;

        Vector3 spawnPos      = firePoint != null ? firePoint.position : transform.position;
        Vector3 preciseDir    = (TargetPosition - spawnPos).normalized;
        Quaternion preciseRot = Quaternion.LookRotation(preciseDir);

        var effect = ObjectPoolManager.instance.GetGo(projectileName);
        if (effect == null) yield break;

        effect.transform.position = spawnPos;
        effect.transform.rotation = preciseRot;

        float damage = myStat.TotalAp;

        if (Random.value <= myStat.TotalCritRate)
        {
            damage *= myStat.TotalCritDamage;
            CinemachineShake.Instance.ShakeCamera(10f, .2f);
        }

        ProjectileScript proj = effect.GetComponent<ProjectileScript>();
        if (proj != null)
            proj.SetProjectileData(damage, gameObject, OnProjectileHit, myStat.GetDamageColor());

        Rigidbody rb = effect.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position        = spawnPos;
            rb.rotation        = preciseRot;
            rb.velocity        = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        attackCoroutine = null;
    }

    private void OnProjectileHit(EnemyHp enemyStat)
    {
        if (enemyStat == null) return;

        if (TargetHpScript.instance != null)
            TargetHpScript.instance.SetTarget(enemyStat);
    }

    public override void OnHit() { }
}
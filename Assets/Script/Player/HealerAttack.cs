using UnityEngine;
using System.Collections;

public class HealerAttack : AttackBase
{
    private CharacterStat myStat;

    [Header("Ranged Settings")]
    public string projectileName = "MagicBall";
    public Transform firePoint;
    public float damageDelay = 0.35f;

    protected override void Start()
    {
        base.Start();
        myStat = GetComponent<CharacterStat>();

        if (myStat == null)
            Debug.LogError($"[HealerAttack] {gameObject.name}에 CharacterStat이 없습니다!");
    }

    protected override void ExecuteAttack()
    {
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        if (currentTarget == null) yield break;

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

        ProjectileScript proj = effect.GetComponent<ProjectileScript>();
        if (proj != null)
        {
            // 색상도 함께 전달
            proj.SetProjectileData(damage, gameObject, OnProjectileHit, myStat.GetDamageColor());
        }

        Rigidbody rb = effect.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position        = spawnPos;
            rb.rotation        = preciseRot;
            rb.velocity        = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void OnProjectileHit(EnemyHp enemyStat)
    {
        if (enemyStat == null) return;

        if (TargetHpScript.instance != null)
            TargetHpScript.instance.SetTarget(enemyStat);
    }

    public override void OnHit() { }
}
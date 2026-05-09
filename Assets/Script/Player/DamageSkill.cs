using UnityEngine;
using System.Collections;

public class DamageSkill : SkillBase
{
    private DamageSkillData damageData;

    private DamageSkillData GetDamageData()
    {
        if (damageData == null)
            damageData = skillData as DamageSkillData;

        if (damageData == null)
            Debug.LogError($"[DamageSkill] {gameObject.name}의 skillData가 DamageSkillData가 아닙니다!");

        return damageData;
    }

    protected override IEnumerator ExecuteSkill(Transform target)
    {
        var data = GetDamageData();
        if (data == null) yield break;
        if (target == null) yield break;

        // 1. 타겟 방향으로 회전
        Vector3 dir = (target.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        // 2. 애니메이션 실행
        if (anim != null && !string.IsNullOrEmpty(data.animTriggerName))
            anim.SetTrigger(data.animTriggerName);

        // 3. 이펙트 스폰 타이밍까지 대기
        if (data.effectSpawnDelay > 0f)
            yield return new WaitForSeconds(data.effectSpawnDelay);

        // 4. 이펙트 스폰
        SpawnEffect();

        // 5. 단일 타겟 or 범위 타격 판정
        if (data.isAoe)
            ApplyAoeDamage();
        else
            ApplySingleDamage(target);

        // 6. 애니메이션 나머지 대기
        float remaining = data.animDuration - data.effectSpawnDelay;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    // ─────────────────────────────────────────────────────────────────
    // 데미지 계산 (공통)
    // ─────────────────────────────────────────────────────────────────

    private float CalculateDamage()
    {
        if (myStat == null) return 0f;

        float baseStat   = damageData.useAp ? myStat.TotalAp : myStat.TotalAtk;
        float damage     = baseStat * damageData.GetDamageMultiplier(skillLevel);

        // 치명타 판정
        if (Random.value <= myStat.TotalCritRate)
            damage *= myStat.TotalCritDamage;

        return damage;
    }

    // ─────────────────────────────────────────────────────────────────
    // 단일 타겟 공격
    // ─────────────────────────────────────────────────────────────────

    private void ApplySingleDamage(Transform target)
    {
        EnemyHp enemyHp = target.GetComponent<EnemyHp>();
        if (enemyHp == null) return;

        enemyHp.TakeDamage(CalculateDamage(), gameObject, myStat.GetDamageColor());

        if (TargetHpScript.instance != null)
            TargetHpScript.instance.SetTarget(enemyHp);
    }

    // ─────────────────────────────────────────────────────────────────
    // 범위 공격 (isAoe = true)
    // ─────────────────────────────────────────────────────────────────

    private void ApplyAoeDamage()
    {
        float range    = damageData.GetRange(skillLevel);
        Vector3 hitPos = transform.position + transform.forward * (range * 0.5f);

        Collider[] hitCols = Physics.OverlapSphere(hitPos, range, LayerMask.GetMask("Enemy"));

        bool isTargetSet = false;

        foreach (Collider col in hitCols)
        {
            EnemyHp enemyHp = col.GetComponent<EnemyHp>();
            if (enemyHp == null) continue;

            enemyHp.TakeDamage(CalculateDamage(), gameObject, myStat.GetDamageColor());

            if (!isTargetSet && TargetHpScript.instance != null)
            {
                TargetHpScript.instance.SetTarget(enemyHp);
                isTargetSet = true;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 이펙트 스폰
    // ─────────────────────────────────────────────────────────────────

    private void SpawnEffect()
    {
        if (string.IsNullOrEmpty(damageData.effectPoolKey)) return;
        if (ObjectPoolManager.instance == null) return;

        var effect = ObjectPoolManager.instance.GetGo(damageData.effectPoolKey);
        if (effect != null)
        {
            effect.transform.position = transform.position + transform.forward;
            effect.transform.rotation = transform.rotation;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 에디터 기즈모
    // ─────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (damageData == null || !damageData.isAoe) return;

        Gizmos.color = Color.yellow;
        float range  = damageData.GetRange(skillLevel);
        Vector3 hitPos = transform.position + transform.forward * (range * 0.5f);
        Gizmos.DrawWireSphere(hitPos, range);
    }
}
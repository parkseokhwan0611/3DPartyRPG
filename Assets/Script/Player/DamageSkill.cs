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

        // 일반 공격 강제 중단
        AttackBase attackBase = GetComponent<AttackBase>();
        if (attackBase != null)
        {
            attackBase.StopAllCoroutines();
            attackBase.ResetAttackCooldown();
        }

        // 애니메이터 초기화 — 현재 재생 중인 공격 모션 즉시 종료
        if (anim != null)
        {
            anim.ResetTrigger("doNormalAttack");
            // Play로 강제로 Idle 상태로 복귀
            anim.Play("Idle", 0, 0f);
        }

        // 두 프레임 대기 후 스킬 트리거
        yield return null;
        yield return null;

        Vector3 dir = (target.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        if (anim != null && !string.IsNullOrEmpty(data.animTriggerName))
        {
            anim.ResetTrigger(data.animTriggerName);
            yield return null;
            anim.SetTrigger(data.animTriggerName);
        }

        if (data.effectSpawnDelay > 0f)
            yield return new WaitForSeconds(data.effectSpawnDelay);

        SpawnEffect();

        if (data.isAoe) ApplyAoeDamage();
        else            ApplySingleDamage(target);

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
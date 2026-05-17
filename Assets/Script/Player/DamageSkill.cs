using UnityEngine;
using System.Collections;

public class DamageSkill : SkillBase
{
    private int enemyLayer;

    private DamageSkillData damageData;

    protected override void Awake()
    {
        base.Awake();
        enemyLayer = LayerMask.GetMask("Enemy");
    }

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

        // 1. 타겟 방향 회전
        Vector3 dir = (target.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        // 2. 애니메이션 즉시 실행
        if (anim != null && !string.IsNullOrEmpty(data.animTriggerName))
        {
            anim.ResetTrigger(data.animTriggerName);
            anim.SetTrigger(data.animTriggerName);
        }

        // 3. 이펙트 스폰 타이밍 대기
        if (data.effectSpawnDelay > 0f)
            yield return new WaitForSeconds(data.effectSpawnDelay);

        // 4. 이펙트 스폰
        SpawnEffect(data);

        // 5. 데미지 판정
        if (data.isAoe) ApplyAoeDamage(data);
        else            ApplySingleDamage(data, target);

        // 6. 어그로 적용
        ApplyAggro(data);

        // 7. 연계 버프 적용
        if (data.hasNextSkillBuff)
            myStat.ApplyNextSkillBuff(data.nextSkillDamageBonus, data.nextSkillBuffDuration);

        // ★ 판정 완료 → 후딜 캔슬 허용
        ReleaseActivating();

        // 8. 후딜 대기
        float remaining = data.animDuration - data.effectSpawnDelay;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    // ─────────────────────────────────────────────────────────────────
    // 데미지 계산
    // ─────────────────────────────────────────────────────────────────

    private float CalculateDamage(DamageSkillData data)
    {
        if (myStat == null) return 0f;

        float baseStat = data.useAp ? myStat.TotalAp : myStat.TotalAtk;
        float damage   = baseStat * data.GetDamageMultiplier(skillLevel);

        float bonus = myStat.ConsumeNextSkillBonus();
        damage *= (1f + bonus);

        if (Random.value <= myStat.TotalCritRate)
            damage *= myStat.TotalCritDamage;

        return damage;
    }

    // ─────────────────────────────────────────────────────────────────
    // 단일 공격
    // ─────────────────────────────────────────────────────────────────

    private void ApplySingleDamage(DamageSkillData data, Transform target)
    {
        EnemyHp enemyHp = target.GetComponent<EnemyHp>();
        if (enemyHp == null) return;

        enemyHp.TakeDamage(CalculateDamage(data), gameObject, myStat.GetDamageColor());
        ApplyOnHitDebuffs(data, target);

        if (TargetHpScript.instance != null)
            TargetHpScript.instance.SetTarget(enemyHp);
    }

    // ─────────────────────────────────────────────────────────────────
    // 범위 공격
    // ─────────────────────────────────────────────────────────────────

    private void ApplyAoeDamage(DamageSkillData data)
    {
        float range    = data.GetRange(skillLevel);
        Vector3 hitPos = transform.position + transform.forward * (range * 0.5f);

        Collider[] hitCols = Physics.OverlapSphere(hitPos, range, enemyLayer);
        bool isTargetSet   = false;

        foreach (Collider col in hitCols)
        {
            EnemyHp enemyHp = col.GetComponent<EnemyHp>();
            if (enemyHp == null) continue;

            enemyHp.TakeDamage(CalculateDamage(data), gameObject, myStat.GetDamageColor());
            ApplyOnHitDebuffs(data, col.transform);

            if (!isTargetSet && TargetHpScript.instance != null)
            {
                TargetHpScript.instance.SetTarget(enemyHp);
                isTargetSet = true;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 부가 디버프
    // ─────────────────────────────────────────────────────────────────

    private void ApplyOnHitDebuffs(DamageSkillData data, Transform target)
    {
        if (data.onHitDebuffs == null || data.onHitDebuffs.Count == 0) return;

        StatusEffectHandler handler = target.GetComponent<StatusEffectHandler>();
        if (handler == null) return;

        foreach (var debuff in data.onHitDebuffs)
        {
            handler.ApplyEffect(new StatusEffect(
                debuff.effectType,
                debuff.GetValue(skillLevel),
                debuff.GetDuration(skillLevel),
                gameObject
            ));
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 어그로 적용
    // ─────────────────────────────────────────────────────────────────

    private void ApplyAggro(DamageSkillData data)
    {
        if (!data.hasAggroEffect) return;

        // 범위 내 모든 몬스터에게 어그로 추가
        Collider[] cols = Physics.OverlapSphere(
            transform.position, data.aggroRange, enemyLayer);

        foreach (Collider col in cols)
        {
            // IAggroable 인터페이스 방식 (나중에 몬스터 종류 늘어날 때 확장 용이)
            GoblinThiefMaleScript goblin = col.GetComponent<GoblinThiefMaleScript>();
            if (goblin != null)
                goblin.AddAggro(transform, data.aggroAmount);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 이펙트 스폰
    // ─────────────────────────────────────────────────────────────────

    private void SpawnEffect(DamageSkillData data)
    {
        if (string.IsNullOrEmpty(data.effectPoolKey)) return;
        if (ObjectPoolManager.instance == null) return;

        var effect = ObjectPoolManager.instance.GetGo(data.effectPoolKey);
        if (effect != null)
        {
            effect.transform.position = transform.position + transform.rotation * data.effectSpawnOffset;
            effect.transform.rotation = transform.rotation;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (damageData == null) return;

        if (damageData.isAoe)
        {
            Gizmos.color   = Color.yellow;
            float range    = damageData.GetRange(skillLevel);
            Vector3 hitPos = transform.position + transform.forward * (range * 0.5f);
            Gizmos.DrawWireSphere(hitPos, range);
        }

        if (damageData.hasAggroEffect)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, damageData.aggroRange);
        }
    }
}
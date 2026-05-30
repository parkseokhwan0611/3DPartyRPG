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

        // 4. 이펙트 스폰 + 데미지 판정
        if (data.spawnAtTarget)
        {
            // 장판형: 타겟 위치에 스폰, SkillZone이 직접 판정
            SpawnZone(data, target);
        }
        else
        {
            // 일반형: 시전자 위치에 스폰, 즉시 판정
            SpawnEffect(data);
            if (data.isAoe) ApplyAoeDamage(data);
            else            ApplySingleDamage(data, target);
        }

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

        // (물리/마법 데미지 + 스탯 배율 합산) * 스킬 계수
        float statBonus = data.GetTotalStatBonus(skillLevel, GetScalingStatValue);
        float damage    = (baseStat + statBonus) * data.GetDamageMultiplier(skillLevel);

        float bonus = myStat.ConsumeNextSkillBonus();
        damage *= (1f + bonus);

        if (Random.value <= myStat.TotalCritRate)
            damage *= myStat.TotalCritDamage;

        return damage;
    }

    private float GetScalingStatValue(DamageSkillData.ScalingStat stat)
    {
        if (myStat == null) return 0f;
        return stat switch
        {
            DamageSkillData.ScalingStat.Str => myStat.TotalStr,
            DamageSkillData.ScalingStat.Vit => myStat.TotalVit,
            DamageSkillData.ScalingStat.Int => myStat.TotalInt,
            DamageSkillData.ScalingStat.Fth => myStat.TotalFth,
            _                               => 0f,
        };
    }

    // ─────────────────────────────────────────────────────────────────
    // 단일 공격
    // ─────────────────────────────────────────────────────────────────

    private void ApplySingleDamage(DamageSkillData data, Transform target)
    {
        EnemyHp enemyHp = target.GetComponent<EnemyHp>();
        if (enemyHp == null) return;

        enemyHp.TakeDamage(CalculateDamage(data), gameObject, myStat.GetDamageColor(!data.useAp));
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

            enemyHp.TakeDamage(CalculateDamage(data), gameObject, myStat.GetDamageColor(!data.useAp));
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

    // 일반형 — 시전자 위치에 이펙트 스폰
    private void SpawnEffect(DamageSkillData data)
    {
        if (string.IsNullOrEmpty(data.effectPoolKey)) return;
        if (ObjectPoolManager.instance == null) return;

        var effect = ObjectPoolManager.instance.GetGo(data.effectPoolKey);
        if (effect != null)
        {
            effect.transform.position = transform.position + transform.rotation * data.effectSpawnOffset;
            effect.transform.rotation = transform.rotation * Quaternion.Euler(data.effectSpawnRotation);
        }
    }

    // 장판형 — 타겟 위치에 이펙트 스폰 후 SkillZone에 파라미터 전달
    private void SpawnZone(DamageSkillData data, Transform target)
    {
        if (string.IsNullOrEmpty(data.effectPoolKey)) return;
        if (ObjectPoolManager.instance == null) return;

        var effect = ObjectPoolManager.instance.GetGo(data.effectPoolKey);
        if (effect == null) return;

        // 타겟 위치에 배치 (오프셋 적용)
        effect.transform.position = target.position + data.effectSpawnOffset;
        effect.transform.rotation = Quaternion.Euler(data.effectSpawnRotation);

        // SkillZone에 판정 파라미터 전달
        var zone = effect.GetComponent<SkillZone>();
        if (zone != null)
        {
            float damage = CalculateDamage(data);
            float range  = data.GetRange(skillLevel);
            zone.Setup(damage, range, data.zoneDamageInterval, data.zoneActivationDelay,
                       data.zoneHitOnce, gameObject, myStat.GetDamageColor(!data.useAp));
        }
        else
        {
            Debug.LogWarning($"[DamageSkill] '{data.effectPoolKey}' 오브젝트에 SkillZone 컴포넌트가 없습니다.");
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
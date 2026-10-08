using UnityEngine;
using System.Collections;

public class DamageSkill : SkillBase
{
    private static readonly Collider[] _hitBuffer = new Collider[16];

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

        PlaySkillSfx(data);

        // 3. 이펙트 스폰 타이밍 대기
        if (data.effectSpawnDelay > 0f)
            yield return new WaitForSeconds(data.effectSpawnDelay);

        // 4. 이펙트 스폰 + 데미지 판정
        if (data.throwGrenade)
        {
            // 수류탄형: 포물선으로 던지고, 착지 시 GrenadeProjectile이 직접 범위 판정
            ThrowGrenade(data, target);
        }
        else if (data.fireProjectile)
        {
            // 투사체형: 개수·간격·퍼짐 각도대로 발사, 맞는 순간 ProjectileScript가 데미지 적용.
            // 연사면 마지막 발까지 쏜 뒤에 후딜 캔슬을 허용한다
            yield return FireProjectiles(data, target);
        }
        else if (data.spawnAtTarget)
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

        // 5.5 부가 버프 적용 (시전자 자신 — 명중 여부와 무관하게 스킬 사용 시점에 1회 적용)
        ApplyOnCastBuffs(data);

        // 6. 어그로 적용
        ApplyAggro(data);

        // 7. 연계 버프 적용
        if (data.hasNextSkillBuff)
            myStat.ApplyNextSkillBuff(data.nextSkillDamageBonus, data.nextSkillBuffDuration);

        // ★ 판정 완료 → 후딜 캔슬 허용
        ReleaseActivating();

        // 8. 후딜 대기 — 연사에 쓴 시간만큼은 이미 지났으므로 뺀다
        float remaining = data.animDuration - data.effectSpawnDelay - FiringTime(data);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    // ─────────────────────────────────────────────────────────────────
    // 데미지 계산
    // ─────────────────────────────────────────────────────────────────

    private float CalculateDamage(DamageSkillData data, float comboBonus, out bool isCrit)
    {
        isCrit = false;
        if (myStat == null) return 0f;

        // (공격력 + 스탯 배율) × 스킬 계수 × (1 + 물리/마법 데미지 증가) — 예전엔 데미지 증가 %가 평타에만 적용됐었음
        float damage = data.GetRawDamage(skillLevel, myStat);

        damage *= (1f + comboBonus);

        isCrit = Random.value < myStat.TotalCritRate;
        if (isCrit)
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

        float comboBonus = myStat.ConsumeNextSkillBonus();
        float damage = CalculateDamage(data, comboBonus, out bool isCrit);
        if (data.useAp) enemyHp.TakeMagicDamage(damage, gameObject, isCrit);
        else             enemyHp.TakeDamage(damage, gameObject, isCrit);
        ApplyOnHitDebuffs(data, target);
        if (isCrit) CinemachineShake.ShakeCrit();
    }

    // ─────────────────────────────────────────────────────────────────
    // 범위 공격
    // ─────────────────────────────────────────────────────────────────

    private void ApplyAoeDamage(DamageSkillData data)
    {
        float range    = data.GetRange(skillLevel);
        Vector3 hitPos = transform.position + transform.forward * (range * 0.5f);

        int hitCount = Physics.OverlapSphereNonAlloc(hitPos, range, _hitBuffer, enemyLayer);
        float comboBonus = myStat.ConsumeNextSkillBonus();
        bool  anyCrit    = false;

        for (int i = 0; i < hitCount; i++)
        {
            EnemyHp enemyHp = _hitBuffer[i].GetComponent<EnemyHp>();
            if (enemyHp == null) continue;

            float damage = CalculateDamage(data, comboBonus, out bool isCrit);
            if (data.useAp) enemyHp.TakeMagicDamage(damage, gameObject, isCrit);
            else             enemyHp.TakeDamage(damage, gameObject, isCrit);
            ApplyOnHitDebuffs(data, _hitBuffer[i].transform);
            anyCrit |= isCrit;
        }

        // 치명타는 대상마다 따로 굴리지만 흔들림은 한 번만
        if (anyCrit) CinemachineShake.ShakeCrit();
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
                gameObject,
                debuff.EffectiveMode
            ));
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 부가 버프 (시전자 자신)
    // ─────────────────────────────────────────────────────────────────

    private void ApplyOnCastBuffs(DamageSkillData data)
    {
        if (data.onCastBuffs == null || data.onCastBuffs.Count == 0) return;
        if (statusHandler == null) return;

        foreach (var buff in data.onCastBuffs)
        {
            statusHandler.ApplyBuff(new StatusEffect(
                buff.effectType,
                buff.GetTotalValue(skillLevel, myStat),
                buff.GetDuration(skillLevel),
                gameObject,
                buff.valueMode
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
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position, data.aggroRange, _hitBuffer, enemyLayer);

        for (int i = 0; i < hitCount; i++)
        {
            // IAggroable 인터페이스 방식 (나중에 몬스터 종류 늘어날 때 확장 용이)
            BasicMonsterScript monster = _hitBuffer[i].GetComponent<BasicMonsterScript>();
            if (monster != null)
                monster.AddAggro(transform, data.aggroAmount);
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
            float comboBonus = myStat.ConsumeNextSkillBonus();
            float damage = CalculateDamage(data, comboBonus, out bool isCrit);
            float range  = data.GetRange(skillLevel);
            zone.Setup(damage, range, data.zoneDamageInterval, data.zoneActivationDelay,
                       data.zoneHitOnce, gameObject, data.useAp, data.zoneDuration, isCrit);
        }
        else
        {
            Debug.LogWarning($"[DamageSkill] '{data.effectPoolKey}' 오브젝트에 SkillZone 컴포넌트가 없습니다.");
        }
    }

    // 투사체형 연사에 걸리는 시간 (첫 발은 즉시, 이후 간격마다 한 발)
    private static float FiringTime(DamageSkillData data)
        => data.fireProjectile && !data.throwGrenade
            ? data.projectileInterval * (Mathf.Max(1, data.projectileCount) - 1)
            : 0f;

    // 투사체형 — 평타 총구에서 대상의 조준점을 향해 발사. 퍼짐 각도는 첫 발~마지막 발에 고르게 나눈다
    // (동시 발사면 부채꼴, 연사면 좌→우로 훑으며 쏘는 모양). 연계 보너스는 스킬 한 번에 한 번만 소모해서
    // 모든 발에 같이 적용하고, 치명타는 발마다 따로 굴린다
    private IEnumerator FireProjectiles(DamageSkillData data, Transform target)
    {
        if (string.IsNullOrEmpty(data.effectPoolKey) || ObjectPoolManager.instance == null) yield break;

        int   count      = Mathf.Max(1, data.projectileCount);
        float comboBonus = myStat.ConsumeNextSkillBonus();
        var   ranged     = GetComponent<RangedAttack>();
        Transform aim    = target != null ? target.Find("AimTarget") : null;
        EnemyHp targetHp = target != null ? target.GetComponent<EnemyHp>() : null;
        Vector3 lastDir  = transform.forward;
        var wait         = data.projectileInterval > 0f ? new WaitForSeconds(data.projectileInterval) : null;

        for (int i = 0; i < count; i++)
        {
            if (i > 0 && wait != null) yield return wait;

            Transform fp     = ranged != null ? ranged.CurrentFirePoint : null;
            Vector3 spawnPos = fp != null ? fp.position : transform.position + transform.rotation * data.effectSpawnOffset;

            // 연사 도중 대상이 죽거나 사라지면 마지막 방향 그대로 마저 쏜다
            if (target != null && target.gameObject.activeInHierarchy && (targetHp == null || !targetHp.isDead))
            {
                Vector3 aimPos = aim != null ? aim.position : target.position;
                Vector3 dir    = aimPos - spawnPos;
                if (dir.sqrMagnitude > 0.0001f) lastDir = dir.normalized;

                Vector3 flat = lastDir; flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(flat);
            }

            float angle = count > 1 ? Mathf.Lerp(-data.projectileSpreadAngle * 0.5f, data.projectileSpreadAngle * 0.5f, i / (float)(count - 1)) : 0f;
            Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up) * Quaternion.LookRotation(lastDir);

            SpawnProjectile(data, spawnPos, rot, comboBonus);

            // 발사음 — 연사면 발마다, 동시 발사면 같은 소리가 한 프레임에 겹치지 않게 첫 발에서 한 번만
            if ((i == 0 || wait != null) && !string.IsNullOrEmpty(data.projectileShotSfxKey))
                AudioManager.instance?.PlaySFX(data.projectileShotSfxKey);
        }
    }

    private void SpawnProjectile(DamageSkillData data, Vector3 spawnPos, Quaternion rot, float comboBonus)
    {
        var go = ObjectPoolManager.instance.GetGo(data.effectPoolKey);
        if (go == null) return;

        go.transform.SetPositionAndRotation(spawnPos, rot);

        var proj = go.GetComponent<ProjectileScript>();
        if (proj == null)
        {
            Debug.LogWarning($"[DamageSkill] '{data.effectPoolKey}' 오브젝트에 ProjectileScript 컴포넌트가 없습니다.");
            go.GetComponent<PoolAble>()?.ReleaseObject();
            return;
        }

        proj.SpawnMuzzleFlash(spawnPos, rot);

        float damage = CalculateDamage(data, comboBonus, out bool isCrit);
        proj.SetProjectileData(damage, gameObject, enemy =>
        {
            ApplyOnHitDebuffs(data, enemy.transform);
            if (isCrit) CinemachineShake.ShakeCrit(); // 쏠 때가 아니라 맞는 순간에 흔든다
        }, data.useAp, isCrit);

        // 풀에서 꺼낸 직후 Rigidbody가 이전 위치·속도를 들고 있으면 첫 물리 프레임에 엉뚱한 곳으로 튐 (RangedAttack과 동일)
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position        = spawnPos;
            rb.rotation        = rot;
            rb.velocity        = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // 수류탄형 — 몬스터 수류탄(MonsterGrenadeSkill)과 같은 GrenadeProjectile로 던진다.
    // 착지 지점은 지금 대상의 발밑으로 고정, 폭발 반경은 스킬 범위(GetRange)
    private void ThrowGrenade(DamageSkillData data, Transform target)
    {
        if (string.IsNullOrEmpty(data.effectPoolKey)) return;
        if (ObjectPoolManager.instance == null) return;

        var go = ObjectPoolManager.instance.GetGo(data.effectPoolKey);
        if (go == null) return;

        var grenade = go.GetComponent<GrenadeProjectile>();
        if (grenade == null)
        {
            Debug.LogWarning($"[DamageSkill] '{data.effectPoolKey}' 오브젝트에 GrenadeProjectile 컴포넌트가 없습니다.");
            go.GetComponent<PoolAble>()?.ReleaseObject();
            return;
        }

        Vector3 start   = transform.position + transform.rotation * data.effectSpawnOffset;
        Vector3 landing = target.position;
        go.transform.rotation = transform.rotation * Quaternion.Euler(data.effectSpawnRotation);

        // 치명타는 폭발에 맞은 대상마다 GrenadeProjectile이 굴리므로 여기서는 치명타 전 데미지만 넘긴다
        float comboBonus = myStat.ConsumeNextSkillBonus();
        float damage     = data.GetRawDamage(skillLevel, myStat) * (1f + comboBonus);

        grenade.Launch(start, landing, data.grenadeFlightDuration, data.grenadeArcHeight,
                       damage, data.GetRange(skillLevel), enemyLayer, gameObject, data.useAp,
                       hit => ApplyOnHitDebuffs(data, hit.transform),
                       myStat.TotalCritRate, myStat.TotalCritDamage, shakeOnCrit: true);
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
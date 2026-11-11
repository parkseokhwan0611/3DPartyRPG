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

        // 시전 딜레이 동안 대상이 죽어 사라질 수 있으므로 위치를 미리 기억 (장판·수류탄은 그 자리에 떨어진다)
        Vector3 targetPos = target.position;

        // 은신형 — 모션 시작 기준 타이밍으로 따로 돌린다 (이펙트 스폰 타이밍과 독립)
        float castStartTime = Time.time;
        if (data.vanishCaster)
        {
            CancelVanish();
            SetVanishing(true);
            _vanishRoutine = StartCoroutine(VanishRoutine(data, target, targetPos));
        }

        // 3. 이펙트 스폰 타이밍 대기
        if (data.effectSpawnDelay > 0f)
            yield return new WaitForSeconds(data.effectSpawnDelay);

        if (target != null) targetPos = target.position;

        // 4. 이펙트 스폰 + 데미지 판정
        if (data.throwGrenade)
        {
            // 수류탄형: 포물선으로 던지고, 착지 시 GrenadeProjectile이 직접 범위 판정
            ThrowGrenade(data, targetPos);
        }
        else if (data.fireProjectile)
        {
            // 투사체형: 개수·간격·퍼짐 각도대로 발사, 맞는 순간 ProjectileScript가 데미지 적용.
            // 연사면 마지막 발까지 쏜 뒤에 후딜 캔슬을 허용한다 (대상이 사라지면 마지막 방향으로 발사)
            yield return FireProjectiles(data, target);
        }
        else if (data.spawnAtTarget)
        {
            // 장판형: 타겟 위치에 스폰, SkillZone이 직접 판정
            SpawnZone(data, targetPos);
        }
        else
        {
            // 일반형: 시전자 위치에 스폰, 즉시 판정 (단일 대상이 사라졌으면 데미지 없음)
            SpawnEffect(data);
            if (data.isAoe)          ApplyAoeDamage(data);
            else if (target != null) ApplySingleDamage(data, target);
        }

        // 5.5 부가 버프 적용 (시전자 자신 — 명중 여부와 무관하게 스킬 사용 시점에 1회 적용)
        ApplyOnCastBuffs(data);

        // 6. 어그로 적용
        ApplyAggro(data);

        // 7. 연계 버프 적용
        if (data.hasNextSkillBuff)
            myStat.ApplyNextSkillBuff(data.nextSkillDamageBonus, data.nextSkillBuffDuration);

        // 은신형은 다시 나타날 때까지 다른 스킬로 끊지 못하게 발동 상태 유지
        if (data.vanishCaster)
        {
            while (_vanishRoutine != null) yield return null;
        }

        // ★ 판정 완료 → 후딜 캔슬 허용
        ReleaseActivating();

        // 8. 후딜 대기 — 연사에 쓴 시간만큼은 이미 지났으므로 뺀다 (은신형은 나타날 때까지 기다린 시간도 뺀다)
        float remaining = data.vanishCaster
            ? data.animDuration - (Time.time - castStartTime)
            : data.animDuration - data.effectSpawnDelay - FiringTime(data);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        // 교체·포탈·NPC 차단은 나타난 뒤 마무리 모션까지 — 스킬이 완전히 끝날 때 해제
        if (data.vanishCaster) SetVanishing(false);
    }

    // ─────────────────────────────────────────────────────────────────
    // 은신 연출 (검기 난무형) — 숨는 동안 무적, 끝나면 지정 위치에 다시 나타남
    // ─────────────────────────────────────────────────────────────────

    private Coroutine _vanishRoutine;
    private StatusEffect _vanishInvuln;

    // 파티원 중 누군가 은신(무적) 스킬을 쓰는 중인지 — 시전부터 스킬이 끝날 때까지 파티원 교체·포탈·NPC·아이템 줍기를 막는다
    private static int _vanishingCount;
    private bool _isVanishing;
    public static bool IsAnyVanishing => _vanishingCount > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetVanishingCount() => _vanishingCount = 0; // 도메인 리로드 없이 플레이 모드 재진입 대비

    private void SetVanishing(bool on)
    {
        if (_isVanishing == on) return;
        _isVanishing = on;
        _vanishingCount = Mathf.Max(0, _vanishingCount + (on ? 1 : -1));
    }
    private readonly System.Collections.Generic.List<Renderer> _hiddenRenderers = new System.Collections.Generic.List<Renderer>();
    private readonly System.Collections.Generic.List<(CanvasGroup group, float alpha)> _hiddenCanvasGroups
        = new System.Collections.Generic.List<(CanvasGroup group, float alpha)>();

    private IEnumerator VanishRoutine(DamageSkillData data, Transform target, Vector3 lastTargetPos)
    {
        // 무적 스킬 — 시전 순간부터 다시 나타날 때까지 무적 (숨는 건 모션 연출일 뿐, 몬스터 어그로는 그대로 유지).
        // 기존 상태 효과로 데미지·디버프(스턴 포함) 무시. 나타날 때 이 효과만 직접 지운다
        if (statusHandler != null)
        {
            _vanishInvuln = new StatusEffect(StatusEffectType.Invulnerable, 0f, data.VanishEndTime + 0.5f, gameObject);
            statusHandler.ApplyBuff(_vanishInvuln);
        }

        if (data.vanishStartTime > 0f)
            yield return new WaitForSeconds(data.vanishStartTime);

        SetCasterHidden(true);

        // 숨어 있는 동안 대상 위치를 계속 기억 (대상이 죽어 사라져도 마지막 위치 기준으로 나타남)
        float t = 0f;
        while (t < data.vanishDuration)
        {
            if (target != null && target.gameObject.activeInHierarchy) lastTargetPos = target.position;
            t += Time.deltaTime;
            yield return null;
        }

        MoveToReappearPosition(data, lastTargetPos);
        EndVanish();

        if (anim != null && !string.IsNullOrEmpty(data.reappearAnimTrigger))
        {
            anim.ResetTrigger(data.reappearAnimTrigger);
            anim.SetTrigger(data.reappearAnimTrigger);
        }

        _vanishRoutine = null;
    }

    private void MoveToReappearPosition(DamageSkillData data, Vector3 targetPos)
    {
        Vector3 toCaster = transform.position - targetPos;
        toCaster.y = 0f;
        toCaster = toCaster.sqrMagnitude > 0.0001f ? toCaster.normalized : -transform.forward;

        if (data.reappearPosition != DamageSkillData.ReappearPosition.Origin)
        {
            Vector3 dest = data.reappearPosition == DamageSkillData.ReappearPosition.FrontOfTarget
                ? targetPos + toCaster * data.reappearDistance
                : targetPos - toCaster * data.reappearDistance;

            // 벽 너머·낭떠러지로 나타나지 않게 가까운 NavMesh 위로 보정 (못 찾으면 제자리)
            if (UnityEngine.AI.NavMesh.SamplePosition(dest, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (agent != null && agent.enabled) agent.Warp(hit.position);
                else                                transform.position = hit.position;
            }
        }

        // 나타나면서 대상을 바라봄
        Vector3 look = targetPos - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(look);
    }

    // 렌더러만 끄고 캔버스는 투명하게 — 오브젝트를 끄면 Animator·NavMeshAgent·스탯 스크립트까지 멈추고, 천 시뮬레이션이 튐.
    // 꺼져 있는 자식(버프 아우라 등)의 렌더러까지 미리 꺼 둬서 숨어 있는 동안 아우라가 켜져도 안 보이게 하고,
    // 머리 위 HP/MP 바(Canvas)와 상태 텍스트(TextMeshPro — MeshRenderer라 위 렌더러 처리로 숨음)도 같이 숨긴다.
    // 캔버스는 Canvas.enabled를 끄고 켜면 그 아래 3D TextMeshPro(CanvasRenderer 없음)가 UI 레이캐스트 대상으로
    // 잘못 등록돼 MissingComponentException이 나므로, CanvasGroup 알파만 0으로 내린다.
    // 원래 꺼져 있던 렌더러는 건드리지 않도록 이번에 끈 것만 기억했다가 되돌린다
    private void SetCasterHidden(bool hidden)
    {
        if (hidden)
        {
            if (_hiddenRenderers.Count > 0 || _hiddenCanvasGroups.Count > 0) return;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                r.enabled = false;
                _hiddenRenderers.Add(r);
            }
            foreach (var c in GetComponentsInChildren<Canvas>(true))
            {
                if (!c.TryGetComponent(out CanvasGroup group)) group = c.gameObject.AddComponent<CanvasGroup>();
                _hiddenCanvasGroups.Add((group, group.alpha));
                group.alpha = 0f;
            }
        }
        else
        {
            foreach (var r in _hiddenRenderers)
                if (r != null) r.enabled = true;
            _hiddenRenderers.Clear();
            foreach (var (group, alpha) in _hiddenCanvasGroups)
                if (group != null) group.alpha = alpha;
            _hiddenCanvasGroups.Clear();
        }
    }

    private void EndVanish()
    {
        SetCasterHidden(false);
        if (_vanishInvuln != null)
        {
            statusHandler?.RemoveBuffEffect(_vanishInvuln);
            _vanishInvuln = null;
        }
    }

    // 끊기거나(파티원 교체·장비 변경 등) 비활성화되면 그 자리에서 바로 다시 보이게
    private void CancelVanish()
    {
        if (_vanishRoutine != null)
        {
            StopCoroutine(_vanishRoutine);
            _vanishRoutine = null;
        }
        EndVanish();
        SetVanishing(false);
    }

    protected override void OnForceStopped() => CancelVanish();

    private void OnDisable() => CancelVanish();

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
        float damage = CalculateDamage(data, comboBonus, out bool isCrit) * data.GetDebuffMultiplier(skillLevel, enemyHp);
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

            float damage = CalculateDamage(data, comboBonus, out bool isCrit) * data.GetDebuffMultiplier(skillLevel, enemyHp);
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

        float longest = 0f;
        foreach (var buff in data.onCastBuffs)
        {
            float duration = buff.GetDuration(skillLevel);
            longest = Mathf.Max(longest, duration);
            statusHandler.ApplyBuff(new StatusEffect(
                buff.effectType,
                buff.GetTotalValue(skillLevel, myStat),
                duration,
                gameObject,
                buff.valueMode
            ));

            // 버프 지속시간 동안 아우라 표시 (보호막처럼 시간 전에 깨질 수 있는 효과도 지속시간 기준)
            if (buff.useAura && myStat != null)
                myStat.ShowBuffAuraFor(buff.auraIndex, duration);
        }

        // 전투 퀵슬롯의 버프 지속시간 바 — 부가 버프가 여러 개면 가장 오래가는 것 기준
        StartBuffTimer(longest);
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
    private void SpawnZone(DamageSkillData data, Vector3 targetPos)
    {
        if (string.IsNullOrEmpty(data.effectPoolKey)) return;
        if (ObjectPoolManager.instance == null) return;

        var effect = ObjectPoolManager.instance.GetGo(data.effectPoolKey);
        if (effect == null) return;

        // 타겟 위치에 배치 (오프셋 적용). zoneFaceCaster면 캐릭터가 바라보는 방향(수평) 기준으로 오프셋·회전을 적용
        Quaternion facing = data.zoneFaceCaster ? FlatFacing(transform) : Quaternion.identity;
        effect.transform.position = targetPos + facing * data.effectSpawnOffset;
        effect.transform.rotation = facing * Quaternion.Euler(data.effectSpawnRotation);

        // SkillZone에 판정 파라미터 전달
        var zone = effect.GetComponent<SkillZone>();
        if (zone != null)
        {
            // 치명타는 여기서 굴리지 않고 확률·배율만 넘긴다 — SkillZone이 틱마다(적마다) 굴림
            float comboBonus = myStat.ConsumeNextSkillBonus();
            float damage = data.GetRawDamage(skillLevel, myStat) * (1f + comboBonus);
            float range  = data.GetRange(skillLevel);
            zone.Setup(damage, range, data.zoneDamageInterval, data.zoneActivationDelay,
                       data.zoneHitOnce, gameObject, data.useAp, data.zoneDuration,
                       myStat.TotalCritRate, myStat.TotalCritDamage,
                       data.GetDebuffBonusPerStack(skillLevel), data.maxDebuffStacks);
        }
        else
        {
            Debug.LogWarning($"[DamageSkill] '{data.effectPoolKey}' 오브젝트에 SkillZone 컴포넌트가 없습니다.");
        }
    }

    // 캐릭터가 바라보는 수평 방향 (위아래 기울기는 뺌 — 장판이 비스듬히 눕지 않게)
    public static Quaternion FlatFacing(Transform caster)
    {
        Vector3 fwd = caster.forward;
        fwd.y = 0f;
        return fwd.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(fwd.normalized, Vector3.up) : Quaternion.identity;
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
        // 맞았을 때 데미지 방식 (투사체 / 폭발 범위 / 둘 다) — 폭발 반경은 스킬 범위
        proj.ConfigureHitDamage(data.projectileHitDamage, data.GetRange(skillLevel),
                                data.GetDebuffBonusPerStack(skillLevel), data.maxDebuffStacks);

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
    private void ThrowGrenade(DamageSkillData data, Vector3 targetPos)
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
        Vector3 landing = targetPos;
        go.transform.rotation = transform.rotation * Quaternion.Euler(data.effectSpawnRotation);

        // 치명타는 폭발에 맞은 대상마다 GrenadeProjectile이 굴리므로 여기서는 치명타 전 데미지만 넘긴다
        float comboBonus = myStat.ConsumeNextSkillBonus();
        float damage     = data.GetRawDamage(skillLevel, myStat) * (1f + comboBonus);

        grenade.Launch(start, landing, data.grenadeFlightDuration, data.grenadeArcHeight,
                       damage, data.GetRange(skillLevel), enemyLayer, gameObject, data.useAp,
                       hit => ApplyOnHitDebuffs(data, hit.transform),
                       myStat.TotalCritRate, myStat.TotalCritDamage, shakeOnCrit: true,
                       debuffBonusPerStack: data.GetDebuffBonusPerStack(skillLevel), maxDebuffStacks: data.maxDebuffStacks);
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
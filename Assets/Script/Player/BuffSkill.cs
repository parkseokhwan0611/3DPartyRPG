using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BuffSkill : SkillBase
{
    private BuffSkillData buffData;

    private BuffSkillData GetBuffData()
    {
        if (buffData == null)
            buffData = skillData as BuffSkillData;

        if (buffData == null)
            Debug.LogError($"[BuffSkill] {gameObject.name}의 skillData가 BuffSkillData가 아닙니다!");

        return buffData;
    }

    protected override IEnumerator ExecuteSkill(Transform target)
    {
        var data = GetBuffData();
        if (data == null) yield break;

        // 1. 애니메이션 즉시 실행
        if (anim != null && !string.IsNullOrEmpty(data.animTriggerName))
        {
            anim.ResetTrigger(data.animTriggerName);
            anim.SetTrigger(data.animTriggerName);
        }

        PlaySkillSfx(data);

        // 2. 이펙트 타이밍 대기
        if (data.effectSpawnDelay > 0f)
            yield return new WaitForSeconds(data.effectSpawnDelay);

        if (data.summonOnly)
        {
            // 3~4. 소환수 전용 — 이펙트는 각 소환수 위치에
            ApplySummonOnlyBuff(data);
        }
        else
        {
            // 3. 시전자 이펙트 스폰 (OneShot 파티 버프만 — 개인 버프는 BuffPresentationRoutine이 대상 위치에 스폰하므로
            //    여기서도 꺼내면 같은 이펙트가 두 번 겹쳐 나왔음)
            if (data.effectStyle == BuffSkillData.EffectStyle.OneShot && data.isPartyBuff)
                SpawnEffect(data);

            // 4. 버프 적용
            if (data.isPartyBuff) ApplyPartyBuff(data);
            else                  ApplySelfBuff(data);
        }

        // 버프 적용 완료 → 후딜 캔슬 허용
        ReleaseActivating();

        // 5. 후딜 대기
        float remaining = data.animDuration - data.effectSpawnDelay;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    // ─────────────────────────────────────────────────────────────────
    // 소환수 전용 버프 (BuffSkillData.summonOnly)
    // ─────────────────────────────────────────────────────────────────

    private bool HasSummonTarget(BuffSkillData data)
    {
        foreach (var s in SummonUnit.All)
            if (data.IsSummonBuffTarget(s, myStat)) return true;
        return false;
    }

    // 대상 소환수가 1기 이상 있어야 사용 가능 (없으면 마나·쿨타임을 쓰지 않고 거절)
    public override bool CanUseNow
    {
        get
        {
            var data = GetBuffData();
            if (data == null) return false;
            return !data.summonOnly || HasSummonTarget(data);
        }
    }

    public override string CannotUseMessage
    {
        get
        {
            var data = GetBuffData();
            if (data != null && data.summonOnly && !HasSummonTarget(data))
                return "소환수가 존재하지 않습니다.";
            return null;
        }
    }

    // 팔로워 자동 사용 — 소환수 전용 버프는 대상 소환수 근처에 적이 있을 때만 (전투 중일 때)
    private const float SummonBuffEnemyRange = 10f;
    public override bool IsWorthAutoUsing
    {
        get
        {
            var data = GetBuffData();
            if (data == null || !data.summonOnly) return true;
            float sqr = SummonBuffEnemyRange * SummonBuffEnemyRange;
            foreach (var s in SummonUnit.All)
            {
                if (!data.IsSummonBuffTarget(s, myStat)) continue;
                foreach (var e in EnemyHp.AllInstances)
                    if (e != null && !e.isDead && (e.transform.position - s.transform.position).sqrMagnitude <= sqr) return true;
            }
            return false;
        }
    }

    private void ApplySummonOnlyBuff(BuffSkillData data)
    {
        _revivedThisCast.Clear();
        bool any = false;
        for (int i = SummonUnit.All.Count - 1; i >= 0; i--)
        {
            var summon = SummonUnit.All[i];
            if (!data.IsSummonBuffTarget(summon, myStat)) continue;
            ApplyBuffEffectsToSummon(data, skillLevel, myStat, summon, anyAttackBonus: true);
            // Aura: 소환수의 buffAuras[auraIndex]를 지속시간 동안 켬 / OneShot: 소환수 위치에 이펙트 한 번
            if (data.effectStyle == BuffSkillData.EffectStyle.Aura) summon.ShowBuffAura(data.auraIndex, data.GetDuration(skillLevel));
            else                                                    SpawnTargetEffect(data, summon.transform);
            any = true;
        }

        // 스킬 슬롯 버프 남은 시간 표시
        if (any && data.HasDurationEffect) StartBuffTimer(data.GetDuration(skillLevel));
    }

    private void ApplySelfBuff(BuffSkillData data)
    {
        _revivedThisCast.Clear();
        // PartyManager에서 코루틴 실행: 시전자가 죽어 SetActive(false)되어도 코루틴 유지
        if (PartyManager.instance != null)
            PartyManager.instance.StartCoroutine(BuffPresentationRoutine(myStat, data));
    }

    private void ApplyPartyBuff(BuffSkillData data)
    {
        if (PartyManager.instance == null) return;

        // 부활을 먼저 — 그래야 아래 버프(무적 등)가 방금 일어난 파티원에게도 걸린다
        _revivedThisCast.Clear(); // 이전 시전 기록은 비움 (같은 캐릭터의 다른 무적 스킬에서 "무적"이 빠지지 않게)
        ReviveDeadMembers(data);

        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member == null) continue;
            if (member.CurrentState == PartyMemberScript.MemberState.Dead) continue;

            CharacterStat stat = member.StatComp;
            if (stat != null)
                PartyManager.instance.StartCoroutine(BuffPresentationRoutine(stat, data));
        }

        // 아군 전체 버프는 소환수에게도 — 소환수가 지원하는 효과(피해 감소·보호막·무적·공격력·치명타·이동 속도)만 적용
        for (int i = SummonUnit.All.Count - 1; i >= 0; i--)
        {
            var summon = SummonUnit.All[i];
            if (summon != null && summon.IsAlive) ApplyBuffEffectsToSummon(data, skillLevel, myStat, summon);
        }
    }

    // 이번 시전으로 부활한 파티원 — 상태 텍스트를 "부활"만 띄우려고 기억 (다음 부활 시전 때 비움)
    private readonly HashSet<CharacterStat> _revivedThisCast = new HashSet<CharacterStat>();

    private static bool HasInvulnerable(BuffSkillData data)
    {
        foreach (var effect in data.buffEffects)
            if (effect != null && effect.effectType == BuffSkillData.BuffEffectType.Invulnerable) return true;
        return false;
    }

    // ReviveDead 효과 — 쓰러진 파티원을 시전자 양옆·뒤쪽에 일으켜 세운다 (수치 = 부활 체력 비율)
    private void ReviveDeadMembers(BuffSkillData data)
    {
        float ratio = 0f;
        bool  has   = false;
        foreach (var effect in data.buffEffects)
        {
            if (effect == null || effect.effectType != BuffSkillData.BuffEffectType.ReviveDead) continue;
            has   = true;
            ratio = Mathf.Max(ratio, effect.GetValue(skillLevel) + GetScalingValue(effect, skillLevel, myStat));
        }
        if (!has) return;
        if (ratio <= 0f) ratio = 0.3f; // 수치 미설정 시 기본 30%

        int revived = 0;
        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member == null || member.CurrentState != PartyMemberScript.MemberState.Dead) continue;
            CharacterStat stat = member.StatComp;
            if (stat == null) continue;

            // 시전자 기준 오른쪽·왼쪽 번갈아, 살짝 뒤에 (겹쳐 서지 않게)
            float side   = (revived % 2 == 0 ? 1f : -1f) * (1.2f + 0.6f * (revived / 2));
            Vector3 spot = transform.position + transform.right * side - transform.forward * 0.5f;
            if (!stat.ReviveFromDeath(ratio, spot)) continue;
            revived++;
            _revivedThisCast.Add(stat);

            if (!string.IsNullOrEmpty(data.reviveEffectPoolKey) && ObjectPoolManager.instance != null)
            {
                var fx = ObjectPoolManager.instance.GetGo(data.reviveEffectPoolKey);
                if (fx != null) fx.transform.SetPositionAndRotation(member.transform.position, member.transform.rotation);
            }
        }
    }

    // anyAttackBonus: 소환수 전용 버프 — AtkBonus·ApBonus 어느 쪽이든 소환수 공격력 기준(물리/마법)에 맞춰 적용.
    // 파티 버프에서는 소환수 기준과 같은 쪽만 적용 (마법 소환수에게 물리 공격력 버프는 무시)
    private void ApplyBuffEffectsToSummon(BuffSkillData data, int level, CharacterStat caster, SummonUnit summon, bool anyAttackBonus = false)
    {
        float duration = data.GetDuration(level);
        foreach (var effect in data.buffEffects)
        {
            if (effect.effectType == BuffSkillData.BuffEffectType.Shield)
            {
                summon.ApplyShield(effect.GetValue(level) + GetScalingValue(effect, level, caster), duration);
                continue;
            }

            StatusEffectType? mapped = MapToStatusEffectType(effect.effectType);
            if (mapped == null || !SummonUnit.SupportsBuff(mapped.Value)) continue;
            if (anyAttackBonus && (mapped == StatusEffectType.AtkUp || mapped == StatusEffectType.ApUp))
                mapped = summon.UsesAp ? StatusEffectType.ApUp : StatusEffectType.AtkUp;

            float value = effect.GetValue(level) + GetScalingValue(effect, level, caster);
            ModifierMode mode = effect.IsPercent ? ModifierMode.Percent : ModifierMode.Flat;
            summon.ApplyBuff(new StatusEffect(mapped.Value, value, duration, gameObject, mode));
        }
    }

    // 수치 적용/지속시간/원복은 PartyStatusEffectHandler가 전담. 여기서는 연출(아우라/VFX)과 UI 타이머만 관리.
    private IEnumerator BuffPresentationRoutine(CharacterStat stat, BuffSkillData data)
    {
        if (stat == null) yield break;

        var handler = stat.GetComponent<PartyStatusEffectHandler>();
        ApplyBuffEffects(data, skillLevel, myStat, handler);

        // 무적 스킬 — 머리 위에 "무적" (이번 시전으로 부활한 파티원은 "부활"만 보이게 생략)
        if (HasInvulnerable(data) && !_revivedThisCast.Contains(stat) && handler != null && handler.IsInvulnerable)
            stat.ShowStatusText("무적", StatusTextColors.Invulnerable, Mathf.Min(2f, data.GetDuration(skillLevel)));

        // 디버프 해제·쿨 초기화만 있는 즉시 발동 스킬은 시전 이펙트만 내고, 버프 타이머 UI·아우라는 띄우지 않는다
        if (!data.HasDurationEffect)
        {
            if (data.effectStyle == BuffSkillData.EffectStyle.OneShot && !data.isPartyBuff)
                SpawnTargetEffect(data, stat.transform);
            yield break;
        }

        ShowBuffEffect(data, stat);

        float duration = data.GetDuration(skillLevel);

        // 시전자 본인 버프일 때 UI용 타이머 설정
        if (stat == myStat)
            StartBuffTimer(duration);

        yield return new WaitForSeconds(duration);

        if (stat == null) yield break;
        // 사망 여부와 관계없이 항상 해제 — 참조 카운트가 남으면 부활 후 같은 아우라가 영영 안 꺼짐
        HideBuffEffect(data, stat);
    }

    private void ShowBuffEffect(BuffSkillData data, CharacterStat stat)
    {
        // 평타 이펙트 교체는 시전자 본인만 — 파티 버프여도 다른 클래스의 평타(근접/원거리)에 엉뚱한 키가 들어가지 않게
        if (stat == myStat)
            stat.PushBasicAttackEffect(data.basicAttackEffectKey, data.basicAttackEffectRotation);

        switch (data.effectStyle)
        {
            case BuffSkillData.EffectStyle.OneShot:
                // 파티 버프는 시전자 위치에서만 이펙트 생성 (ExecuteSkill의 SpawnEffect로 처리됨)
                if (!data.isPartyBuff)
                    SpawnTargetEffect(data, stat.transform);
                break;
            case BuffSkillData.EffectStyle.Aura:
                // 참조 카운트 방식 — 같은 아우라를 쓰는 다른 버프가 남아있으면 꺼지지 않음
                stat.AcquireBuffAura(data.auraIndex);
                break;
        }
    }

    private void HideBuffEffect(BuffSkillData data, CharacterStat stat)
    {
        if (data.effectStyle == BuffSkillData.EffectStyle.Aura)
            stat.ReleaseBuffAura(data.auraIndex);
        if (stat == myStat)
            stat.PopBasicAttackEffect(data.basicAttackEffectKey);
    }

    // 스탯 반영은 PartyStatusEffectHandler.ApplyBuff(StatusEffect)에 위임 — 적용/지속시간/원복을 한 곳에서 관리
    private void ApplyBuffEffects(BuffSkillData data, int level, CharacterStat caster, PartyStatusEffectHandler targetHandler)
    {
        if (targetHandler == null) return;

        foreach (var effect in data.buffEffects)
        {
            // DispelDebuff는 즉시 발동 전용(지속시간 없음)
            if (effect.effectType == BuffSkillData.BuffEffectType.DispelDebuff)
            {
                targetHandler.DispelAllDebuffs();
                continue;
            }

            // Shield는 activeBuffs 목록/StatusEffectType이 아니라 PartyStatusEffectHandler의
            // 전용 CurrentShield 풀로 관리 (스택 시 수치는 합연산, 지속시간은 최신 것으로 갱신)
            if (effect.effectType == BuffSkillData.BuffEffectType.Shield)
            {
                float shieldAmount = effect.GetValue(level) + GetScalingValue(effect, level, caster);
                targetHandler.ApplyShield(shieldAmount, data.GetDuration(level), gameObject);
                continue;
            }

            // 가시 반사는 시전자 자신에게만 — 파티 버프로 설정돼 있어도 다른 파티원에게는 걸지 않는다
            // (반사 데미지를 누구 능력치로 계산할지 모호해지는 것을 막기 위함)
            if (effect.effectType == BuffSkillData.BuffEffectType.Thorns)
            {
                if (targetHandler.gameObject == gameObject)
                    targetHandler.ApplyThorns(data, effect, level, data.GetDuration(level));
                continue;
            }

            // 쿨타임 초기화는 즉시 발동 — 수치 = 초기화할 스킬 개수 (0 이하면 전부). 파티 버프면 각 파티원 자신의 스킬을 초기화.
            // 쿨 초기화 스킬(이 스킬 포함)은 SkillManager.ResetCooldowns에서 대상에서 빠짐
            if (effect.effectType == BuffSkillData.BuffEffectType.CooldownReset)
            {
                var targetSkills = targetHandler.GetComponent<SkillManager>();
                if (targetSkills != null)
                    targetSkills.ResetCooldowns(Mathf.RoundToInt(effect.GetValue(level) + GetScalingValue(effect, level, caster)));
                continue;
            }

            StatusEffectType? mapped = effect.effectType == BuffSkillData.BuffEffectType.OnHitDamage
                ? (data.onHitDamageIsMagic ? StatusEffectType.OnHitMagicDamageUp : StatusEffectType.OnHitPhysDamageUp)
                : MapToStatusEffectType(effect.effectType);
            if (mapped == null) continue;

            float flat    = effect.GetValue(level);
            float scaling = GetScalingValue(effect, level, caster);
            float value   = flat + scaling;

            // 퍼센트 버프의 기준 수치는 시전자가 아니라 버프를 받는 대상의 기본 수치
            ModifierMode mode = effect.IsPercent ? ModifierMode.Percent : ModifierMode.Flat;
            targetHandler.ApplyBuff(new StatusEffect(mapped.Value, value, data.GetDuration(level), gameObject, mode));
        }
    }

    private StatusEffectType? MapToStatusEffectType(BuffSkillData.BuffEffectType type)
    {
        switch (type)
        {
            case BuffSkillData.BuffEffectType.AtkBonus:      return StatusEffectType.AtkUp;
            case BuffSkillData.BuffEffectType.ApBonus:       return StatusEffectType.ApUp;
            case BuffSkillData.BuffEffectType.DefBonus:      return StatusEffectType.DefUp;
            case BuffSkillData.BuffEffectType.MagicResBonus: return StatusEffectType.MagicResUp;
            case BuffSkillData.BuffEffectType.CritRate:      return StatusEffectType.CritRateUp;
            case BuffSkillData.BuffEffectType.CritDamage:    return StatusEffectType.CritDamageUp;
            case BuffSkillData.BuffEffectType.MaxHpBonus:    return StatusEffectType.MaxHpUp;
            case BuffSkillData.BuffEffectType.HpOnHit:       return StatusEffectType.HpOnHitUp;
            case BuffSkillData.BuffEffectType.SpeedBonus:    return StatusEffectType.MoveSpeedUp;
            case BuffSkillData.BuffEffectType.ManaRegen:     return StatusEffectType.ManaRegen;
            case BuffSkillData.BuffEffectType.HpRegen:       return StatusEffectType.HpRegen;
            case BuffSkillData.BuffEffectType.DebuffImmune:  return StatusEffectType.DebuffImmune;
            case BuffSkillData.BuffEffectType.AtkSpeedBonus: return StatusEffectType.AtkSpeedUp;
            case BuffSkillData.BuffEffectType.DmgReduction:  return StatusEffectType.DmgReductionUp;
            case BuffSkillData.BuffEffectType.Invulnerable:  return StatusEffectType.Invulnerable;
            case BuffSkillData.BuffEffectType.MoveSpeedPenalty: return StatusEffectType.MoveSpeedPenalty;
            case BuffSkillData.BuffEffectType.PhysDmgBonus:  return StatusEffectType.PhysDmgUp;
            case BuffSkillData.BuffEffectType.MagicDmgBonus: return StatusEffectType.MagicDmgUp;
            case BuffSkillData.BuffEffectType.MainSkillDmgBonus: return StatusEffectType.MainSkillDmgUp;
            case BuffSkillData.BuffEffectType.MpCostIncrease:    return StatusEffectType.MpCostUp;
            default:                                         return null;
        }
    }

    private float GetScalingValue(BuffSkillData.BuffEffect effect, int level, CharacterStat caster)
    {
        if (caster == null || effect.scalingStat == BuffSkillData.ScalingStat.None) return 0f;

        float coeff = effect.GetScaling(level);
        if (coeff == 0f) return 0f;

        float stat = effect.scalingStat switch
        {
            BuffSkillData.ScalingStat.Str => caster.TotalStr,
            BuffSkillData.ScalingStat.Dex => caster.TotalDex,
            BuffSkillData.ScalingStat.Vit => caster.TotalVit,
            BuffSkillData.ScalingStat.Int => caster.TotalInt,
            BuffSkillData.ScalingStat.Fth => caster.TotalFth,
            _                             => 0f,
        };

        return stat * coeff;
    }

    private void SpawnEffect(BuffSkillData data)
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

    private void SpawnTargetEffect(BuffSkillData data, Transform target)
    {
        if (string.IsNullOrEmpty(data.effectPoolKey)) return;
        if (ObjectPoolManager.instance == null) return;

        var effect = ObjectPoolManager.instance.GetGo(data.effectPoolKey);
        if (effect != null)
        {
            effect.transform.position = target.position + target.rotation * data.effectSpawnOffset;
            effect.transform.rotation = target.rotation * Quaternion.Euler(data.effectSpawnRotation);
        }
    }
}

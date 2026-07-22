using UnityEngine;
using System.Collections;

public class BuffSkill : SkillBase
{
    // 시전자 기준 버프 남은 시간 / 총 시간 (UI 표시용)
    public float BuffRemaining { get; private set; } = 0f;
    private float buffTotal = 0f;
    public float BuffRemainingRatio => buffTotal > 0f ? Mathf.Clamp01(BuffRemaining / buffTotal) : 0f;

    protected override void Update()
    {
        base.Update();
        if (BuffRemaining > 0f)
            BuffRemaining -= Time.deltaTime;
    }

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

        // 3. 시전자 이펙트 스폰 (OneShot만 풀에서 꺼냄)
        if (data.effectStyle == BuffSkillData.EffectStyle.OneShot)
            SpawnEffect(data);

        // 4. 버프 적용
        if (data.isPartyBuff) ApplyPartyBuff(data);
        else                  ApplySelfBuff(data);

        // 버프 적용 완료 → 후딜 캔슬 허용
        ReleaseActivating();

        // 5. 후딜 대기
        float remaining = data.animDuration - data.effectSpawnDelay;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    private void ApplySelfBuff(BuffSkillData data)
    {
        // PartyManager에서 코루틴 실행: 시전자가 죽어 SetActive(false)되어도 코루틴 유지
        if (PartyManager.instance != null)
            PartyManager.instance.StartCoroutine(BuffPresentationRoutine(myStat, data));
    }

    private void ApplyPartyBuff(BuffSkillData data)
    {
        if (PartyManager.instance == null) return;

        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member == null) continue;
            if (member.CurrentState == PartyMemberScript.MemberState.Dead) continue;

            CharacterStat stat = member.GetComponent<CharacterStat>();
            if (stat != null)
                PartyManager.instance.StartCoroutine(BuffPresentationRoutine(stat, data));
        }
    }

    // 수치 적용/지속시간/원복은 PartyStatusEffectHandler가 전담. 여기서는 연출(아우라/VFX)과 UI 타이머만 관리.
    private IEnumerator BuffPresentationRoutine(CharacterStat stat, BuffSkillData data)
    {
        if (stat == null) yield break;

        var handler = stat.GetComponent<PartyStatusEffectHandler>();
        ApplyBuffEffects(data, skillLevel, myStat, handler);
        ShowBuffEffect(data, stat);

        float duration = data.GetDuration(skillLevel);

        // 시전자 본인 버프일 때 UI용 타이머 설정
        if (stat == myStat)
        {
            buffTotal     = duration;
            BuffRemaining = duration;
        }

        yield return new WaitForSeconds(duration);

        if (stat == null) yield break;
        // 사망 여부와 관계없이 항상 해제 — 참조 카운트가 남으면 부활 후 같은 아우라가 영영 안 꺼짐
        HideBuffEffect(data, stat);
    }

    private void ShowBuffEffect(BuffSkillData data, CharacterStat stat)
    {
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

            StatusEffectType? mapped = MapToStatusEffectType(effect.effectType);
            if (mapped == null) continue; // SpeedBonus/Shield/ManaRegen/HpRegen/DebuffImmune: 미구현 (기존과 동일)

            float flat    = effect.GetValue(level);
            float scaling = GetScalingValue(effect, level, caster);
            float value   = flat + scaling;

            targetHandler.ApplyBuff(new StatusEffect(mapped.Value, value, data.GetDuration(level), gameObject));
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

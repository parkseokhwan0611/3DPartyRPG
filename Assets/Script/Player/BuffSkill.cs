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
            PartyManager.instance.StartCoroutine(BuffRoutine(myStat, data));
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
                PartyManager.instance.StartCoroutine(BuffRoutine(stat, data));
        }
    }

    private IEnumerator BuffRoutine(CharacterStat stat, BuffSkillData data)
    {
        if (stat == null) yield break;

        if (DataManager.instance == null) yield break;
        if (stat.partyIndex < 0 || stat.partyIndex >= DataManager.instance.partyStatuses.Count) yield break;
        var status  = DataManager.instance.partyStatuses[stat.partyIndex];
        var handler = stat.GetComponent<PartyStatusEffectHandler>();

        ApplyBuffEffects(status, data, skillLevel, 1f, myStat, handler);
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
        var buffMember = stat.GetComponent<PartyMemberScript>();
        // 사망 여부와 관계없이 스탯 버프를 항상 되돌려 CharacterStatus 영구 오염 방지
        ApplyBuffEffects(status, data, skillLevel, -1f, myStat, handler);
        if (buffMember == null || buffMember.CurrentState != PartyMemberScript.MemberState.Dead)
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
                stat.ActivateBuffAura(data.auraIndex);
                break;
        }
    }

    private void HideBuffEffect(BuffSkillData data, CharacterStat stat)
    {
        if (data.effectStyle == BuffSkillData.EffectStyle.Aura)
            stat.DeactivateBuffAura(data.auraIndex);
    }

    private void ApplyBuffEffects(CharacterStatus status, BuffSkillData data, int level, float multiplier, CharacterStat caster, PartyStatusEffectHandler targetHandler = null)
    {
        foreach (var effect in data.buffEffects)
        {
            // DispelDebuff는 즉시 발동 전용 — 적용 시(multiplier>0)에만 실행, 만료 시 되돌리지 않음
            if (effect.effectType == BuffSkillData.BuffEffectType.DispelDebuff)
            {
                if (multiplier > 0f)
                    targetHandler?.DispelAllDebuffs();
                continue;
            }

            float flat    = effect.GetValue(level);
            float scaling = GetScalingValue(effect, level, caster);
            float value   = (flat + scaling) * multiplier;

            switch (effect.effectType)
            {
                case BuffSkillData.BuffEffectType.AtkBonus:      status.addedStr        += value; break;
                case BuffSkillData.BuffEffectType.ApBonus:       status.addedInt        += value; break;
                case BuffSkillData.BuffEffectType.DefBonus:      status.addedDef        += value; break;
                case BuffSkillData.BuffEffectType.MagicResBonus: status.addedMagicRes   += value; break;
                case BuffSkillData.BuffEffectType.CritRate:      status.addedCritRate   += value; break;
                case BuffSkillData.BuffEffectType.CritDamage:    status.addedCritDamage += value; break;
                case BuffSkillData.BuffEffectType.MaxHpBonus:    status.addedVit        += value; break;
                case BuffSkillData.BuffEffectType.HpOnHit:       status.hpOnHit         += value; break;
            }
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

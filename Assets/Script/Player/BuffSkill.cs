using UnityEngine;
using System.Collections;

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

        // 2. 이펙트 타이밍 대기
        if (data.effectSpawnDelay > 0f)
            yield return new WaitForSeconds(data.effectSpawnDelay);

        // 3. 시전자 이펙트 스폰 (OneShot만 풀에서 꺼냄)
        if (data.effectStyle == BuffSkillData.EffectStyle.OneShot)
            SpawnEffect(data);

        // 4. 버프 적용
        if (data.isPartyBuff) ApplyPartyBuff(data);
        else                  ApplySelfBuff(data);

        // ★ 버프 적용 완료 → 후딜 캔슬 허용
        ReleaseActivating();

        // 5. 후딜 대기
        float remaining = data.animDuration - data.effectSpawnDelay;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    private void ApplySelfBuff(BuffSkillData data)
    {
        StartCoroutine(BuffRoutine(myStat, data));
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
                StartCoroutine(BuffRoutine(stat, data));
        }
    }

    private IEnumerator BuffRoutine(CharacterStat stat, BuffSkillData data)
    {
        if (stat == null) yield break;

        if (DataManager.instance == null) yield break;
        if (stat.partyIndex < 0 || stat.partyIndex >= DataManager.instance.partyStatuses.Count) yield break;
        var status = DataManager.instance.partyStatuses[stat.partyIndex];

        ApplyBuffEffects(status, data, skillLevel, 1f, myStat);
        ShowBuffEffect(data, stat);

        yield return new WaitForSeconds(data.GetDuration(skillLevel));

        ApplyBuffEffects(status, data, skillLevel, -1f, myStat);
        HideBuffEffect(data, stat);
    }

    // 버프 시작 시 이펙트 표시
    private void ShowBuffEffect(BuffSkillData data, CharacterStat stat)
    {
        switch (data.effectStyle)
        {
            case BuffSkillData.EffectStyle.OneShot:
                SpawnTargetEffect(data, stat.transform);
                break;

            case BuffSkillData.EffectStyle.Aura:
                stat.ActivateBuffAura(data.auraIndex);
                break;
        }
    }

    // 버프 종료 시 이펙트 정리 (OneShot은 이미 자동 반환되므로 아우라만 처리)
    private void HideBuffEffect(BuffSkillData data, CharacterStat stat)
    {
        if (data.effectStyle == BuffSkillData.EffectStyle.Aura)
            stat.DeactivateBuffAura(data.auraIndex);
    }

    private void ApplyBuffEffects(CharacterStatus status, BuffSkillData data, int level, float multiplier, CharacterStat caster)
    {
        foreach (var effect in data.buffEffects)
        {
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
            }
        }
    }

    // 시전자 스탯 * 계수 계산
    private float GetScalingValue(BuffSkillData.BuffEffect effect, int level, CharacterStat caster)
    {
        if (caster == null || effect.scalingStat == BuffSkillData.ScalingStat.None) return 0f;

        float coeff = effect.GetScaling(level);
        float stat  = effect.scalingStat switch
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
            effect.transform.position = transform.position;
            effect.transform.rotation = transform.rotation;
        }
    }

    private void SpawnTargetEffect(BuffSkillData data, Transform target)
    {
        if (string.IsNullOrEmpty(data.effectPoolKey)) return;
        if (ObjectPoolManager.instance == null) return;

        var effect = ObjectPoolManager.instance.GetGo(data.effectPoolKey);
        if (effect != null)
        {
            effect.transform.position = target.position;
            effect.transform.rotation = target.rotation;
        }
    }
}
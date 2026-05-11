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

        // 1. 애니메이션 실행
        if (anim != null && !string.IsNullOrEmpty(data.animTriggerName))
            anim.SetTrigger(data.animTriggerName);

        // 2. 이펙트 스폰 타이밍까지 대기
        if (data.effectSpawnDelay > 0f)
            yield return new WaitForSeconds(data.effectSpawnDelay);

        // 3. 이펙트 스폰
        SpawnEffect(data);

        // 4. 파티 버프 or 개인 버프
        if (data.isPartyBuff)
            ApplyPartyBuff(data);
        else
            ApplySelfBuff(data);

        // 5. 애니메이션 나머지 대기
        float remaining = data.animDuration - data.effectSpawnDelay;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    // ─────────────────────────────────────────────────────────────────
    // 버프 적용
    // ─────────────────────────────────────────────────────────────────

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

        var status = DataManager.instance?.partyStatuses[stat.partyIndex];
        if (status == null) yield break;

        float duration = data.GetDuration(skillLevel);

        // 버프 적용
        ApplyBuffEffects(status, data, skillLevel, 1f);

        // 버프 이펙트 (대상 위에)
        SpawnTargetEffect(data, stat.transform);

        // 지속시간 대기
        yield return new WaitForSeconds(duration);

        // 버프 해제
        ApplyBuffEffects(status, data, skillLevel, -1f);
    }

    // multiplier: 1f = 적용, -1f = 해제
    private void ApplyBuffEffects(CharacterStatus status, BuffSkillData data, int level, float multiplier)
    {
        foreach (var effect in data.buffEffects)
        {
            float value = effect.GetValue(level) * multiplier;

            switch (effect.effectType)
            {
                case BuffSkillData.BuffEffectType.AtkBonus:
                    status.addedStr += value;
                    break;
                case BuffSkillData.BuffEffectType.ApBonus:
                    status.addedInt += value;
                    break;
                case BuffSkillData.BuffEffectType.DefBonus:
                    status.addedDef += value;
                    break;
                case BuffSkillData.BuffEffectType.CritRate:
                    status.addedCritRate += value;
                    break;
                case BuffSkillData.BuffEffectType.CritDamage:
                    status.addedCritDamage += value;
                    break;
                case BuffSkillData.BuffEffectType.MaxHpBonus:
                    status.addedVit += value;
                    break;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 이펙트 스폰
    // ─────────────────────────────────────────────────────────────────

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
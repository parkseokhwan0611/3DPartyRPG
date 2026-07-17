using UnityEngine;
using System.Collections;

public class HealSkill : SkillBase
{
    private HealSkillData healData;

    private HealSkillData GetHealData()
    {
        if (healData == null)
            healData = skillData as HealSkillData;

        if (healData == null)
            Debug.LogError($"[HealSkill] {gameObject.name}의 skillData가 HealSkillData가 아닙니다!");

        return healData;
    }

    protected override IEnumerator ExecuteSkill(Transform target)
    {
        var data = GetHealData();
        if (data == null) yield break;

        // 1. 애니메이션 즉시 실행
        if (anim != null && !string.IsNullOrEmpty(data.animTriggerName))
        {
            anim.ResetTrigger(data.animTriggerName);
            anim.SetTrigger(data.animTriggerName);
        }

        if (!string.IsNullOrEmpty(data.sfxKey))
            AudioManager.instance?.PlaySFX(data.sfxKey);

        // 2. 이펙트 타이밍 대기
        if (data.effectSpawnDelay > 0f)
            yield return new WaitForSeconds(data.effectSpawnDelay);

        // 3. 시전 이펙트
        SpawnCasterEffect(data);

        // 4. 힐 적용
        if (data.targetType == HealSkillData.HealTargetType.Party)
            ApplyPartyHeal(data);
        else
            ApplySingleHeal(data, target);

        // ★ 힐 적용 완료 → 후딜 캔슬 허용
        ReleaseActivating();

        // 5. 후딜 대기
        float remaining = data.animDuration - data.effectSpawnDelay;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    private void ApplySingleHeal(HealSkillData data, Transform target)
    {
        CharacterStat targetStat = target != null
            ? target.GetComponent<CharacterStat>()
            : myStat;

        if (targetStat == null) return;

        float healAmount = CalculateHeal(data);
        ApplyHeal(targetStat, healAmount, data);
        targetStat.ShowHealAura();
    }

    private void ApplyPartyHeal(HealSkillData data)
    {
        if (PartyManager.instance == null) return;

        float healAmount = CalculateHeal(data);

        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member == null) continue;
            if (member.CurrentState == PartyMemberScript.MemberState.Dead) continue;

            CharacterStat stat = member.GetComponent<CharacterStat>();
            if (stat == null) continue;

            ApplyHeal(stat, healAmount, data);
            SpawnTargetEffect(data, member.transform);
        }
    }

    private void ApplyHeal(CharacterStat stat, float amount, HealSkillData data)
    {
        if (data.isDotHeal)
            StartCoroutine(DotHealRoutine(stat, amount, data.dotInterval, data.GetDotDuration(skillLevel)));
        else
            HealTarget(stat, amount);
    }

    private IEnumerator DotHealRoutine(CharacterStat stat, float amountPerTick, float interval, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return new WaitForSeconds(interval);
            elapsed += interval;
            if (stat == null) yield break;
            var member = stat.GetComponent<PartyMemberScript>();
            if (member != null && member.CurrentState == PartyMemberScript.MemberState.Dead) yield break;
            HealTarget(stat, amountPerTick);
        }
    }

    private void HealTarget(CharacterStat stat, float amount)
    {
        if (stat == null) return;
        stat.HealHp(amount);
    }

    private float CalculateHeal(HealSkillData data)
    {
        if (myStat == null) return 0f;
        float baseStat  = data.useApRatio ? myStat.TotalAp : myStat.TotalAtk;
        float statBonus = data.GetTotalStatBonus(skillLevel, GetScalingStatValue);
        return (baseStat + statBonus) * data.GetHealMultiplier(skillLevel) * (1f + myStat.HealBonus);
    }

    private void SpawnCasterEffect(HealSkillData data)
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

    private void SpawnTargetEffect(HealSkillData data, Transform target)
    {
        if (string.IsNullOrEmpty(data.targetEffectPoolKey)) return;
        if (ObjectPoolManager.instance == null) return;

        var effect = ObjectPoolManager.instance.GetGo(data.targetEffectPoolKey);
        if (effect != null)
        {
            effect.transform.position = target.position + target.rotation * data.targetEffectSpawnOffset;
            effect.transform.rotation = target.rotation * Quaternion.Euler(data.targetEffectSpawnRotation);
        }
    }
}
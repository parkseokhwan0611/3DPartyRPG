using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

        // 1. 애니메이션
        if (anim != null && !string.IsNullOrEmpty(data.animTriggerName))
        {
            anim.ResetTrigger(data.animTriggerName);
            yield return null;
            yield return null;
            anim.SetTrigger(data.animTriggerName);
        }

        // 2. 이펙트 타이밍 대기
        if (data.effectSpawnDelay > 0f)
            yield return new WaitForSeconds(data.effectSpawnDelay);

        // 3. 시전 이펙트
        SpawnCasterEffect(data);

        // 4. 힐 적용
        if (data.isAoe)
            ApplyAoeHeal(data);
        else
            ApplySingleHeal(data, target);

        // 5. 나머지 애니메이션 대기
        float remaining = data.animDuration - data.effectSpawnDelay;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    // ─────────────────────────────────────────────────────────────────
    // 단일 힐
    // ─────────────────────────────────────────────────────────────────

    private void ApplySingleHeal(HealSkillData data, Transform target)
    {
        // 타겟이 없으면 자신을 힐
        CharacterStat targetStat = target != null
            ? target.GetComponent<CharacterStat>()
            : myStat;

        if (targetStat == null) return;

        float healAmount = CalculateHeal(data);
        ApplyHeal(targetStat, healAmount, data);

        SpawnTargetEffect(data, targetStat.transform);
    }

    // ─────────────────────────────────────────────────────────────────
    // 광역 힐
    // ─────────────────────────────────────────────────────────────────

    private void ApplyAoeHeal(HealSkillData data)
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

    // ─────────────────────────────────────────────────────────────────
    // 힐 적용 (도트힐 / 즉시힐)
    // ─────────────────────────────────────────────────────────────────

    private void ApplyHeal(CharacterStat stat, float amount, HealSkillData data)
    {
        if (data.isDotHeal)
        {
            float duration = data.cooldown.Length > 0 ? data.cooldown[skillLevel - 1] : 5f;
            StartCoroutine(DotHealRoutine(stat, amount, data.dotInterval, duration));
        }
        else
        {
            HealTarget(stat, amount);
        }
    }

    private IEnumerator DotHealRoutine(CharacterStat stat, float amountPerTick, float interval, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return new WaitForSeconds(interval);
            elapsed += interval;

            if (stat == null) yield break;
            HealTarget(stat, amountPerTick);
        }
    }

    // HealSkill.cs - HealTarget() 수정
    private void HealTarget(CharacterStat stat, float amount)
    {
        if (stat == null) return;

        var status = DataManager.instance?.partyStatuses[stat.partyIndex];
        if (status == null) return;

        status.currentHp = Mathf.Clamp(status.currentHp + amount, 0, status.MaxHp);
        status.RaiseHpChanged();
        stat.RaiseHpChanged(); // ← 직접 Invoke 대신 메서드 호출
    }

    // ─────────────────────────────────────────────────────────────────
    // 힐량 계산
    // ─────────────────────────────────────────────────────────────────

    private float CalculateHeal(HealSkillData data)
    {
        if (myStat == null) return 0f;

        float baseStat = data.useApRatio ? myStat.TotalAp : myStat.TotalAtk;
        return baseStat * data.GetHealMultiplier(skillLevel);
    }

    // ─────────────────────────────────────────────────────────────────
    // 이펙트
    // ─────────────────────────────────────────────────────────────────

    private void SpawnCasterEffect(HealSkillData data)
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

    private void SpawnTargetEffect(HealSkillData data, Transform target)
    {
        if (string.IsNullOrEmpty(data.targetEffectPoolKey)) return;
        if (ObjectPoolManager.instance == null) return;

        var effect = ObjectPoolManager.instance.GetGo(data.targetEffectPoolKey);
        if (effect != null)
        {
            effect.transform.position = target.position;
            effect.transform.rotation = target.rotation;
        }
    }
}
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
        SpawnEffect();

        // 4. 버프 적용
        ApplyPartyBuff();


        // 5. 애니메이션 나머지 대기
        float remaining = data.animDuration - data.effectSpawnDelay;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    // ─────────────────────────────────────────────────────────────────
    // 파티 전체 버프 적용
    // ─────────────────────────────────────────────────────────────────

    private void ApplyPartyBuff()
    {
        if (PartyManager.instance == null) return;

        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member == null) continue;
            if (member.CurrentState == PartyMemberScript.MemberState.Dead) continue;

            CharacterStat stat = member.GetComponent<CharacterStat>();
            if (stat != null)
                StartCoroutine(BuffRoutine(stat));
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 버프 지속 코루틴
    // ─────────────────────────────────────────────────────────────────

    private IEnumerator BuffRoutine(CharacterStat stat)
    {
        if (stat == null) yield break;

        float duration   = buffData.GetDuration(skillLevel);
        float atkBonus   = buffData.GetAtkBonus(skillLevel);
        float apBonus    = buffData.GetApBonus(skillLevel);
        float defBonus   = buffData.GetDefBonus(skillLevel);

        // 버프 적용
        ApplyBuff(stat, atkBonus, apBonus, defBonus);

        // 버프 이펙트 (대상 위에 이펙트 표시)
        SpawnTargetEffect(stat.transform);

        // 지속시간 대기
        yield return new WaitForSeconds(duration);

        // 버프 해제
        RemoveBuff(stat, atkBonus, apBonus, defBonus);
    }

    private void ApplyBuff(CharacterStat stat, float atk, float ap, float def)
    {
        // CharacterStatus의 added 수치에 직접 더함
        var status = DataManager.instance?.partyStatuses[stat.partyIndex];
        if (status == null) return;

        status.addedStr += atk;
        status.addedInt += ap;
        status.addedDef += def;
    }

    private void RemoveBuff(CharacterStat stat, float atk, float ap, float def)
    {
        var status = DataManager.instance?.partyStatuses[stat.partyIndex];
        if (status == null) return;

        status.addedStr -= atk;
        status.addedInt -= ap;
        status.addedDef -= def;
    }

    // ─────────────────────────────────────────────────────────────────
    // 이펙트 스폰
    // ─────────────────────────────────────────────────────────────────

    private void SpawnEffect()
    {
        if (string.IsNullOrEmpty(buffData.effectPoolKey)) return;
        if (ObjectPoolManager.instance == null) return;

        var effect = ObjectPoolManager.instance.GetGo(buffData.effectPoolKey);
        if (effect != null)
        {
            effect.transform.position = transform.position;
            effect.transform.rotation = transform.rotation;
        }
    }

    // 버프 대상 위에 이펙트 표시
    private void SpawnTargetEffect(Transform target)
    {
        if (string.IsNullOrEmpty(buffData.effectPoolKey)) return;
        if (ObjectPoolManager.instance == null) return;

        var effect = ObjectPoolManager.instance.GetGo(buffData.effectPoolKey);
        if (effect != null)
        {
            effect.transform.position = target.position;
            effect.transform.rotation = target.rotation;
        }
    }
}
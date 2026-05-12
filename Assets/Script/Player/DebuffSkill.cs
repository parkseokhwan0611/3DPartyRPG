using UnityEngine;
using System.Collections;

public class DebuffSkill : SkillBase
{
    private DebuffSkillData debuffData;

    private DebuffSkillData GetDebuffData()
    {
        if (debuffData == null)
            debuffData = skillData as DebuffSkillData;

        if (debuffData == null)
            Debug.LogError($"[DebuffSkill] {gameObject.name}의 skillData가 DebuffSkillData가 아닙니다!");

        return debuffData;
    }

    protected override IEnumerator ExecuteSkill(Transform target)
    {
        var data = GetDebuffData();
        if (data == null) yield break;
        if (target == null) yield break;

        // 1. 타겟 방향 회전
        Vector3 dir = (target.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        // 2. 애니메이션
        if (anim != null && !string.IsNullOrEmpty(data.animTriggerName))
        {
            anim.ResetTrigger(data.animTriggerName);
            yield return null;
            yield return null;
            anim.SetTrigger(data.animTriggerName);
        }

        // 3. 이펙트 타이밍 대기
        if (data.effectSpawnDelay > 0f)
            yield return new WaitForSeconds(data.effectSpawnDelay);

        // 4. 이펙트 스폰
        SpawnEffect(data, target);

        // 5. 디버프 적용
        if (data.isAoe)
            ApplyAoeDebuff(data);
        else
            ApplySingleDebuff(data, target);

        // 6. 나머지 애니메이션 대기
        float remaining = data.animDuration - data.effectSpawnDelay;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    // ─────────────────────────────────────────────────────────────────
    // 단일 디버프
    // ─────────────────────────────────────────────────────────────────

    private void ApplySingleDebuff(DebuffSkillData data, Transform target)
    {
        StatusEffectHandler handler = target.GetComponent<StatusEffectHandler>();
        if (handler == null) return;

        ApplyDebuffEffects(handler, data);
    }

    // ─────────────────────────────────────────────────────────────────
    // 광역 디버프
    // ─────────────────────────────────────────────────────────────────

    private void ApplyAoeDebuff(DebuffSkillData data)
    {
        AttackBase attackBase  = GetComponent<AttackBase>();
        float range            = 5f; // 기본 광역 범위
        Vector3 center         = transform.position + transform.forward * 2f;

        Collider[] hitCols = Physics.OverlapSphere(center, range, LayerMask.GetMask("Enemy"));

        foreach (Collider col in hitCols)
        {
            StatusEffectHandler handler = col.GetComponent<StatusEffectHandler>();
            if (handler != null)
                ApplyDebuffEffects(handler, data);
        }
    }

    private void ApplyDebuffEffects(StatusEffectHandler handler, DebuffSkillData data)
    {
        foreach (var effect in data.debuffEffects)
        {
            handler.ApplyEffect(new StatusEffect(
                effect.effectType,
                effect.GetValue(skillLevel),
                effect.GetDuration(skillLevel),
                gameObject
            ));
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 이펙트
    // ─────────────────────────────────────────────────────────────────

    private void SpawnEffect(DebuffSkillData data, Transform target)
    {
        if (string.IsNullOrEmpty(data.effectPoolKey)) return;
        if (ObjectPoolManager.instance == null) return;

        var effect = ObjectPoolManager.instance.GetGo(data.effectPoolKey);
        if (effect != null)
        {
            effect.transform.position = target != null ? target.position : transform.position;
            effect.transform.rotation = transform.rotation;
        }
    }
}
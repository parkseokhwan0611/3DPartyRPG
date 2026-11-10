using UnityEngine;
using Effect = PassiveSkillData.PassiveEffectType;

// 발동형 패시브 처리 — CharacterStatus.activeTriggerPassives에 등록된 패시브를
// 평타 적중·데미지 계산·적 처치·힐 시점에 확인해서 발동시킨다.
//
// 확률(procChance)을 굴리는 효과: 평타 공격 속도 증가, 독, 치명타 번개, 쿨 초기화, 보호막
// 확률 없이 항상 적용되는 효과: 디버프 걸린 적 추가 데미지, 처치 시 회복, 힐 치명타(치명타 확률로 판정), 힐 대상 공격 속도 증가
public partial class CharacterStat
{
    private SkillManager _skillManager;
    private SkillManager SkillManagerComp => _skillManager != null ? _skillManager : (_skillManager = GetComponent<SkillManager>());

    // ─────────────────────────────────────────────────────────────────
    // 평타 적중 (MeleeAttack / RangedAttack이 한 번 공격할 때 한 번 호출)
    // ─────────────────────────────────────────────────────────────────

    public void NotifyBasicAttackHit(EnemyHp target, bool isCrit, bool isMagic)
    {
        if (myStatus == null || myStatus.activeTriggerPassives.Count == 0) return;

        foreach (var kvp in myStatus.activeTriggerPassives)
        {
            PassiveSkillData passive = kvp.Key;
            int level = kvp.Value;
            if (passive == null || level <= 0) continue;

            switch (passive.effectType)
            {
                case Effect.OnHitAtkSpeedUp:
                    if (RollProc(passive, level))
                        ApplyProcAtkSpeed(this, passive, level, OnHitAtkSpeedMaxStacks);
                    break;

                case Effect.OnHitPoison:
                    if (IsAlive(target) && RollProc(passive, level))
                        ApplyPoison(target, passive, level, isMagic);
                    break;

                case Effect.OnCritLightning:
                    if (isCrit && IsAlive(target) && RollProc(passive, level))
                        CastProcLightning(target, passive, level);
                    break;

                case Effect.OnHitCooldownReset:
                    if (RollProc(passive, level) && SkillManagerComp != null)
                        SkillManagerComp.ResetCooldowns(0); // 쿨 초기화 스킬은 ResetCooldowns가 알아서 제외
                    break;

                case Effect.OnHitShield:
                    if (RollProc(passive, level))
                        ApplyProcShield(passive, level);
                    break;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 데미지 보정 (EnemyHp가 파티원에게 맞을 때마다 호출 — 평타·스킬·장판·투사체·반사·독 공통)
    // ─────────────────────────────────────────────────────────────────

    public float ModifyOutgoingDamage(EnemyHp target, float damage, bool isMagic)
    {
        if (myStatus == null || target == null || myStatus.activeTriggerPassives.Count == 0) return damage;

        bool targetDebuffed = target.StatusHandler != null && target.StatusHandler.DebuffCount > 0;
        if (!targetDebuffed) return damage;

        float bonus = 0f;
        foreach (var kvp in myStatus.activeTriggerPassives)
        {
            if (kvp.Key != null && kvp.Value > 0 && kvp.Key.effectType == Effect.OnDebuffExtraDamage)
                bonus += kvp.Key.GetValue(kvp.Value); // 0.1 = +10%
        }
        return damage * (1f + bonus);
    }

    // ─────────────────────────────────────────────────────────────────
    // 적 처치 (EnemyHp가 이 파티원의 공격으로 죽었을 때)
    // ─────────────────────────────────────────────────────────────────

    public void NotifyEnemyKilled(EnemyHp enemy)
    {
        if (myStatus == null || myStatus.currentHp <= 0f) return;

        foreach (var kvp in myStatus.activeTriggerPassives)
        {
            if (kvp.Key == null || kvp.Value <= 0 || kvp.Key.effectType != Effect.OnKillHeal) continue;

            // 발동 수치 = 최대 체력 대비 비율 (0.05 = 5%)
            float amount = myStatus.MaxHp * kvp.Key.GetProcValue(kvp.Value);
            if (amount > 0f) HealHp(amount, showAura: false);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 힐 (HealSkill이 대상 하나에 힐을 적용할 때)
    // ─────────────────────────────────────────────────────────────────

    // 힐 치명타 패시브가 있으면 치명타 확률로 굴려서 치명타 데미지 배율만큼 힐량 증가
    public float RollHealCrit(float amount)
    {
        if (myStatus == null || !HasTriggerPassive(Effect.HealCrit)) return amount;
        return Random.value < TotalCritRate ? amount * TotalCritDamage : amount;
    }

    // 힐 받은 대상 공격 속도 증가 패시브
    public void NotifyHealed(CharacterStat target)
    {
        if (myStatus == null || target == null || target.Hp <= 0f) return;

        foreach (var kvp in myStatus.activeTriggerPassives)
        {
            if (kvp.Key != null && kvp.Value > 0 && kvp.Key.effectType == Effect.OnHealAtkSpeedUp)
                ApplyProcAtkSpeed(target, kvp.Key, kvp.Value);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 소환수 (SummonUnit이 공격을 적중시킬 때)
    // ─────────────────────────────────────────────────────────────────

    // 소환수 적중 시 마나 회복 패시브
    public void NotifySummonHit()
    {
        if (myStatus == null || myStatus.currentHp <= 0f || myStatus.summonHitMp <= 0f) return;
        RecoverMp(myStatus.summonHitMp, showAura: false, showText: false);
    }

    // ─────────────────────────────────────────────────────────────────
    // 발동 효과 구현
    // ─────────────────────────────────────────────────────────────────

    private bool HasTriggerPassive(Effect type)
    {
        foreach (var kvp in myStatus.activeTriggerPassives)
            if (kvp.Key != null && kvp.Value > 0 && kvp.Key.effectType == type) return true;
        return false;
    }

    private static bool RollProc(PassiveSkillData passive, int level)
        => Random.value < passive.GetProcChance(level);

    private static bool IsAlive(EnemyHp enemy) => enemy != null && !enemy.isDead;

    // 평타 적중 공격 속도 증가 패시브의 최대 중첩 수
    private const int OnHitAtkSpeedMaxStacks = 3;

    // 공격 속도 증가 (발동 수치 0.2 = 1중첩당 +20%, 발동 지속시간 동안). 같은 패시브로 다시 발동하면 maxStacks까지
    // 한 중첩씩 쌓이고, 지속시간은 전체가 새로 갱신된다 (maxStacks 1 = 누적 없이 갱신만)
    private void ApplyProcAtkSpeed(CharacterStat target, PassiveSkillData passive, int level, int maxStacks = 1)
    {
        float perStack = passive.GetProcValue(level);
        float duration = passive.GetProcDuration(level);
        if (perStack <= 0f || duration <= 0f) return;

        if (!target.TryGetComponent(out PartyStatusEffectHandler handler)) return;

        int stacks  = Mathf.RoundToInt(handler.GetRefreshBuffValue(passive, StatusEffectType.AtkSpeedUp) / perStack);
        float value = perStack * Mathf.Clamp(stacks + 1, 1, Mathf.Max(1, maxStacks));

        handler.ApplyBuff(new StatusEffect(StatusEffectType.AtkSpeedUp, value, duration, gameObject)
        {
            refreshKey = passive,
        });
    }

    // 보호막 — 기본 수치(고정) + 최대 체력 × 발동 수치 (0.05 = 5%), 발동 지속시간 동안. 보호막 스킬과 같은 풀이라
    // 이미 있는 보호막에 합산된다 (만료 시점은 더 늦은 쪽 유지). 계속 때리는 동안 끝없이 쌓이지 않도록
    // 전체 보호막이 1회 발동량의 ProcShieldStackCap배가 될 때까지만 채운다
    private const float ProcShieldStackCap = 3f;

    private void ApplyProcShield(PassiveSkillData passive, int level)
    {
        float amount   = passive.GetValue(level) + myStatus.MaxHp * passive.GetProcValue(level);
        float duration = passive.GetProcDuration(level);
        if (amount <= 0f || duration <= 0f) return;
        if (!TryGetComponent(out PartyStatusEffectHandler handler)) return;

        handler.ApplyShield(amount, duration, gameObject, amount * ProcShieldStackCap);
        SpawnProcEffect(passive, transform);
    }

    // 독 — 1초마다 (평타 공격력 × 발동 수치)의 마법 데미지. 근접은 물리 공격력, 원거리는 마법 공격력 기준
    private void ApplyPoison(EnemyHp target, PassiveSkillData passive, int level, bool isMagic)
    {
        float duration = passive.GetProcDuration(level);
        float tick     = (isMagic ? TotalAp : TotalAtk) * passive.GetProcValue(level);
        if (duration <= 0f || tick <= 0f || target.StatusHandler == null) return;

        target.StatusHandler.ApplyEffect(new StatusEffect(StatusEffectType.Poison, tick, duration, gameObject));
        SpawnProcEffect(passive, target.transform);
    }

    // 치명타 번개 — procSkill이 있으면 그 스킬의 데미지 공식(배운 레벨, 최소 1)으로, 없으면 마법 공격력 × 발동 수치
    private void CastProcLightning(EnemyHp target, PassiveSkillData passive, int level)
    {
        float damage;
        bool  isMagic;
        if (passive.procSkill != null)
        {
            int skillLevel = Mathf.Max(1, GetSkillLevel(passive.procSkill));
            damage  = passive.procSkill.GetRawDamage(skillLevel, this);
            isMagic = passive.procSkill.useAp;
        }
        else
        {
            damage  = TotalAp * passive.GetProcValue(level) * (1f + MagicDmgBonus);
            isMagic = true;
        }
        if (damage <= 0f) return;

        bool isCrit = Random.value < TotalCritRate;
        if (isCrit) damage *= TotalCritDamage;

        SpawnProcEffect(passive, target.transform);
        if (isMagic) target.TakeMagicDamage(damage, gameObject, isCrit);
        else         target.TakeDamage(damage, gameObject, isCrit);
    }

    private static void SpawnProcEffect(PassiveSkillData passive, Transform target)
    {
        if (string.IsNullOrEmpty(passive.procEffectPoolKey) || ObjectPoolManager.instance == null) return;

        GameObject fx = ObjectPoolManager.instance.GetGo(passive.procEffectPoolKey);
        if (fx != null) fx.transform.position = target.position;
    }
}

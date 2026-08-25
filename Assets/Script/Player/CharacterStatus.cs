using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System; // Action을 사용하기 위해 필요

public class CharacterStatus
{
public string charName;
    public float currentHp;
    public float currentMp;

    // 이 캐릭터의 원본 데이터(SO)를 참조로 들고 있게 합니다.
    public ClassData classData;

    public int statPoint = 0;  // 개인 스탯 포인트

    // ── 스탯 포인트 배분으로 올라가는 수치 ──
    public float addedStr = 0;
    public float addedVit = 0;
    public float addedInt = 0;
    public float addedFht = 0;

    // ── 아이템/장비/깡수치 보너스 (스탯과 별개) ──
    public float bonusAtk = 0f;
    public float bonusAp  = 0f;
    public float bonusDef = 0f;

    // ── 장비 장착으로 추가되는 수치 (CharacterEquipment.RecalculateStats가 매번 초기화 후 재계산) ──
    public float equipStr      = 0f;
    public float equipVit      = 0f;
    public float equipInt      = 0f;
    public float equipFht      = 0f;
    public float equipAtk      = 0f;   // 무기 메인: 물리 공격력
    public float equipAp       = 0f;   // 무기 메인: 마법 공격력
    public float equipMaxHp    = 0f;   // 방어구 메인: 최대 체력 고정 보너스
    public float equipDef      = 0f;   // 방어구 메인/서브: 방어력
    public float equipMagicRes = 0f;   // 방어구 메인/서브: 마법 저항력
    public float equipCritRate = 0f;
    public float equipCritDmg  = 0f;
    public float equipCDReduce = 0f;   // 스킬 쿨타임 감소 (0.1 = 10%)
    public float equipMpReduce = 0f;   // 마나 소모 감소  (0.1 = 10%)
    public float equipPhysDmg  = 0f;   // 물리 피해 증가  (0.1 = 10%)
    public float equipMagicDmg = 0f;   // 마법 피해 증가  (0.1 = 10%)

    // ── 아이템/패시브로 추가되는 재생량 ──
    public float addedHpRegen = 0f;
    public float addedMpRegen = 0f;

    public int skillPoint = 0;

    public float addedMp = 0f;

    // 최대 체력/방어력 % 패시브 보너스 (0.1 = +10%) — VIT을 직접 건드리지 않고 최종 계산식에만 곱연산으로 반영
    public float maxHpPercentBonus = 0f;
    public float defPercentBonus   = 0f;

    public float MaxHp => (classData.hp
                        + ((classData.baseVit + addedVit + equipVit) * classData.hpPerVit)
                        + equipMaxHp) * (1f + maxHpPercentBonus);
    public float MaxMp => classData.mp + addedMp;

    public float TotalHpRegen => classData.baseHpRegen
                               + (classData.baseVit + addedVit + equipVit) * classData.hpRegenPerVit
                               + addedHpRegen;
    public float TotalMpRegen => classData.baseMpRegen
                               + (classData.baseFht + addedFht + equipFht) * classData.mpRegenPerFth
                               + addedMpRegen;
    // 공격력/방어력 감소 디버프 배율 (스킬로 조정, 1.0 = 기본) — Slow의 moveSpeedMultiplier와 동일한 패턴
    public float atkDebuffMultiplier = 1f;
    public float defDebuffMultiplier = 1f;

    public float TotalAtk => ((classData.baseStr + addedStr + equipStr) * classData.atkPerStr
                           + bonusAtk + equipAtk) * atkDebuffMultiplier;
    public float TotalAp  => ((classData.baseInt + addedInt + equipInt) * classData.apPerInt)
                           + ((classData.baseFht + addedFht + equipFht) * classData.apPerFth)
                           + bonusAp + equipAp;

    // ── 아이템/패시브로 쌓이는 추가 수치 ──
    public float addedCritRate   = 0f;
    public float addedCritDamage = 0f;

    public float TotalCritRate   => classData.baseCritRate   + addedCritRate  + equipCritRate;
    public float TotalCritDamage => classData.baseCritDamage + addedCritDamage + equipCritDmg;

    // 방어력 (VIT 비례 + 패시브 + 장비)
    public float addedDef = 0f;
    public float TotalDef => (((classData.baseVit + addedVit + equipVit) * classData.defPerVit)
                           + addedDef + bonusDef + equipDef) * (1f + defPercentBonus) * defDebuffMultiplier;

    // 마법 저항력
    public float addedMagicRes = 0f;
    public float TotalMagicRes => classData.baseMagicRes + addedMagicRes + equipMagicRes;

    // 피해 증가 합산 (패시브 + 장비)
    public float TotalPhysDmgBonus  => physDmgBonus  + equipPhysDmg;
    public float TotalMagicDmgBonus => magicDmgBonus + equipMagicDmg;

    // 스킬 쿨타임·마나 소모 감소 (현재는 장비만, 추후 패시브 확장 가능)
    public float TotalCDReduce => equipCDReduce;
    public float TotalMpReduce => equipMpReduce;

    // 이동속도
    public float moveSpeedMultiplier = 1f; // 버프/디버프로 조정 (1.0 = 기본)
    public float TotalMoveSpeed => classData.baseMoveSpeed * moveSpeedMultiplier;

    // 기본 공격 적중 시 체력/마나 회복
    public float hpOnHit = 0f;
    public float mpOnHit = 0f;

    // 최종 피해/힐 배율 보너스 — 패시브 스킬 전용 (0.1 = +10%)
    public float physDmgBonus  = 0f;
    public float magicDmgBonus = 0f;
    public float healBonus     = 0f;

    // 키: SkillData, 값: 현재 스킬 레벨
    public Dictionary<SkillData, int> skillLevels = new Dictionary<SkillData, int>();

    // 이벤트를 데이터 클래스에 넣으면 UI 업데이트가 더 쉬워집니다.
    public event Action OnHpChanged;
    public event Action OnMpChanged;
    public Dictionary<PassiveSkillData, int> activeTriggerPassives
        = new Dictionary<PassiveSkillData, int>();

    // 스킬 연계 버프
    public float nextSkillDamageBonus = 0f; // 다음 스킬 데미지 증가량
    public float nextSkillBonusTimer  = 0f; // 버프 지속시간

    // 부활 패시브 쿨타임 (초)
    public float reviveCooldownTimer = 0f;
    
    public void RaiseHpChanged() => OnHpChanged?.Invoke();
    public void RaiseMpChanged() => OnMpChanged?.Invoke();

    // MP 회복 (자연 회복이나 힐러 스킬용)
    public void RecoverMp(float amount)
    {
        currentMp = Mathf.Clamp(currentMp + amount, 0, MaxMp);
        RaiseMpChanged();
    }

    // 스킬 레벨 가져오기 (없으면 0)
    public int GetSkillLevel(SkillData skill)
    {
        return skillLevels.ContainsKey(skill) ? skillLevels[skill] : 0;
    }

    public bool TryLevelUpSkill(SkillData skill)
    {
        int currentLevel = GetSkillLevel(skill);

        if (currentLevel >= skill.maxLevel) return false;
        if (skill.skillPointCost == null || skill.skillPointCost.Length == 0) return false;

        // maxLevel과 skillPointCost 배열 길이가 어긋나 있어도 크래시 없이 마지막 유효값으로 대체
        int costIdx = Mathf.Clamp(currentLevel, 0, skill.skillPointCost.Length - 1);
        int cost    = skill.skillPointCost[costIdx];
        if (skillPoint < cost) return false;

        skillPoint -= cost;
        skillLevels[skill] = currentLevel + 1;

        if (skill is PassiveSkillData passive)
            ApplyPassive(passive, currentLevel, currentLevel + 1);

        AudioManager.instance?.PlaySFX("SkillLevelUp");

        return true;
    }

    // LevelUpSkill (포인트 없이 강제 레벨업 — 디버그/이벤트용)
    public bool LevelUpSkill(SkillData skill)
    {
        int currentLevel = GetSkillLevel(skill);
        if (currentLevel >= skill.maxLevel) return false;

        skillLevels[skill] = currentLevel + 1;

        if (skill is PassiveSkillData passive)
            ApplyPassive(passive, currentLevel, currentLevel + 1);

        return true;
    }

    // oldLevel: 적용 전 레벨 (0 = 미습득), newLevel: 적용 후 레벨
    // 델타만 더해서 레벨업할수록 중복 누적되지 않음
    private void ApplyPassive(PassiveSkillData passive, int oldLevel, int newLevel)
    {
        float oldValue = oldLevel > 0 ? passive.GetValue(oldLevel) : 0f;
        float delta    = passive.GetValue(newLevel) - oldValue;

        switch (passive.effectType)
        {
            case PassiveSkillData.PassiveEffectType.PhysDmgBonus:
                physDmgBonus += delta;
                break;
            case PassiveSkillData.PassiveEffectType.MagicDmgBonus:
                magicDmgBonus += delta;
                break;
            case PassiveSkillData.PassiveEffectType.HealPercent:
                healBonus += delta;
                break;
            case PassiveSkillData.PassiveEffectType.CritRate:
                addedCritRate += delta;
                break;
            case PassiveSkillData.PassiveEffectType.CritDamage:
                addedCritDamage += delta;
                break;
            case PassiveSkillData.PassiveEffectType.MagicResPercent:
                addedMagicRes += classData.baseMagicRes * delta;
                break;
            case PassiveSkillData.PassiveEffectType.DefPercent:
                defPercentBonus += delta;
                break;
            case PassiveSkillData.PassiveEffectType.MaxHpPercent:
                maxHpPercentBonus += delta;
                break;

            case PassiveSkillData.PassiveEffectType.MaxMpBonus:
                addedMp += delta;
                break;

            case PassiveSkillData.PassiveEffectType.OnHitManaRestore:
                mpOnHit += delta;
                break;

            // 트리거 패시브 — 수치 없이 등록만
            case PassiveSkillData.PassiveEffectType.OnCritLightning:
            case PassiveSkillData.PassiveEffectType.OnHitPoison:
            case PassiveSkillData.PassiveEffectType.OnKillHeal:
                RegisterTriggerPassive(passive, newLevel);
                break;
        }
    }

    // 트리거 패시브 등록 (공격 스크립트에서 체크할 수 있도록)
    private void RegisterTriggerPassive(PassiveSkillData passive, int level)
    {
        // 활성화된 트리거 패시브 목록에 추가
        if (!activeTriggerPassives.ContainsKey(passive))
            activeTriggerPassives[passive] = level;
    }
}

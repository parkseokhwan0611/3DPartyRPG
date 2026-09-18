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

    // ── 스킬(버프/패시브)로 오른 능력치 — 세이브 대상 아님 ──
    // 스탯 포인트 필드(addedStr 등)와 분리해서, 버프가 걸린 채로 저장해도 세이브에 섞여 들어가지 않게 한다.
    // 패시브는 로드 시 LevelUpSkill로 재적용되고, 버프는 지속시간이 끝나면 사라진다.
    private static readonly int ModifierStatCount = Enum.GetValues(typeof(ModifierStat)).Length;
    private readonly float[] skillFlat    = new float[ModifierStatCount];
    private readonly float[] skillPercent = new float[ModifierStatCount]; // 0.1 = +10%, 겹치면 합산

    // value가 음수면 해제 (버프 만료 시 -value로 호출)
    public void AddSkillModifier(ModifierStat stat, ModifierMode mode, float value)
    {
        if (mode == ModifierMode.Percent) skillPercent[(int)stat] += value;
        else                              skillFlat[(int)stat]    += value;
    }

    // 퍼센트 증가는 기본 수치(스탯 + 장비)에만 곱하고, 고정 증가는 그 뒤에 더한다
    private float ApplySkillModifiers(ModifierStat stat, float baseValue)
        => baseValue * (1f + skillPercent[(int)stat]) + skillFlat[(int)stat];

    // 기본 수치 = 스탯 + 장비. 스킬 퍼센트 증가의 기준이자 스탯창의 "(+N)" 표기 기준
    public float BaseMaxHp => classData.hp
                            + ((classData.baseVit + addedVit + equipVit) * classData.hpPerVit)
                            + equipMaxHp;
    // 신앙 비례 체력(FaithToHp 패시브)은 스킬 증가분처럼 기본 수치 뒤에 더한다 — 퍼센트 버프에 곱해지지 않음
    public float MaxHp => ApplySkillModifiers(ModifierStat.MaxHp, BaseMaxHp)
                        + (classData.baseFht + addedFht + equipFht) * faithToHpCoeff;
    // 최대 마나는 퍼센트 증가 없이 고정 증가(addedMp)만 지원
    public float MaxMp => classData.mp + addedMp;

    public float TotalHpRegen => classData.baseHpRegen
                               + (classData.baseVit + addedVit + equipVit) * classData.hpRegenPerVit
                               + addedHpRegen + buffHpRegen;
    public float TotalMpRegen => classData.baseMpRegen
                               + (classData.baseFht + addedFht + equipFht) * classData.mpRegenPerFth
                               + addedMpRegen + buffMpRegen;

    // ── 버프·패시브로 붙는 수치 — 세이브 대상 아님 ──
    public float buffHpRegen    = 0f;  // 체력 재생 버프 (초당)
    public float buffMpRegen    = 0f;  // 마나 재생 버프 (초당)
    public float atkSpeedBonus  = 0f;  // 공격속도 증가 (0.1 = +10%, 패시브·버프·발동 효과 합산)
    public float faithToHpCoeff = 0f;  // 신앙 1당 최대 체력
    // 공격력/방어력 감소 디버프 배율 (스킬로 조정, 1.0 = 기본) — Slow의 moveSpeedMultiplier와 동일한 패턴
    public float atkDebuffMultiplier = 1f;
    public float defDebuffMultiplier = 1f;

    public float BaseAtk => (classData.baseStr + addedStr + equipStr) * classData.atkPerStr
                          + bonusAtk + equipAtk;
    // 감소 디버프는 스킬 증가분까지 반영된 최종 수치에 곱한다
    public float TotalAtk => ApplySkillModifiers(ModifierStat.Atk, BaseAtk) * atkDebuffMultiplier;

    public float BaseAp => ((classData.baseInt + addedInt + equipInt) * classData.apPerInt)
                         + ((classData.baseFht + addedFht + equipFht) * classData.apPerFth)
                         + bonusAp + equipAp;
    public float TotalAp => ApplySkillModifiers(ModifierStat.Ap, BaseAp);

    // ── 아이템/패시브로 쌓이는 추가 수치 ──
    public float addedCritRate   = 0f;
    public float addedCritDamage = 0f;

    public float TotalCritRate   => classData.baseCritRate   + addedCritRate  + equipCritRate;
    public float TotalCritDamage => classData.baseCritDamage + addedCritDamage + equipCritDmg;

    // 방어력 (VIT 비례 + 장비 → 스킬 증가 → 감소 디버프)
    public float BaseDef => ((classData.baseVit + addedVit + equipVit) * classData.defPerVit)
                          + bonusDef + equipDef;
    public float TotalDef => ApplySkillModifiers(ModifierStat.Def, BaseDef) * defDebuffMultiplier;

    // 마법 저항력
    public float BaseMagicRes  => classData.baseMagicRes + equipMagicRes;
    public float TotalMagicRes => ApplySkillModifiers(ModifierStat.MagicRes, BaseMagicRes);

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

    // 받는 데미지 감소 — 패시브 전용, 세이브 대상 아님 (로드 시 패시브 재적용으로 복원)
    // 퍼센트(0.1 = 10%)는 합산, 고정은 방어력·퍼센트 감소 뒤에 한 대마다 뺀다
    public float physDmgReductionPct  = 0f;
    public float magicDmgReductionPct = 0f;
    public float physDmgReductionFlat  = 0f;
    public float magicDmgReductionFlat = 0f;
    // 받는 데미지 감소 버프 — 물리·마법 공통, 패시브 퍼센트와 합산
    public float buffDmgReductionPct   = 0f;

    // 방어력/마법 저항력 경감이 끝난 데미지에 받는 데미지 감소를 적용
    // 퍼센트 합이 100%를 넘어도 음수가 되지 않게 막고, 고정 감소로는 원래 데미지가 있던 공격을
    // 완전히 무효화하지 못하게 최소 1은 들어가도록 한다
    public float ApplyDamageReduction(float damage, bool isMagic)
    {
        if (damage <= 0f) return damage;

        float pct  = (isMagic ? magicDmgReductionPct : physDmgReductionPct) + buffDmgReductionPct;
        float flat = isMagic ? magicDmgReductionFlat : physDmgReductionFlat;

        float reduced = damage * (1f - Mathf.Clamp01(pct)) - flat;
        return Mathf.Max(1f, reduced);
    }

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
        ApplyPassiveEffect(passive, passive.effectType, passive.valueMode,
                           passive.GetValue(newLevel) - oldValue, newLevel);

        // 두 번째 효과 (예: 방어력 + 마법 저항력) — 첫 번째와 같은 방식으로 레벨 차이만큼 더한다
        if (passive.HasValidSecondEffect)
        {
            float oldSecond = oldLevel > 0 ? passive.GetSecondValue(oldLevel) : 0f;
            ApplyPassiveEffect(passive, passive.secondEffectType, passive.secondValueMode,
                               passive.GetSecondValue(newLevel) - oldSecond, newLevel);
        }
    }

    private void ApplyPassiveEffect(PassiveSkillData passive, PassiveSkillData.PassiveEffectType type,
                                    ModifierMode mode, float delta, int newLevel)
    {
        // 공격력/마법 공격력/방어력/마법 저항력/최대 체력 — 고정/퍼센트 증가 공통 처리.
        // 로드 시에도 이 경로로 재적용되므로 현재 체력은 건드리지 않는다 (세이브의 currentHp가 이미 최종값)
        if (PassiveSkillData.TryGetModifierStat(type, out ModifierStat modifierStat))
        {
            AddSkillModifier(modifierStat, mode, delta);
            return;
        }

        switch (type)
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
            case PassiveSkillData.PassiveEffectType.MaxMpBonus:
                addedMp += delta;
                break;

            case PassiveSkillData.PassiveEffectType.OnHitManaRestore:
                mpOnHit += delta;
                break;

            case PassiveSkillData.PassiveEffectType.PhysDmgReduction:
                if (mode == ModifierMode.Percent) physDmgReductionPct  += delta;
                else                              physDmgReductionFlat += delta;
                break;
            case PassiveSkillData.PassiveEffectType.MagicDmgReduction:
                if (mode == ModifierMode.Percent) magicDmgReductionPct  += delta;
                else                              magicDmgReductionFlat += delta;
                break;

            case PassiveSkillData.PassiveEffectType.AtkSpeed:
                atkSpeedBonus += delta;
                break;
            case PassiveSkillData.PassiveEffectType.FaithToHp:
                faithToHpCoeff += delta;
                break;

            // 발동형 패시브 — 등록만 해두고 실제 발동은 CharacterStat(PassiveTriggers)이 공격·힐·처치 시점에 확인
            default:
                if (PassiveSkillData.IsTriggerEffect(type))
                    activeTriggerPassives[passive] = newLevel; // 레벨업 시 레벨도 갱신
                break;
        }
    }
}

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
    public float addedStr = 0;
    public float addedVit = 0;
    public float addedInt = 0;
    public float addedFht = 0;

    public float MaxHp   => classData.hp + ((classData.baseVit + addedVit) * classData.hpPerVit);
    public float MaxMp => classData.mp; // 필요시 공식 추가
    public float TotalAtk => (classData.baseStr + addedStr) * classData.atkPerStr;
    public float TotalAp  => ((classData.baseInt + addedInt) * classData.apPerInt)
                        + ((classData.baseFht + addedFht) * classData.apPerFth);

    // 패시브/아이템으로 누적되는 추가 수치
    public float addedCritRate   = 0f;
    public float addedCritDamage = 0f;

    // 최종 치명타 수치
    public float TotalCritRate   => classData.baseCritRate + addedCritRate;
    public float TotalCritDamage => classData.baseCritDamage + addedCritDamage;
    //방어력
    public float addedDef = 0f; // 아이템으로 추가되는 방어력

    public float TotalDef => ((classData.baseVit + addedVit) * classData.defPerVit) + addedDef;

    // 키: SkillData, 값: 현재 스킬 레벨
    public Dictionary<SkillData, int> skillLevels = new Dictionary<SkillData, int>();

    // 이벤트를 데이터 클래스에 넣으면 UI 업데이트가 더 쉬워집니다.
    public event Action OnHpChanged;
    public Dictionary<PassiveSkillData, int> activeTriggerPassives 
        = new Dictionary<PassiveSkillData, int>();
    public void RaiseHpChanged()
    {
        OnHpChanged?.Invoke();
    }
    // MP 회복 (자연 회복이나 힐러 스킬용)
    public void RecoverMp(float amount)
    {
        currentMp = Mathf.Clamp(currentMp + amount, 0, MaxMp);
    }

    // 스킬 레벨 가져오기 (없으면 0)
    public int GetSkillLevel(SkillData skill)
    {
        return skillLevels.ContainsKey(skill) ? skillLevels[skill] : 0;
    }

    // 스킬 레벨업
    public bool LevelUpSkill(SkillData skill)
    {
        int currentLevel = GetSkillLevel(skill);
        if (currentLevel >= skill.maxLevel) return false;

        skillLevels[skill] = currentLevel + 1;

        // 패시브 스킬이면 즉시 스탯 적용
        if (skill is PassiveSkillData passive)
            ApplyPassive(passive, currentLevel + 1);

        return true;
    }

    private void ApplyPassive(PassiveSkillData passive, int level)
    {
        switch (passive.effectType)
        {
            // 단순 수치 증가
            case PassiveSkillData.PassiveEffectType.AtkPercent:
                addedStr += classData.baseStr * passive.GetValue(level);
                break;
            case PassiveSkillData.PassiveEffectType.ApPercent:
                addedInt += classData.baseInt * passive.GetValue(level);
                break;
            case PassiveSkillData.PassiveEffectType.DefPercent:
                addedDef += TotalDef * passive.GetValue(level);
                break;
            case PassiveSkillData.PassiveEffectType.CritRate:
                addedCritRate += passive.GetValue(level);
                break;
            case PassiveSkillData.PassiveEffectType.CritDamage:
                addedCritDamage += passive.GetValue(level);
                break;
            case PassiveSkillData.PassiveEffectType.MaxHpPercent:
                addedVit += classData.baseVit * passive.GetValue(level);
                break;

            // 특수 효과는 수치 적용 없이 등록만 함
            // 실제 발동은 공격 스크립트에서 처리
            case PassiveSkillData.PassiveEffectType.OnCritLightning:
            case PassiveSkillData.PassiveEffectType.OnHitPoison:
            case PassiveSkillData.PassiveEffectType.OnKillHeal:
                RegisterTriggerPassive(passive, level);
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

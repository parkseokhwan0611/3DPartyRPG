using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillDetailPanelUI : MonoBehaviour
{
    [Header("# 스킬 포인트")]
    public TextMeshProUGUI skillPointText;

    [Header("# 현재 스킬 정보")]
    public TextMeshProUGUI skillNameText;
    public TextMeshProUGUI skillLvText;
    public TextMeshProUGUI skillDescText;
    public TextMeshProUGUI mpCostText;
    public TextMeshProUGUI cooldownText;
    public TextMeshProUGUI damageText;
    public TextMeshProUGUI specialText;

    [Header("# 다음 레벨 정보")]
    public TextMeshProUGUI nextSkillLvText;
    public TextMeshProUGUI levelConditionText;
    public TextMeshProUGUI requiredSkillPointText;
    public TextMeshProUGUI nextMpCostText;
    public TextMeshProUGUI nextCooldownText;
    public TextMeshProUGUI nextDamageText;
    public TextMeshProUGUI nextSpecialText;

    [Header("# 버튼")]
    public Button learnButton;

    private SkillData currentSkill;
    private int currentCharIndex;
    private int currentRequiredLevel;
    private SkillWindowUI skillWindow;
    private CharacterStat currentCaster;

    void Start()
    {
        skillWindow = GetComponentInParent<SkillWindowUI>();
        if (learnButton != null)
            learnButton.onClick.AddListener(OnLearnButtonClicked);
        Clear();
    }

    public void RefreshSkillPoint(int point)
    {
        if (skillPointText != null)
            skillPointText.text = $"Skill Point: {point}";
    }

    public void ShowSkillDetail(SkillData skill, CharacterStatus status, int partyLevel, int charIndex, int requiredLevel)
    {
        currentSkill         = skill;
        currentCharIndex     = charIndex;
        currentRequiredLevel = requiredLevel;
        currentCaster        = FindCasterStat(charIndex);

        int currentLevel = status.GetSkillLevel(skill);
        int nextLevel    = currentLevel + 1;
        bool isMaxLevel  = currentLevel >= skill.maxLevel;
        bool isPassive   = skill.skillType == SkillData.SkillType.Passive;

        SetTextSafe(skillNameText, skill.skillName);
        SetTextSafe(skillLvText,   currentLevel > 0 ? $"LV {currentLevel}" : "미습득");
        SetTextSafe(skillDescText, skill.description);

        // ── 현재 레벨 수치 ──
        if (currentLevel > 0)
        {
            int idx = currentLevel - 1;

            if (!isPassive && idx < skill.mpCost.Length)
                SetTextSafe(mpCostText, $"마나 소모량: {skill.mpCost[idx]}");
            else
                SetTextSafe(mpCostText, "");

            if (!isPassive && idx < skill.cooldown.Length)
                SetTextSafe(cooldownText, $"쿨타임: {skill.cooldown[idx]}초");
            else
                SetTextSafe(cooldownText, "");

            ShowCurrentStatByType(skill, currentLevel);
        }
        else
        {
            SetTextSafe(mpCostText,   "");
            SetTextSafe(cooldownText, "");
            SetTextSafe(damageText,   "");
            SetTextSafe(specialText,  "");
        }

        // ── 다음 레벨 수치 ──
        if (!isMaxLevel)
        {
            int nextIdx = nextLevel - 1;

            SetTextSafe(nextSkillLvText, $"LV {nextLevel}");

            if (!isPassive && nextIdx < skill.mpCost.Length)
                SetTextSafe(nextMpCostText, $"마나 소모량: {skill.mpCost[nextIdx]}");
            else
                SetTextSafe(nextMpCostText, "");

            if (!isPassive && nextIdx < skill.cooldown.Length)
                SetTextSafe(nextCooldownText, $"쿨타임: {skill.cooldown[nextIdx]}초");
            else
                SetTextSafe(nextCooldownText, "");

            ShowNextStatByType(skill, nextLevel);

            if (nextIdx < skill.skillPointCost.Length)
            {
                int cost = skill.skillPointCost[nextIdx];
                SetTextSafe(requiredSkillPointText, $"필요 스킬 포인트: {cost}");
                SetTextSafe(levelConditionText, $"필요 레벨: {requiredLevel}");
                if (learnButton != null)
                    learnButton.interactable = status.skillPoint >= cost && partyLevel >= requiredLevel;
            }
        }
        else
        {
            SetTextSafe(nextSkillLvText,        "최대 레벨");
            SetTextSafe(nextMpCostText,         "");
            SetTextSafe(nextCooldownText,       "");
            SetTextSafe(nextDamageText,         "");
            SetTextSafe(nextSpecialText,        "");
            SetTextSafe(requiredSkillPointText, "");
            SetTextSafe(levelConditionText,     "");
            if (learnButton != null) learnButton.interactable = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 타입별 표시
    // ─────────────────────────────────────────────────────────────────

    private void ShowCurrentStatByType(SkillData skill, int level)
    {
        if (skill is DamageSkillData dmgSkill)
        {
            SetTextSafe(damageText,  SkillDescriptionBuilder.BuildDamageDescription(dmgSkill, level, currentCaster));
            SetTextSafe(specialText, SkillDescriptionBuilder.GetDamageSkillSpecial(dmgSkill, level));
        }
        else if (skill is HealSkillData healSkill)
        {
            SetTextSafe(damageText,  "");
            SetTextSafe(specialText, SkillDescriptionBuilder.BuildHealDescription(healSkill, level, currentCaster));
        }
        else if (skill is BuffSkillData buffSkill)
        {
            SetTextSafe(damageText,  "");
            SetTextSafe(specialText, SkillDescriptionBuilder.GetBuffDescription(buffSkill, level, currentCaster));
        }
        else if (skill is DebuffSkillData debuffSkill)
        {
            SetTextSafe(damageText,  "");
            SetTextSafe(specialText, SkillDescriptionBuilder.GetDebuffDescription(debuffSkill, level));
        }
        else if (skill is PassiveSkillData passiveSkill)
        {
            SetTextSafe(damageText,  "");
            SetTextSafe(specialText, SkillDescriptionBuilder.GetPassiveDescription(passiveSkill, level));
        }
        else
        {
            SetTextSafe(damageText,  "");
            SetTextSafe(specialText, "");
        }
    }

    private void ShowNextStatByType(SkillData skill, int nextLevel)
    {
        if (skill is DamageSkillData dmgSkill)
        {
            SetTextSafe(nextDamageText,  SkillDescriptionBuilder.BuildDamageDescription(dmgSkill, nextLevel, currentCaster));
            SetTextSafe(nextSpecialText, SkillDescriptionBuilder.GetDamageSkillSpecial(dmgSkill, nextLevel));
        }
        else if (skill is HealSkillData healSkill)
        {
            SetTextSafe(nextDamageText,  "");
            SetTextSafe(nextSpecialText, SkillDescriptionBuilder.BuildHealDescription(healSkill, nextLevel, currentCaster));
        }
        else if (skill is BuffSkillData buffSkill)
        {
            SetTextSafe(nextDamageText,  "");
            SetTextSafe(nextSpecialText, SkillDescriptionBuilder.GetBuffDescription(buffSkill, nextLevel, currentCaster));
        }
        else if (skill is DebuffSkillData debuffSkill)
        {
            SetTextSafe(nextDamageText,  "");
            SetTextSafe(nextSpecialText, SkillDescriptionBuilder.GetDebuffDescription(debuffSkill, nextLevel));
        }
        else if (skill is PassiveSkillData passiveSkill)
        {
            SetTextSafe(nextDamageText,  "");
            SetTextSafe(nextSpecialText, SkillDescriptionBuilder.GetPassiveDescription(passiveSkill, nextLevel));
        }
        else
        {
            SetTextSafe(nextDamageText,  "");
            SetTextSafe(nextSpecialText, "");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────────────────────────

    private CharacterStat FindCasterStat(int partyIndex)
    {
        if (PartyManager.instance == null) return null;
        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member == null) continue;
            CharacterStat stat = member.GetComponent<CharacterStat>();
            if (stat != null && stat.partyIndex == partyIndex)
                return stat;
        }
        return null;
    }

    private void OnLearnButtonClicked()
    {
        if (currentSkill == null || DataManager.instance == null) return;
        if (DataManager.instance.partyLevel < currentRequiredLevel) return;

        CharacterStatus status = DataManager.instance.partyStatuses[currentCharIndex];
        bool success           = status.TryLevelUpSkill(currentSkill);

        if (!success) return;

        skillWindow?.OnSkillLevelUp();
        ShowSkillDetail(currentSkill, status, DataManager.instance.partyLevel, currentCharIndex, currentRequiredLevel);
    }

    private void SetTextSafe(TextMeshProUGUI tmp, string text)
    {
        if (tmp != null) tmp.text = text;
    }

    public void Clear()
    {
        currentSkill         = null;
        currentRequiredLevel = 0;

        SetTextSafe(skillNameText,          "");
        SetTextSafe(skillLvText,            "");
        SetTextSafe(skillDescText,          "");
        SetTextSafe(mpCostText,             "");
        SetTextSafe(cooldownText,           "");
        SetTextSafe(damageText,             "");
        SetTextSafe(specialText,            "");
        SetTextSafe(nextSkillLvText,        "");
        SetTextSafe(levelConditionText,     "");
        SetTextSafe(requiredSkillPointText, "");
        SetTextSafe(nextMpCostText,         "");
        SetTextSafe(nextCooldownText,       "");
        SetTextSafe(nextDamageText,         "");
        SetTextSafe(nextSpecialText,        "");

        if (learnButton != null) learnButton.interactable = false;
    }
}

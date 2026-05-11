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
    public TextMeshProUGUI damageText;    // 데미지 스킬 전용
    public TextMeshProUGUI specialText;   // 버프/패시브 전용

    [Header("# 다음 레벨 정보")]
    public TextMeshProUGUI nextSkillLvText;
    public TextMeshProUGUI levelConditionText;
    public TextMeshProUGUI requiredSkillPointText;
    public TextMeshProUGUI nextMpCostText;
    public TextMeshProUGUI nextCooldownText;
    public TextMeshProUGUI nextDamageText;   // 데미지 스킬 전용
    public TextMeshProUGUI nextSpecialText;  // 버프/패시브 전용

    [Header("# 버튼")]
    public Button learnButton;

    private SkillData currentSkill;
    private int currentCharIndex;
    private SkillWindowUI skillWindow;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Start()
    {
        skillWindow = GetComponentInParent<SkillWindowUI>();

        if (learnButton != null)
            learnButton.onClick.AddListener(OnLearnButtonClicked);

        Clear();
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 포인트 갱신
    // ─────────────────────────────────────────────────────────────────

    public void RefreshSkillPoint(int point)
    {
        if (skillPointText != null)
            skillPointText.text = $"Skill Point: {point}";
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 상세 표시
    // ─────────────────────────────────────────────────────────────────

    public void ShowSkillDetail(SkillData skill, CharacterStatus status, int partyLevel, int charIndex)
    {
        currentSkill     = skill;
        currentCharIndex = charIndex;

        int currentLevel = status.GetSkillLevel(skill);
        int nextLevel    = currentLevel + 1;
        bool isMaxLevel  = currentLevel >= skill.maxLevel;

        // ── 스킬 이름 / 레벨 / 설명 ──
        if (skillNameText != null)
            skillNameText.text = skill.skillName;

        if (skillLvText != null)
            skillLvText.text = currentLevel > 0 ? $"LV {currentLevel}" : "미습득";

        if (skillDescText != null)
            skillDescText.text = skill.description;

        // ── 현재 레벨 수치 ──
        if (currentLevel > 0)
        {
            int idx = currentLevel - 1;

            SetTextSafe(mpCostText,   idx < skill.mpCost.Length
                ? $"마나 소모량: {skill.mpCost[idx]}" : "");

            SetTextSafe(cooldownText, idx < skill.cooldown.Length
                ? $"쿨타임: {skill.cooldown[idx]}초" : "");

            // 스킬 타입에 따라 계수 표시 분리
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

            if (nextSkillLvText != null)
                nextSkillLvText.text = $"LV {nextLevel}";

            SetTextSafe(nextMpCostText,   nextIdx < skill.mpCost.Length
                ? $"마나 소모량: {skill.mpCost[nextIdx]}" : "");

            SetTextSafe(nextCooldownText, nextIdx < skill.cooldown.Length
                ? $"쿨타임: {skill.cooldown[nextIdx]}초" : "");

            // 스킬 타입에 따라 다음 레벨 계수 표시
            ShowNextStatByType(skill, nextLevel);

            // 필요 스킬 포인트
            if (nextIdx < skill.skillPointCost.Length)
            {
                int cost = skill.skillPointCost[nextIdx];

                SetTextSafe(requiredSkillPointText, $"필요 스킬 포인트: {cost}");
                SetTextSafe(levelConditionText,
                    $"스킬 포인트 {status.skillPoint} 보유");

                if (learnButton != null)
                    learnButton.interactable = status.skillPoint >= cost;
            }
        }
        else
        {
            // 최대 레벨
            if (nextSkillLvText != null) nextSkillLvText.text = "최대 레벨";
            SetTextSafe(nextMpCostText,        "");
            SetTextSafe(nextCooldownText,      "");
            SetTextSafe(nextDamageText,        "");
            SetTextSafe(nextSpecialText,       "");
            SetTextSafe(requiredSkillPointText,"");
            SetTextSafe(levelConditionText,    "");
            if (learnButton != null) learnButton.interactable = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 타입별 현재 레벨 계수 표시
    // ─────────────────────────────────────────────────────────────────

    private void ShowCurrentStatByType(SkillData skill, int level)
    {
        // 데미지 스킬
        if (skill is DamageSkillData dmgSkill)
        {
            SetTextSafe(damageText,  $"데미지: {dmgSkill.GetDamageMultiplier(level) * 100f:F1}%");
            SetTextSafe(specialText, ""); // 패시브/버프 텍스트 비우기
        }
        // 버프 스킬 — 데미지 없음
        else if (skill is BuffSkillData buffSkill)
        {
            SetTextSafe(damageText,  "");
            string buffInfo = "";
            if (buffSkill.GetAtkBonus(level) > 0)  buffInfo += $"공격력 +{buffSkill.GetAtkBonus(level)}\n";
            if (buffSkill.GetApBonus(level) > 0)   buffInfo += $"주문력 +{buffSkill.GetApBonus(level)}\n";
            if (buffSkill.GetDefBonus(level) > 0)  buffInfo += $"방어력 +{buffSkill.GetDefBonus(level)}\n";
            buffInfo += $"지속시간: {buffSkill.GetDuration(level)}초";
            SetTextSafe(specialText, buffInfo);
        }
        // 패시브 스킬 — 데미지 없음
        else if (skill is PassiveSkillData passiveSkill)
        {
            SetTextSafe(damageText,  "");
            SetTextSafe(specialText, GetPassiveDescription(passiveSkill, level));
        }
        else
        {
            SetTextSafe(damageText,  "");
            SetTextSafe(specialText, "");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 타입별 다음 레벨 계수 표시
    // ─────────────────────────────────────────────────────────────────

    private void ShowNextStatByType(SkillData skill, int nextLevel)
    {
        if (skill is DamageSkillData dmgSkill)
        {
            SetTextSafe(nextDamageText,  $"데미지: {dmgSkill.GetDamageMultiplier(nextLevel) * 100f:F1}%");
            SetTextSafe(nextSpecialText, "");
        }
        else if (skill is BuffSkillData buffSkill)
        {
            SetTextSafe(nextDamageText, "");
            string buffInfo = "";
            if (buffSkill.GetAtkBonus(nextLevel) > 0)  buffInfo += $"공격력 +{buffSkill.GetAtkBonus(nextLevel)}\n";
            if (buffSkill.GetApBonus(nextLevel) > 0)   buffInfo += $"주문력 +{buffSkill.GetApBonus(nextLevel)}\n";
            if (buffSkill.GetDefBonus(nextLevel) > 0)  buffInfo += $"방어력 +{buffSkill.GetDefBonus(nextLevel)}\n";
            buffInfo += $"지속시간: {buffSkill.GetDuration(nextLevel)}초";
            SetTextSafe(nextSpecialText, buffInfo);
        }
        else if (skill is PassiveSkillData passiveSkill)
        {
            SetTextSafe(nextDamageText,  "");
            SetTextSafe(nextSpecialText, GetPassiveDescription(passiveSkill, nextLevel));
        }
        else
        {
            SetTextSafe(nextDamageText,  "");
            SetTextSafe(nextSpecialText, "");
        }
    }

    private string GetPassiveDescription(PassiveSkillData passive, int level)
    {
        switch (passive.effectType)
        {
            case PassiveSkillData.PassiveEffectType.AtkPercent:
                return $"공격력 {passive.GetValue(level) * 100f:F1}% 증가";
            case PassiveSkillData.PassiveEffectType.ApPercent:
                return $"주문력 {passive.GetValue(level) * 100f:F1}% 증가";
            case PassiveSkillData.PassiveEffectType.DefPercent:
                return $"방어력 {passive.GetValue(level) * 100f:F1}% 증가";
            case PassiveSkillData.PassiveEffectType.CritRate:
                return $"치명타 확률 +{passive.GetValue(level) * 100f:F1}%";
            case PassiveSkillData.PassiveEffectType.CritDamage:
                return $"치명타 배율 +{passive.GetValue(level) * 100f:F1}%";
            case PassiveSkillData.PassiveEffectType.MaxHpPercent:
                return $"최대 체력 {passive.GetValue(level) * 100f:F1}% 증가";
            case PassiveSkillData.PassiveEffectType.OnCritLightning:
                return $"치명타 시 번개 발동 확률 {passive.GetProcChance(level) * 100f:F1}%";
            case PassiveSkillData.PassiveEffectType.OnHitPoison:
                return $"공격 시 독 발동 확률 {passive.GetProcChance(level) * 100f:F1}%";
            case PassiveSkillData.PassiveEffectType.OnKillHeal:
                return $"적 처치 시 체력 {passive.GetProcValue(level)} 회복";
            default:
                return "";
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 배우기 버튼
    // ─────────────────────────────────────────────────────────────────

    private void OnLearnButtonClicked()
    {
        if (currentSkill == null || DataManager.instance == null) return;

        CharacterStatus status = DataManager.instance.partyStatuses[currentCharIndex];
        bool success           = status.TryLevelUpSkill(currentSkill);

        if (!success)
        {
            Debug.Log("[SkillDetailPanelUI] 스킬 레벨업 실패");
            return;
        }

        skillWindow.OnSkillLevelUp();
        ShowSkillDetail(currentSkill, status, DataManager.instance.partyLevel, currentCharIndex);
    }

    // ─────────────────────────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────────────────────────

    private void SetTextSafe(TextMeshProUGUI tmp, string text)
    {
        if (tmp != null) tmp.text = text;
    }

    public void Clear()
    {
        currentSkill = null;

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
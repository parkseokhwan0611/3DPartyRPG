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
    public TextMeshProUGUI specialText;    // 특수 효과 텍스트

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

    // ─────────────────────────────────────────
    // 현재 표시 중인 스킬 데이터
    // ─────────────────────────────────────────
    private SkillData currentSkill;
    private int currentCharIndex;
    private SkillWindowUI skillWindow;

    // ─────────────────────────────────────────────────────────────────
    // 초기화
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
    // 스킬 상세 정보 표시
    // ─────────────────────────────────────────────────────────────────

    public void ShowSkillDetail(SkillData skill, CharacterStatus status, int partyLevel, int charIndex)
    {
        currentSkill     = skill;
        currentCharIndex = charIndex;

        int currentLevel = status.GetSkillLevel(skill);
        int nextLevel    = currentLevel + 1;

        // ── 현재 레벨 정보 ──
        if (skillNameText != null)
            skillNameText.text = skill.skillName;

        if (skillLvText != null)
            skillLvText.text = currentLevel > 0
                ? $"LV {currentLevel}"
                : "미습득";

        if (skillDescText != null)
            skillDescText.text = skill.description;

        if (currentLevel > 0)
        {
            int idx = currentLevel - 1;

            if (mpCostText != null)
                mpCostText.text = $"마나 소모량: {skill.mpCost[idx]}";

            if (cooldownText != null)
                cooldownText.text = $"쿨타임: {skill.cooldown[idx]}초";

            // 데미지 스킬이면 배율 표시
            if (skill is DamageSkillData dmgSkill && damageText != null)
                damageText.text = $"데미지: {dmgSkill.GetDamageMultiplier(currentLevel) * 100f:F1}%";

            // 버프 스킬이면 버프량 표시
            if (skill is BuffSkillData buffSkill && specialText != null)
                specialText.text = $"지속시간: {buffSkill.GetDuration(currentLevel)}초";
        }
        else
        {
            if (mpCostText != null)   mpCostText.text   = "";
            if (cooldownText != null) cooldownText.text = "";
            if (damageText != null)   damageText.text   = "";
            if (specialText != null)  specialText.text  = "";
        }

        // ── 다음 레벨 정보 ──
        bool isMaxLevel = currentLevel >= skill.maxLevel;

        if (nextSkillLvText != null)
            nextSkillLvText.text = isMaxLevel
                ? "최대 레벨"
                : $"LV {nextLevel}";

        if (!isMaxLevel)
        {
            int nextIdx = nextLevel - 1;

            if (nextMpCostText != null)
                nextMpCostText.text = $"마나 소모량: {skill.mpCost[nextIdx]}";

            if (nextCooldownText != null)
                nextCooldownText.text = $"쿨타임: {skill.cooldown[nextIdx]}초";

            if (skill is DamageSkillData dmgSkill && nextDamageText != null)
                nextDamageText.text = $"데미지: {dmgSkill.GetDamageMultiplier(nextLevel) * 100f:F1}%";

            if (skill is BuffSkillData buffSkill && nextSpecialText != null)
                nextSpecialText.text = $"지속시간: {buffSkill.GetDuration(nextLevel)}초";

            // 필요 스킬 포인트
            int cost = skill.skillPointCost[nextIdx];
            if (requiredSkillPointText != null)
                requiredSkillPointText.text = $"필요 스킬 포인트: {cost}";

            // 레벨 조건
            if (levelConditionText != null)
                levelConditionText.text = $"스킬 포인트 {status.skillPoint} 보유";

            // 배우기 버튼 활성화 여부
            bool canLearn = status.skillPoint >= cost && partyLevel >= 1;
            if (learnButton != null)
                learnButton.interactable = canLearn;
        }
        else
        {
            // 최대 레벨이면 다음 레벨 정보 비우기
            if (nextMpCostText != null)       nextMpCostText.text       = "";
            if (nextCooldownText != null)     nextCooldownText.text     = "";
            if (nextDamageText != null)       nextDamageText.text       = "";
            if (nextSpecialText != null)      nextSpecialText.text      = "";
            if (requiredSkillPointText != null) requiredSkillPointText.text = "";
            if (levelConditionText != null)   levelConditionText.text   = "";
            if (learnButton != null)          learnButton.interactable  = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 배우기 버튼
    // ─────────────────────────────────────────────────────────────────

    private void OnLearnButtonClicked()
    {
        if (currentSkill == null) return;
        if (DataManager.instance == null) return;

        CharacterStatus status = DataManager.instance.partyStatuses[currentCharIndex];

        bool success = status.TryLevelUpSkill(currentSkill);
        if (!success)
        {
            Debug.Log("[SkillDetailPanelUI] 스킬 레벨업 실패 (포인트 부족 또는 최대 레벨)");
            return;
        }

        // 스킬 트리 및 상세 패널 갱신
        skillWindow.OnSkillLevelUp();

        // 상세 패널 다시 표시
        ShowSkillDetail(
            currentSkill,
            status,
            DataManager.instance.partyLevel,
            currentCharIndex);
    }

    // ─────────────────────────────────────────────────────────────────
    // 초기화 (스킬 클릭 전 빈 상태)
    // ─────────────────────────────────────────────────────────────────

    public void Clear()
    {
        currentSkill = null;

        if (skillNameText != null)          skillNameText.text          = "";
        if (skillLvText != null)            skillLvText.text            = "";
        if (skillDescText != null)          skillDescText.text          = "";
        if (mpCostText != null)             mpCostText.text             = "";
        if (cooldownText != null)           cooldownText.text           = "";
        if (damageText != null)             damageText.text             = "";
        if (specialText != null)            specialText.text            = "";
        if (nextSkillLvText != null)        nextSkillLvText.text        = "";
        if (levelConditionText != null)     levelConditionText.text     = "";
        if (requiredSkillPointText != null) requiredSkillPointText.text = "";
        if (nextMpCostText != null)         nextMpCostText.text         = "";
        if (nextCooldownText != null)       nextCooldownText.text       = "";
        if (nextDamageText != null)         nextDamageText.text         = "";
        if (nextSpecialText != null)        nextSpecialText.text        = "";
        if (learnButton != null)            learnButton.interactable    = false;
    }
}
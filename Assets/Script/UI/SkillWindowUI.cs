using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SkillWindowUI : MonoBehaviour
{
    [Header("# 스킬 아이콘 슬롯")]
    public List<SkillIconUI> mainSlots    = new List<SkillIconUI>(); // 6개
    public List<SkillIconUI> subSlots     = new List<SkillIconUI>(); // 4개
    public List<SkillIconUI> passiveSlots = new List<SkillIconUI>(); // 6개

    [Header("# 패널 참조")]
    public SkillDetailPanelUI detailPanel;
    public QuickSlotUI quickSlotPanel;

    // ─────────────────────────────────────────
    // 현재 선택된 캐릭터
    // ─────────────────────────────────────────
    private int currentCharIndex = 0;

    // 레벨 조건
    private readonly int[] mainRequiredLevels    = { 1, 10, 20, 30, 40, 50 };
    private readonly int[] subRequiredLevels     = { 1, 10, 30, 50 };
    private readonly int[] passiveRequiredLevels = { 1, 10, 20, 30, 40, 50 };

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (PartyManager.instance?.currentLeader != null)
        {
            CharacterStat stat = PartyManager.instance.currentLeader.GetComponent<CharacterStat>();
            if (stat != null)
            {
                currentCharIndex = stat.partyIndex;
                RefreshSkillTree();
                quickSlotPanel.RefreshByCharIndex(currentCharIndex); // 추가
            }
        }
    }
    // ─────────────────────────────────────────────────────────────────
    // 캐릭터 탭 전환 (CharacterTabGroup 버튼에서 호출)
    // ─────────────────────────────────────────────────────────────────

    public void OnCharacterTabClicked(int charIndex)
    {
        currentCharIndex = charIndex;
        RefreshSkillTree();
        detailPanel.Clear();

        // 해당 캐릭터의 퀵슬롯으로 갱신
        quickSlotPanel.RefreshByCharIndex(charIndex);
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 트리 갱신
    // ─────────────────────────────────────────────────────────────────

    public void RefreshSkillTree()
    {
        if (DataManager.instance == null) return;

        CharacterStatus status   = DataManager.instance.partyStatuses[currentCharIndex];
        int partyLevel           = DataManager.instance.partyLevel;
        ClassSkillTree skillTree = DataManager.instance.GetSkillTree(status.classData.classType);

        if (skillTree == null)
        {
            Debug.LogWarning($"[SkillWindowUI] 스킬 트리가 없습니다!");
            return;
        }

        RefreshColumn(mainSlots,    skillTree.mainSkills,    mainRequiredLevels,    status, partyLevel);
        RefreshColumn(subSlots,     skillTree.subSkills,     subRequiredLevels,     status, partyLevel);
        RefreshColumn(passiveSlots, skillTree.passiveSkills, passiveRequiredLevels, status, partyLevel);

        detailPanel.RefreshSkillPoint(status.skillPoint);
    }

    private void RefreshColumn(
        List<SkillIconUI> slots,
        List<SkillData> skills,
        int[] requiredLevels,
        CharacterStatus status,
        int partyLevel)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (i >= skills.Count)
            {
                slots[i].SetEmpty();
                continue;
            }

            bool isUnlocked = partyLevel >= requiredLevels[i];
            slots[i].Setup(skills[i], status.GetSkillLevel(skills[i]), isUnlocked, this);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 아이콘 클릭 (SkillIconUI에서 호출)
    // ─────────────────────────────────────────────────────────────────

    public void OnSkillIconClicked(SkillData skill)
    {
        if (DataManager.instance == null) return;

        CharacterStatus status = DataManager.instance.partyStatuses[currentCharIndex];
        detailPanel.ShowSkillDetail(skill, status, DataManager.instance.partyLevel, currentCharIndex);
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 레벨업 후 갱신 (SkillDetailPanelUI에서 호출)
    // ─────────────────────────────────────────────────────────────────
    
    public void OnSkillLevelUp()
    {
        RefreshSkillTree();
    }


    public int GetCurrentCharIndex() => currentCharIndex;
}
using UnityEngine;

public class QuickSlotUI : MonoBehaviour
{
    [Header("# 퀵슬롯 (Q/W/E/R)")]
    public QuickSlot slotQ;
    public QuickSlot slotW;
    public QuickSlot slotE;
    public QuickSlot slotR;

    private QuickSlot[] slots;

    void Awake()
    {
        slots = new QuickSlot[] { slotQ, slotW, slotE, slotR };
    }
    // ─────────────────────────────────────────────────────────────────
    // 슬롯에 스킬 등록 (QuickSlot.OnDrop에서 호출)
    // ─────────────────────────────────────────────────────────────────

    public void RegisterSkill(int slotIndex, SkillData skill)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        // 현재 스킬창에서 보고 있는 캐릭터 기준으로 등록
        SkillWindowUI skillWindow = FindObjectOfType<SkillWindowUI>();
        int charIndex = skillWindow != null
            ? skillWindow.GetCurrentCharIndex()
            : 0;

        PartyMemberScript member = PartyManager.instance.partyMembers[charIndex];
        SkillManager skillManager = member?.GetComponent<SkillManager>();

        // 동일 스킬이 다른 슬롯에 이미 등록돼 있으면 해당 슬롯 해제
        if (skill != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (i == slotIndex) continue;
                if (slots[i] != null && slots[i].AssignedSkill == skill)
                {
                    slots[i].SetSkill(null);
                    skillManager?.SetSlot(i, null);
                }
            }
        }

        if (skillManager != null)
            skillManager.SetSlot(slotIndex, skill);

        // 슬롯 UI 갱신
        slots[slotIndex]?.SetSkill(skill);

        // 전투 UI도 갱신 (리더일 때만)
        if (charIndex == PartyManager.instance.currentLeader
            .GetComponent<CharacterStat>().partyIndex)
        {
            CombatQuickSlotUI combatUI = FindObjectOfType<CombatQuickSlotUI>();
            if (combatUI != null) combatUI.RefreshSlots();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 쿨다운 갱신
    // ─────────────────────────────────────────────────────────────────

    void Update()
    {
        if (PartyManager.instance?.currentLeader == null) return;

        SkillManager skillManager = PartyManager.instance.currentLeader
            .GetComponent<SkillManager>();

        if (skillManager == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].UpdateCooldown(skillManager.GetCooldownRatio(i));
        }
    }
    public void RefreshByCharIndex(int charIndex)
    {
        // slots가 초기화 안 됐으면 초기화
        if (slots == null)
            slots = new QuickSlot[] { slotQ, slotW, slotE, slotR };
            
        if (PartyManager.instance == null) return;

        // 해당 인덱스의 파티원 SkillManager 가져오기
        if (charIndex >= PartyManager.instance.partyMembers.Count) return;

        PartyMemberScript member = PartyManager.instance.partyMembers[charIndex];
        SkillManager skillManager = member.GetComponent<SkillManager>();
        if (skillManager == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            SkillBase skill = skillManager.GetSlot(i);
            slots[i].SetSkill(skill?.skillData);
        }
    }
}
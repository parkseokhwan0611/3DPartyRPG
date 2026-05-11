using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatQuickSlotUI : MonoBehaviour
{
    [Header("# 전투 퀵슬롯 (Q/W/E/R)")]
    public CombatQuickSlot slotQ;
    public CombatQuickSlot slotW;
    public CombatQuickSlot slotE;
    public CombatQuickSlot slotR;

    private CombatQuickSlot[] slots;
    private SkillManager currentSkillManager;

    void Awake()
    {
        slots = new CombatQuickSlot[] { slotQ, slotW, slotE, slotR };
    }

    void Start()
    {
        RefreshSlots();
    }

    void Update()
    {
        // 리더가 바뀌면 슬롯 갱신
        if (PartyManager.instance?.currentLeader == null) return;

        SkillManager sm = PartyManager.instance.currentLeader.GetComponent<SkillManager>();

        // 리더가 바뀐 경우 갱신
        if (sm != currentSkillManager)
        {
            currentSkillManager = sm;
            RefreshSlots();
        }

        // 쿨다운 갱신
        UpdateCooldowns();
    }

    // ─────────────────────────────────────────────────────────────────
    // 슬롯 갱신 (스킬창에서 등록 후 호출)
    // ─────────────────────────────────────────────────────────────────

    public void RefreshSlots()
    {
        if (PartyManager.instance?.currentLeader == null) return;

        currentSkillManager = PartyManager.instance.currentLeader
            .GetComponent<SkillManager>();

        if (currentSkillManager == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            SkillBase skill = currentSkillManager.GetSlot(i);
            slots[i].SetSkill(skill?.skillData);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 쿨다운 갱신
    // ─────────────────────────────────────────────────────────────────

    private void UpdateCooldowns()
    {
        if (currentSkillManager == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            slots[i].UpdateCooldown(currentSkillManager.GetCooldownRatio(i));
        }
    }
}
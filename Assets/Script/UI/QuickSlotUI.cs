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

        slots[slotIndex].SetSkill(skill);

        // SkillManager에도 반영 (현재 리더 기준)
        if (PartyManager.instance?.currentLeader != null)
        {
            SkillManager skillManager = PartyManager.instance.currentLeader
                .GetComponent<SkillManager>();

            if (skillManager != null)
                skillManager.SetSlot(slotIndex, skill);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 쿨다운 갱신 (매 프레임 호출)
    // ─────────────────────────────────────────────────────────────────

    void Update()
    {
        if (PartyManager.instance?.currentLeader == null) return;

        SkillManager skillManager = PartyManager.instance.currentLeader
            .GetComponent<SkillManager>();

        if (skillManager == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            slots[i].UpdateCooldown(skillManager.GetCooldownRatio(i));
        }
    }
}
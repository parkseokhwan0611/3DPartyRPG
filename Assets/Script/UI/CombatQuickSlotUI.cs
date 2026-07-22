using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatQuickSlotUI : MonoBehaviour
{
    public static CombatQuickSlotUI instance;

    [Header("# 전투 퀵슬롯 (Q/W/E/R)")]
    public CombatQuickSlot slotQ;
    public CombatQuickSlot slotW;
    public CombatQuickSlot slotE;
    public CombatQuickSlot slotR;

    private CombatQuickSlot[] slots;
    private SkillManager currentSkillManager;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;

        slots = new CombatQuickSlot[] { slotQ, slotW, slotE, slotR };
    }

    void Start()
    {
        RefreshSlots();
    }

    void OnEnable()
    {
        if (PartyManager.instance != null)
            PartyManager.instance.OnLeaderChanged += HandleLeaderChanged;
    }

    void OnDisable()
    {
        if (PartyManager.instance != null)
            PartyManager.instance.OnLeaderChanged -= HandleLeaderChanged;
    }

    private void HandleLeaderChanged(PartyMemberScript newLeader)
    {
        RefreshSlots();
    }

    void Update()
    {
        // 쿨다운 갱신 (리더 변경 감지는 OnLeaderChanged 이벤트로 처리)
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
            slots[i].SetSkill(skill);
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
            slots[i].UpdateCooldown(
                currentSkillManager.GetCooldownRatio(i),
                currentSkillManager.GetCooldownRemaining(i),
                currentSkillManager.GetBuffRemainingRatio(i)
            );
        }
    }
}
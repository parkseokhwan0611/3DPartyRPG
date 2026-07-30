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

    // OnEnable은 PartyManager.Awake()보다 먼저 실행될 수 있어(오브젝트 간 실행 순서 미보장)
    // 구독이 조용히 누락될 수 있음 — Awake는 항상 모든 Start보다 먼저 끝나는 것이 보장되므로
    // Start에서 구독 (PotionCombatSlotUI/AutoSkillIndicatorUI와 동일한 패턴)
    void Start()
    {
        if (PartyManager.instance != null)
            PartyManager.instance.OnLeaderChanged += HandleLeaderChanged;

        RefreshSlots();
    }

    void OnDestroy()
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
        // OnLeaderChanged 이벤트가 (씬 전환 등 타이밍 문제로) 누락되더라도 화면이 이전
        // 캐릭터 스킬로 고정돼버리지 않도록, 실제 리더 기준 SkillManager와 어긋나 있으면
        // 매 프레임 자동으로 다시 동기화
        var actualLeaderSkillManager = PartyManager.instance?.currentLeader?.GetComponent<SkillManager>();
        if (actualLeaderSkillManager != null && actualLeaderSkillManager != currentSkillManager)
            RefreshSlots();

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
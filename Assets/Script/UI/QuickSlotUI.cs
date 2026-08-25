using UnityEngine;

public class QuickSlotUI : MonoBehaviour
{
    [Header("# 퀵슬롯 (Q/W/E/R)")]
    public QuickSlot slotQ;
    public QuickSlot slotW;
    public QuickSlot slotE;
    public QuickSlot slotR;

    private QuickSlot[] slots;
    private int currentCharIndex = -1; // -1 = 리더 기준
    private SkillManager _cachedSkillManager;
    private SkillWindowUI _cachedSkillWindow;

    void Awake()
    {
        slots = new QuickSlot[] { slotQ, slotW, slotE, slotR };
    }

    // OnEnable은 PartyManager.Awake()보다 먼저 실행될 수 있어(오브젝트 간 실행 순서 미보장)
    // 구독이 조용히 누락될 수 있음 — Awake는 항상 모든 Start보다 먼저 끝나는 것이 보장되므로
    // Start에서 구독
    void Start()
    {
        if (PartyManager.instance != null)
            PartyManager.instance.OnLeaderChanged += HandleLeaderChanged;
    }

    void OnDestroy()
    {
        if (PartyManager.instance != null)
            PartyManager.instance.OnLeaderChanged -= HandleLeaderChanged;
    }

    // -1(리더 기준) 상태일 때만 리더 변경에 반응 — 특정 캐릭터를 보고 있을 땐 영향 없음
    private void HandleLeaderChanged(PartyMemberScript newLeader)
    {
        if (currentCharIndex != -1) return;
        _cachedSkillManager = newLeader != null ? newLeader.GetComponent<SkillManager>() : null;
    }
    // ─────────────────────────────────────────────────────────────────
    // 슬롯에 스킬 등록 (QuickSlot.OnDrop에서 호출)
    // ─────────────────────────────────────────────────────────────────

    public void RegisterSkill(int slotIndex, SkillData skill)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        // 현재 스킬창에서 보고 있는 캐릭터 기준으로 등록
        // 스킬창은 평소 비활성 패널일 수 있어 매번 스캔하지 않고 최초 1회만 찾아서 캐싱
        if (_cachedSkillWindow == null)
            _cachedSkillWindow = FindObjectOfType<SkillWindowUI>(true);

        int charIndex = _cachedSkillWindow != null
            ? _cachedSkillWindow.GetCurrentCharIndex()
            : 0;

        if (PartyManager.instance == null) return;
        if (charIndex < 0 || charIndex >= PartyManager.instance.partyMembers.Count) return;

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
        CharacterStat leaderStat = PartyManager.instance.currentLeader != null
            ? PartyManager.instance.currentLeader.GetComponent<CharacterStat>()
            : null;
        if (leaderStat != null && charIndex == leaderStat.partyIndex)
        {
            CombatQuickSlotUI.instance?.RefreshSlots();
        }
    }

    // 더블클릭 등록용 — 빈 슬롯이 있으면 거기에, 없으면 첫 슬롯(Q)에 등록 (드래그와 동일한 결과)
    public void RegisterSkillToEmptySlot(SkillData skill)
    {
        if (skill == null || slots == null) return;

        // 이미 등록된 슬롯이 있으면 그대로 둠 (드래그 등록과 동일하게 취급 — 다른 슬롯을 잃지 않도록)
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].AssignedSkill == skill)
                return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].AssignedSkill == null)
            {
                RegisterSkill(i, skill);
                return;
            }
        }

        RegisterSkill(0, skill);
    }

    // ─────────────────────────────────────────────────────────────────
    // 쿨다운 갱신
    // ─────────────────────────────────────────────────────────────────

    void Update()
    {
        if (_cachedSkillManager == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].UpdateCooldown(_cachedSkillManager.GetCooldownRatio(i));
        }
    }

    public void RefreshByCharIndex(int charIndex)
    {
        currentCharIndex = charIndex;

        if (slots == null)
            slots = new QuickSlot[] { slotQ, slotW, slotE, slotR };

        if (PartyManager.instance == null) return;

        // 해당 인덱스의 파티원 SkillManager 가져오기
        if (charIndex >= PartyManager.instance.partyMembers.Count) return;

        PartyMemberScript member = PartyManager.instance.partyMembers[charIndex];
        SkillManager skillManager = member.GetComponent<SkillManager>();
        if (skillManager == null) return;

        _cachedSkillManager = skillManager; // 쿨다운 표시용 캐시 갱신

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            SkillBase skill = skillManager.GetSlot(i);
            slots[i].SetSkill(skill?.skillData);
        }
    }
}
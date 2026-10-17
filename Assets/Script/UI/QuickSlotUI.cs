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

    // 등록(교체·해제 포함)에 성공하면 true. 쿨타임 중인 스킬이 걸려 있으면 거절하고 false
    public bool RegisterSkill(int slotIndex, SkillData skill)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return false;

        // 현재 스킬창에서 보고 있는 캐릭터 기준으로 등록
        // 스킬창은 평소 비활성 패널일 수 있어 매번 스캔하지 않고 최초 1회만 찾아서 캐싱
        if (_cachedSkillWindow == null)
            _cachedSkillWindow = FindObjectOfType<SkillWindowUI>(true);

        int charIndex = _cachedSkillWindow != null
            ? _cachedSkillWindow.GetCurrentCharIndex()
            : 0;

        if (PartyManager.instance == null) return false;
        if (charIndex < 0 || charIndex >= PartyManager.instance.partyMembers.Count) return false;

        PartyMemberScript member = PartyManager.instance.partyMembers[charIndex];
        SkillManager skillManager = member?.GetComponent<SkillManager>();

        // 쿨타임 중인 스킬은 교체·해제·다른 슬롯으로 옮기기 불가 — 슬롯을 바꾸면 스킬이 새로 만들어져 쿨타임이 초기화되므로
        if (skillManager != null && IsBlockedByCooldown(skillManager, slotIndex, skill, out SkillBase coolingSkill))
        {
            ShowMessage("쿨타임 중인 스킬은 교체할 수 없습니다.", coolingSkill);
            return false;
        }

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
        return true;
    }

    // 대상 슬롯의 지금 스킬이 쿨타임 중이거나(교체·해제), 등록하려는 스킬이 다른 슬롯에서 쿨타임 중이면(이동) 막는다.
    // 같은 슬롯에 같은 스킬을 다시 놓는 건 아무것도 안 바뀌므로 허용
    private bool IsBlockedByCooldown(SkillManager skillManager, int slotIndex, SkillData skill, out SkillBase coolingSkill)
    {
        coolingSkill = null;
        SkillBase current = skillManager.GetSlot(slotIndex);
        if (current != null && current.skillData == skill) return false;

        if (skillManager.IsSlotOnCooldown(slotIndex)) { coolingSkill = current; return true; }

        if (skill != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (i == slotIndex) continue;
                SkillBase other = skillManager.GetSlot(i);
                if (other != null && other.skillData == skill && skillManager.IsSlotOnCooldown(i)) { coolingSkill = other; return true; }
            }
        }
        return false;
    }

    // ─────────────────────────────────────────────────────────────────
    // 안내 메시지 — 퀵슬롯 안내 문구("스킬을 드래그해 등록" 등) 자리에 잠깐 띄웠다가 원래 문구로 되돌린다
    // ─────────────────────────────────────────────────────────────────

    [Header("# 안내 메시지 (선택)")]
    [Tooltip("퀵슬롯 안내 문구 텍스트. 쿨타임 중 교체를 시도하면 이 자리에 경고를 띄웠다가 원래 문구로 되돌린다. " +
             "비워두면 콘솔 로그만 남김")]
    [SerializeField] TMPro.TextMeshProUGUI messageText;
    [Tooltip("경고 문구를 띄워두는 시간(초). 그 전에 해당 스킬의 쿨타임이 끝나면 바로 원래 문구로 돌아간다")]
    [SerializeField] float messageRevertDelay = 3f;

    private string    _originalMessage;   // 원래 안내 문구 (처음 경고를 띄울 때 보관)
    private bool      _messageShowing;
    private float     _messageEndTime;
    private SkillBase _messageBlockingSkill;

    private void ShowMessage(string message, SkillBase blockingSkill)
    {
        if (messageText == null) { Debug.Log($"[QuickSlotUI] {message}"); return; }

        if (!_messageShowing) _originalMessage = messageText.text;
        messageText.text      = message;
        _messageShowing       = true;
        _messageEndTime       = Time.unscaledTime + messageRevertDelay;
        _messageBlockingSkill = blockingSkill;
    }

    private void UpdateMessage()
    {
        if (!_messageShowing) return;

        bool cooldownDone = _messageBlockingSkill == null || _messageBlockingSkill.CooldownRemaining <= 0f;
        if (cooldownDone || Time.unscaledTime >= _messageEndTime)
            RestoreMessage();
    }

    private void RestoreMessage()
    {
        if (!_messageShowing) return;
        _messageShowing       = false;
        _messageBlockingSkill = null;
        if (messageText != null) messageText.text = _originalMessage;
    }

    // 스킬창을 닫으면 경고를 남겨두지 않는다
    void OnDisable() => RestoreMessage();

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
        UpdateMessage();

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
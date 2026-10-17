using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC 오브젝트에 붙이는 상호작용 컴포넌트.
/// 범위 내 접근 시 [F] 프롬프트 표시 → F키로 대화 시작 → 완료 시 상점/강화 UI 오픈.
/// </summary>
public class NpcInteractable : MonoBehaviour
{
    public enum NpcType { Shop, Enhancement, Story }

    [Header("NPC 정보")]
    [SerializeField] string  npcName = "상인";
    [Tooltip("세이브/로드용 고유 ID. 비워두면 GameObject 이름을 사용.")]
    [SerializeField] string  npcId;
    [SerializeField] NpcType npcType = NpcType.Shop;

    [Header("대화 (순서대로 출력)")]
    [SerializeField] DialogueLine[] dialogueLines;

    [Header("퀘스트 보고 전 대사 (선택)")]
    [Tooltip("이 NPC가 현재 퀘스트의 처치 보고 대상(QuestData.Report Npc Id)인데 아직 처치 수량을 " +
             "다 못 채웠을 때 Dialogue Lines 대신 보여줄 대사. 비워두면 기본 문구 사용")]
    [SerializeField] DialogueLine[] notReadyDialogueLines;

    [Header("상점 데이터 (Shop 타입만)")]
    [SerializeField] ShopData shopData;

    [Header("상호작용 설정")]
    [Tooltip("리더와의 상호작용 가능 거리 (m)")]
    [SerializeField] float      interactRange = 2f;
    [Tooltip("NPC 머리 위에 배치한 WorldSpace [F] 프롬프트 오브젝트")]
    [SerializeField] GameObject promptObject;

    // ─────────────────────────────────────────────────────────────────
    // 런타임 재고 추적 (수량 제한 아이템)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>DataManager가 없을 때(이 씬만 단독 실행 등)만 쓰는 임시 재고. 평소엔 DataManager.npcStocks에 보관 —
    /// NPC는 씬 로컬이라 여기에만 두면 씬을 나갔다 오면 재고가 다시 찼음</summary>
    private Dictionary<int, int> _localStock = new Dictionary<int, int>();

    private bool _playerInRange    = false;
    private bool _isDialogueActive = false;

    // 씬 내 모든 NPC 인스턴스 — SaveManager가 매번 FindObjectsOfType으로 스캔하지 않도록 자체 등록
    public static readonly List<NpcInteractable> AllInstances = new List<NpcInteractable>();

    void OnEnable()  => AllInstances.Add(this);
    void OnDisable() => AllInstances.Remove(this);

    // NPC 대화 퀘스트 등, 개별 NPC를 구독하기 어려운 전역 리스너용
    public static event System.Action<NpcInteractable> OnAnyDialogueComplete;

    // ─────────────────────────────────────────────────────────────────
    // 공개 접근자 (ShopUI / SaveManager에서 사용)
    // ─────────────────────────────────────────────────────────────────

    public ShopData ShopData => shopData;
    public string   NpcName  => npcName;
    public string   NpcId    => !string.IsNullOrEmpty(npcId) ? npcId : gameObject.name;
    public NpcType  Type     => npcType;

    /// <summary>해당 ShopEntry 인덱스의 남은 재고. 제한 없으면 int.MaxValue.</summary>
    public int GetRemainingStock(int entryIndex)
    {
        if (shopData == null || entryIndex < 0 || entryIndex >= shopData.entries.Count) return 0;
        var entry = shopData.entries[entryIndex];
        if (!entry.hasStockLimit) return int.MaxValue;

        int v;
        bool found = DataManager.instance != null
            ? DataManager.instance.TryGetNpcStock(NpcId, entryIndex, out v)
            : _localStock.TryGetValue(entryIndex, out v);
        return found ? v : entry.maxStock;
    }

    /// <summary>구매 시 재고 차감. 재고 부족이면 false.</summary>
    public bool TryConsumeStock(int entryIndex, int amount)
    {
        if (shopData == null || entryIndex < 0 || entryIndex >= shopData.entries.Count) return false;
        var entry = shopData.entries[entryIndex];
        if (!entry.hasStockLimit) return true;

        int remaining = GetRemainingStock(entryIndex);
        if (remaining < amount) return false;

        if (DataManager.instance != null) DataManager.instance.SetNpcStock(NpcId, entryIndex, remaining - amount);
        else                              _localStock[entryIndex] = remaining - amount;
        return true;
    }
    // 재고 저장·불러오기는 DataManager.GetSaveData/LoadSaveData가 모든 NPC 몫을 한꺼번에 처리한다

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Start()
    {
        if (promptObject != null) promptObject.SetActive(false);
    }

    void Update()
    {
        UpdateProximity();

        // 인벤토리·상점·대화 UI가 열려있으면 F키 차단
        bool anyUiOpen = MenuTabUI.IsOpen || ShopUI.IsOpen || DialogueUI.IsOpen || EnhancementUI.IsOpen || SceneLoader.IsLoading;
        if (_playerInRange && !_isDialogueActive && !anyUiOpen && Input.GetKeyDown(KeyCode.F))
            StartDialogue();
    }

    // ─────────────────────────────────────────────────────────────────
    // 거리 체크 / 프롬프트
    // ─────────────────────────────────────────────────────────────────

    void UpdateProximity()
    {
        var leader = PartyManager.instance?.currentLeader;
        if (leader == null) { ShowPrompt(false); return; }

        float dist    = Vector3.Distance(transform.position, leader.transform.position);
        bool  inRange = dist <= interactRange;

        if (inRange == _playerInRange) return;

        _playerInRange = inRange;
        ShowPrompt(inRange && !_isDialogueActive);

        if (!inRange && ShopUI.IsOpen && ShopUI.instance?.CurrentNpc == this)
            ShopUI.instance.Close();
    }

    void ShowPrompt(bool active)
    {
        if (promptObject != null) promptObject.SetActive(active);
    }

    // ─────────────────────────────────────────────────────────────────
    // 대화 → UI 오픈 흐름
    // ─────────────────────────────────────────────────────────────────

    // 이 NPC가 현재 진행 중인 퀘스트의 처치 보고 대상인데, 아직 목표 수량을 다 못 채운 상태인지
    bool IsPendingReportTargetNotReady()
    {
        var qm    = QuestManager.instance;
        var quest = qm?.CurrentQuest;
        if (quest == null || !quest.RequiresReport) return false;
        if (quest.reportNpcId != NpcId) return false;

        return !qm.IsAwaitingReport;
    }

    // 이 NPC가 지금 퀘스트가 아니라 체인의 다음 퀘스트(대화 대상·보고 대상)에서 등장할 차례인지 —
    // 순서를 건너뛰고 먼저 찾아오면 퀘스트 대사(Dialogue Lines) 대신 미완료 대사를 보여주기 위함
    bool IsFutureQuestTarget()
    {
        if (npcType != NpcType.Story) return false;

        var qm      = QuestManager.instance;
        var current = qm?.CurrentQuest;
        if (current == null || qm.Chain == null) return false;

        // 지금 퀘스트의 대상이면 기존 흐름(대화 완료 / 보고 미완료 판정)을 따름
        if (current.objectiveType == QuestObjectiveType.NpcDialogue && current.targetNpcId == NpcId) return false;
        if (current.RequiresReport && current.reportNpcId == NpcId) return false;

        var quests = qm.Chain.quests;
        for (int i = qm.CurrentQuestIndex + 1; i < quests.Count; i++)
        {
            var q = quests[i];
            if (q == null) continue;
            if (q.objectiveType == QuestObjectiveType.NpcDialogue && q.targetNpcId == NpcId) return true;
            if (q.RequiresReport && q.reportNpcId == NpcId) return true;
        }
        return false;
    }

    void StartDialogue()
    {
        if (DialogueUI.instance == null) return;
        _isDialogueActive = true;
        ShowPrompt(false);

        DialogueLine[] lines;
        if (IsPendingReportTargetNotReady())
        {
            lines = (notReadyDialogueLines != null && notReadyDialogueLines.Length > 0)
                ? notReadyDialogueLines
                : new[] { new DialogueLine { speaker = DialogueLine.Speaker.NPC, text = "아직 처치를 다 마치지 못한 것 같군요." } };
        }
        else if (IsFutureQuestTarget())
        {
            lines = (notReadyDialogueLines != null && notReadyDialogueLines.Length > 0)
                ? notReadyDialogueLines
                : new[] { new DialogueLine { speaker = DialogueLine.Speaker.NPC, text = "아직은 때가 아닙니다." } };
        }
        else
        {
            lines = (dialogueLines != null && dialogueLines.Length > 0)
                ? dialogueLines
                : new[] { new DialogueLine { speaker = DialogueLine.Speaker.NPC, text = "어서 오세요." } };
        }

        DialogueUI.instance.Open(npcName, lines, OnDialogueComplete);
    }

    void OnDialogueComplete()
    {
        _isDialogueActive = false;

        switch (npcType)
        {
            case NpcType.Shop:
                ShopUI.instance?.Open(this);
                break;

            case NpcType.Enhancement:
                EnhancementUI.instance?.Open();
                break;

            case NpcType.Story:
                break;
        }

        // 플레이어가 범위를 벗어나지 않은 이상 UpdateProximity의 진입/이탈 전환이 발생하지 않아
        // 프롬프트가 계속 숨겨진 채로 남으므로, 아직 범위 안이면 여기서 다시 표시
        if (_playerInRange)
            ShowPrompt(true);

        OnAnyDialogueComplete?.Invoke(this);
    }
}

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

    /// <summary>남은 재고. ShopData.entries 인덱스 → 남은 수량.</summary>
    private Dictionary<int, int> _remainingStock = new Dictionary<int, int>();

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

    /// <summary>해당 ShopEntry 인덱스의 남은 재고. 제한 없으면 int.MaxValue.</summary>
    public int GetRemainingStock(int entryIndex)
    {
        if (shopData == null || entryIndex < 0 || entryIndex >= shopData.entries.Count) return 0;
        var entry = shopData.entries[entryIndex];
        if (!entry.hasStockLimit) return int.MaxValue;
        return _remainingStock.TryGetValue(entryIndex, out int v) ? v : entry.maxStock;
    }

    /// <summary>구매 시 재고 차감. 재고 부족이면 false.</summary>
    public bool TryConsumeStock(int entryIndex, int amount)
    {
        if (shopData == null || entryIndex < 0 || entryIndex >= shopData.entries.Count) return false;
        var entry = shopData.entries[entryIndex];
        if (!entry.hasStockLimit) return true;

        int remaining = GetRemainingStock(entryIndex);
        if (remaining < amount) return false;

        _remainingStock[entryIndex] = remaining - amount;
        return true;
    }

    /// <summary>세이브용: 현재 재고 상태 반환 (hasStockLimit 항목만).</summary>
    public NpcStockSave GetStockSave()
    {
        var save = new NpcStockSave { npcId = NpcId };
        foreach (var kvp in _remainingStock)
            save.stocks.Add(new NpcStockEntry { entryIndex = kvp.Key, remaining = kvp.Value });
        return save;
    }

    /// <summary>로드용: 저장된 재고 상태 복원.</summary>
    public void RestoreStock(NpcStockSave save)
    {
        _remainingStock.Clear();
        if (save?.stocks == null) return;
        foreach (var entry in save.stocks)
            _remainingStock[entry.entryIndex] = entry.remaining;
    }

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
        bool anyUiOpen = MenuTabUI.IsOpen || ShopUI.IsOpen || DialogueUI.IsOpen || EnhancementUI.IsOpen;
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

    void StartDialogue()
    {
        if (DialogueUI.instance == null) return;
        _isDialogueActive = true;
        ShowPrompt(false);

        DialogueLine[] lines = (dialogueLines != null && dialogueLines.Length > 0)
            ? dialogueLines
            : new[] { new DialogueLine { speaker = DialogueLine.Speaker.NPC, text = "어서 오세요." } };

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

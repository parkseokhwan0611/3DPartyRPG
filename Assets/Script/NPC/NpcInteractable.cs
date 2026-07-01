using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC 오브젝트에 붙이는 상호작용 컴포넌트.
/// 범위 내 접근 시 [F] 프롬프트 표시 → F키로 대화 시작 → 완료 시 상점/강화 UI 오픈.
/// </summary>
public class NpcInteractable : MonoBehaviour
{
    public enum NpcType { Shop, Enhancement }

    [Header("NPC 정보")]
    [SerializeField] string  npcName = "상인";
    [SerializeField] NpcType npcType = NpcType.Shop;

    [Header("대화 (순서대로 출력)")]
    [SerializeField] [TextArea(2, 4)] string[] dialogueLines;

    [Header("상점 데이터 (Shop 타입만)")]
    [SerializeField] ShopData shopData;

    [Header("상호작용 설정")]
    [Tooltip("리더와의 상호작용 가능 거리 (m)")]
    [SerializeField] float      interactRange = 3f;
    [Tooltip("NPC 머리 위에 배치한 WorldSpace [F] 프롬프트 오브젝트")]
    [SerializeField] GameObject promptObject;

    // ─────────────────────────────────────────────────────────────────
    // 런타임 재고 추적 (수량 제한 아이템)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>남은 재고. ShopData.entries 인덱스 → 남은 수량.</summary>
    private Dictionary<int, int> _remainingStock = new Dictionary<int, int>();

    private bool _playerInRange    = false;
    private bool _isDialogueActive = false;

    // ─────────────────────────────────────────────────────────────────
    // 공개 접근자 (ShopUI에서 사용)
    // ─────────────────────────────────────────────────────────────────

    public ShopData  ShopData  => shopData;
    public string    NpcName   => npcName;

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

        if (_playerInRange && !_isDialogueActive && Input.GetKeyDown(KeyCode.F))
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

        string[] lines = (dialogueLines != null && dialogueLines.Length > 0)
            ? dialogueLines
            : new[] { "어서 오세요." };

        DialogueUI.instance.Open(npcName, lines, OnDialogueComplete);
    }

    void OnDialogueComplete()
    {
        _isDialogueActive = false;

        switch (npcType)
        {
            case NpcType.Shop:
                // ShopUI 구현 후 연결: ShopUI.instance?.Open(this);
                Debug.Log($"[NPC] {npcName} 상점 오픈 (ShopUI 미구현)");
                break;

            case NpcType.Enhancement:
                // EnhancementUI 구현 후 연결: EnhancementUI.instance?.Open();
                Debug.Log($"[NPC] {npcName} 강화 UI 오픈 (EnhancementUI 미구현)");
                break;
        }
    }
}

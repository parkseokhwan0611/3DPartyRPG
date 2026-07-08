using System;
using System.Collections.Generic;
using UnityEngine;

// 메인 퀘스트 진행 관리 — 서브 퀘스트 없이 MainQuestChain을 하나씩 순차 진행.
// NPC 대화/아이템 수집/몬스터 처치 세 가지 목표 유형을 전역 이벤트로 감지해 자동 진행.
public class QuestManager : MonoBehaviour
{
    public static QuestManager instance;

    [SerializeField] private MainQuestChain mainQuestChain;

    private int currentQuestIndex = 0;
    private int currentProgress   = 0;
    private List<string> completedQuestIds = new List<string>();
    private bool _completing = false;
    private Inventory _subscribedInventory;

    public QuestData CurrentQuest =>
        (mainQuestChain != null && currentQuestIndex >= 0 && currentQuestIndex < mainQuestChain.quests.Count)
            ? mainQuestChain.quests[currentQuestIndex]
            : null;

    public int CurrentProgress => currentProgress;
    public IReadOnlyList<string> CompletedQuestIds => completedQuestIds;
    public bool IsAllQuestsComplete => mainQuestChain != null && currentQuestIndex >= mainQuestChain.quests.Count;
    public MainQuestChain Chain => mainQuestChain;

    // 활성 퀘스트가 바뀔 때(새 퀘스트 시작/전체 완료) — UI 전체 갱신용
    public event Action OnQuestChanged;
    // 같은 퀘스트 내에서 진행도(수집/처치 카운트)만 바뀔 때
    public event Action OnQuestProgressChanged;

    // ─────────────────────────────────────────────────────────────────
    // 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        EnemyHp.OnAnyEnemyDied += HandleEnemyDied;
        NpcInteractable.OnAnyDialogueComplete += HandleDialogueComplete;

        if (DataManager.instance != null)
        {
            DataManager.instance.OnDataInitialized += HandleDataInitialized;
            RefreshInventorySubscription();
        }
    }

    void OnDestroy()
    {
        EnemyHp.OnAnyEnemyDied -= HandleEnemyDied;
        NpcInteractable.OnAnyDialogueComplete -= HandleDialogueComplete;

        if (DataManager.instance != null)
            DataManager.instance.OnDataInitialized -= HandleDataInitialized;

        if (_subscribedInventory != null)
            _subscribedInventory.OnChanged -= HandleInventoryChanged;
    }

    private void HandleDataInitialized()
    {
        RefreshInventorySubscription();
    }

    private void RefreshInventorySubscription()
    {
        if (_subscribedInventory != null)
            _subscribedInventory.OnChanged -= HandleInventoryChanged;

        _subscribedInventory = DataManager.instance != null ? DataManager.instance.sharedInventory : null;

        if (_subscribedInventory != null)
            _subscribedInventory.OnChanged += HandleInventoryChanged;
    }

    // ─────────────────────────────────────────────────────────────────
    // 새 게임 / 로드 진입점 (TitleMenuUI / SaveManager에서 호출)
    // ─────────────────────────────────────────────────────────────────

    public void StartNewGame()
    {
        currentQuestIndex = 0;
        currentProgress   = 0;
        completedQuestIds.Clear();
        RefreshInventorySubscription();
        OnQuestChanged?.Invoke();
    }

    public void RestoreProgress(GameSaveData data)
    {
        if (data == null) return;
        currentQuestIndex = data.currentQuestIndex;
        currentProgress   = data.currentQuestProgress;
        completedQuestIds = new List<string>(data.completedQuestIds ?? new List<string>());
        RefreshInventorySubscription();
        OnQuestChanged?.Invoke();
    }

    public void FillSaveData(GameSaveData data)
    {
        if (data == null) return;
        data.currentQuestIndex    = currentQuestIndex;
        data.currentQuestProgress = currentProgress;
        data.completedQuestIds    = new List<string>(completedQuestIds);
    }

    // ─────────────────────────────────────────────────────────────────
    // 목표 감지
    // ─────────────────────────────────────────────────────────────────

    private void HandleEnemyDied(EnemyHp enemy)
    {
        var quest = CurrentQuest;
        if (quest == null || quest.objectiveType != QuestObjectiveType.KillMonster) return;
        if (enemy == null || string.IsNullOrEmpty(quest.targetMonsterId)) return;
        if (enemy.monsterId != quest.targetMonsterId) return;

        currentProgress = Mathf.Min(currentProgress + 1, quest.TargetCount);
        OnQuestProgressChanged?.Invoke();

        if (currentProgress >= quest.TargetCount)
            CompleteCurrentQuest();
    }

    private void HandleDialogueComplete(NpcInteractable npc)
    {
        var quest = CurrentQuest;
        if (quest == null || quest.objectiveType != QuestObjectiveType.NpcDialogue) return;
        if (npc == null || string.IsNullOrEmpty(quest.targetNpcId)) return;
        if (npc.NpcId != quest.targetNpcId) return;

        currentProgress = 1;
        CompleteCurrentQuest();
    }

    private void HandleInventoryChanged()
    {
        var quest = CurrentQuest;
        if (quest == null || quest.objectiveType != QuestObjectiveType.CollectItem) return;
        if (quest.targetItem == null || DataManager.instance == null) return;

        int have = 0;
        foreach (var item in DataManager.instance.sharedInventory.Items)
            if (item != null && item.data == quest.targetItem)
                have += item.stackCount;

        int newProgress = Mathf.Min(have, quest.TargetCount);
        if (newProgress != currentProgress)
        {
            currentProgress = newProgress;
            OnQuestProgressChanged?.Invoke();
        }

        if (currentProgress >= quest.TargetCount)
            CompleteCurrentQuest();
    }

    // ─────────────────────────────────────────────────────────────────
    // 완료 처리
    // ─────────────────────────────────────────────────────────────────

    private void CompleteCurrentQuest()
    {
        if (_completing) return;
        _completing = true;

        var quest = CurrentQuest;
        if (quest == null) { _completing = false; return; }

        if (!string.IsNullOrEmpty(quest.questId) && !completedQuestIds.Contains(quest.questId))
            completedQuestIds.Add(quest.questId);

        // 다음 퀘스트로 먼저 전환 — 아래 아이템 소모/보상 지급 중 발생하는 인벤토리 이벤트가
        // 방금 완료한 퀘스트를 다시 완료 처리하지 않도록 함
        currentQuestIndex++;
        currentProgress = 0;

        // 수집형 퀘스트는 완료 시 아이템 소모(반납)
        if (quest.objectiveType == QuestObjectiveType.CollectItem
            && quest.targetItem != null && DataManager.instance != null)
        {
            int remaining = quest.TargetCount;
            var inv = DataManager.instance.sharedInventory;
            var snapshot = new List<ItemInstance>(inv.Items);
            foreach (var item in snapshot)
            {
                if (remaining <= 0) break;
                if (item == null || item.data != quest.targetItem) continue;
                int consume = Mathf.Min(remaining, item.stackCount);
                inv.ConsumeItem(item, consume);
                remaining -= consume;
            }
        }

        // 보상 지급
        if (DataManager.instance != null)
        {
            if (quest.rewardExp > 0f) DataManager.instance.AddExp(quest.rewardExp);
            if (quest.rewardGold > 0) DataManager.instance.AddGold(quest.rewardGold);

            foreach (var rewardItem in quest.rewardItems)
            {
                if (rewardItem?.item == null) continue;
                DataManager.instance.sharedInventory.TryAddItem(new ItemInstance(rewardItem.item, rewardItem.count));
            }
        }

        _completing = false;
        OnQuestChanged?.Invoke();
    }
}

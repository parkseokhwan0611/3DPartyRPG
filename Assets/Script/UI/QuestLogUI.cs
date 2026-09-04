using System.Linq;
using System.Text;
using UnityEngine;
using TMPro;

// 인벤토리 창의 '퀘스트' 탭(MenuTabUI.questWindow)에 붙이는 스크립트.
// 현재 진행중인 퀘스트 상세 + 완료한 퀘스트 목록(회고용)을 보여준다.
public class QuestLogUI : MonoBehaviour
{
    [Header("# 현재 퀘스트")]
    public TextMeshProUGUI currentQuestNameText;
    public TextMeshProUGUI currentQuestDescText;
    public TextMeshProUGUI currentObjectiveText;

    [Header("# 완료한 퀘스트 목록 (회고용)")]
    [Tooltip("스크롤뷰 안에 넣는 텍스트 하나에 줄바꿈으로 이어붙여 표시")]
    public TextMeshProUGUI historyText;

    // OnEnable은 QuestManager.Awake()보다 먼저 실행될 수 있어(오브젝트 간 실행 순서 미보장 —
    // QuestHudUI에서와 동일한 이유로 완전히 안전하지 않음) 구독이 조용히 누락될 수 있음.
    // Awake는 항상 모든 Start보다 먼저 끝나는 것이 보장되므로 Start에서 한 번만 구독하고,
    // OnEnable은 탭이 열릴 때마다 최신 상태로 갱신하는 용도로만 사용
    void Start()
    {
        if (QuestManager.instance != null)
        {
            QuestManager.instance.OnQuestChanged        += Refresh;
            QuestManager.instance.OnQuestProgressChanged += Refresh;
        }
        Refresh();
    }

    void OnEnable()
    {
        Refresh();
    }

    void OnDestroy()
    {
        if (QuestManager.instance != null)
        {
            QuestManager.instance.OnQuestChanged        -= Refresh;
            QuestManager.instance.OnQuestProgressChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        RefreshCurrentQuest();
        RefreshHistory();
    }

    private void RefreshCurrentQuest()
    {
        var qm    = QuestManager.instance;
        var quest = qm != null ? qm.CurrentQuest : null;

        if (quest == null)
        {
            bool allDone = qm != null && qm.IsAllQuestsComplete;
            if (currentQuestNameText != null) currentQuestNameText.text = allDone ? "모든 퀘스트 완료" : "진행중인 퀘스트 없음";
            if (currentQuestDescText != null) currentQuestDescText.text = "";
            if (currentObjectiveText != null) currentObjectiveText.text = "";
            return;
        }

        if (currentQuestNameText != null) currentQuestNameText.text = quest.questName;
        if (currentQuestDescText != null) currentQuestDescText.text = quest.description;
        if (currentObjectiveText != null) currentObjectiveText.text = quest.GetObjectiveText(qm.CurrentProgress, qm.IsAwaitingReport);
    }

    private void RefreshHistory()
    {
        if (historyText == null) return;

        var qm = QuestManager.instance;
        if (qm == null || qm.Chain == null)
        {
            historyText.text = "";
            return;
        }

        var sb = new StringBuilder();
        foreach (var quest in qm.Chain.quests)
        {
            if (quest == null || string.IsNullOrEmpty(quest.questId)) continue;
            if (!qm.CompletedQuestIds.Contains(quest.questId)) continue;

            sb.AppendLine($"[완료] {quest.questName}");
        }

        historyText.text = sb.Length > 0 ? sb.ToString() : "아직 완료한 퀘스트가 없습니다.";
    }
}

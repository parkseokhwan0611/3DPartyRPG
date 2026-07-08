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

    void OnEnable()
    {
        if (QuestManager.instance != null)
        {
            QuestManager.instance.OnQuestChanged        += Refresh;
            QuestManager.instance.OnQuestProgressChanged += Refresh;
        }
        Refresh();
    }

    void OnDisable()
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
        if (currentObjectiveText != null) currentObjectiveText.text = quest.GetObjectiveText(qm.CurrentProgress);
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

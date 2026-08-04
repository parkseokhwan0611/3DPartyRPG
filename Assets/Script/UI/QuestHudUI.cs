using System.Collections;
using UnityEngine;
using TMPro;

// 화면 우측 상시 표시되는 퀘스트 추적 패널 — 퀘스트명 + 목표 설명(진행도 포함).
// NPC 대화형 퀘스트일 때만 대상 NPC 위치에 월드스페이스 마커를 띄운다.
public class QuestHudUI : MonoBehaviour
{
    [Header("# UI 참조")]
    public GameObject root;              // 패널 전체 (퀘스트 없을 때 숨김)
    public TextMeshProUGUI questNameText;
    public TextMeshProUGUI objectiveText;

    [Header("# 목표지점 마커 (NpcDialogue 타입 전용)")]
    [Tooltip("ObjectPoolManager에 등록한 마커 풀 키. PoolableObject(자동 반납형)가 아니라 " +
             "PoolAble만 붙은 프리팹이어야 함 — 퀘스트가 끝날 때까지 계속 떠있어야 하므로")]
    public string markerPoolKey = "QuestMarker";
    public float  markerHeightOffset = 2.2f;

    [Header("# 퀘스트 완료 연출")]
    [Tooltip("퀘스트 완료 문구를 보여줄 시간(초). 이 시간이 지나면 다음 퀘스트를 표시")]
    public float completionDisplayDuration = 3f;

    private GameObject _activeMarker;
    private Coroutine  _completionCoroutine;
    private bool        _showingCompletion;

    // OnEnable은 QuestManager.Awake()보다 먼저 실행될 수 있어(오브젝트 간 실행 순서 미보장 —
    // QuestManager의 [DefaultExecutionOrder(-200)]만으로는 이 상시 표시 패널에서 완전히 안전하지
    // 않았음) 구독이 조용히 누락될 수 있음. Awake는 항상 모든 Start보다 먼저 끝나는 것이
    // 보장되므로 Start에서 구독
    void Start()
    {
        if (QuestManager.instance != null)
        {
            QuestManager.instance.OnQuestChanged        += HandleQuestChanged;
            QuestManager.instance.OnQuestProgressChanged += Refresh;
            QuestManager.instance.OnQuestCompleted       += HandleQuestCompleted;
        }
        Refresh();
    }

    void OnDestroy()
    {
        if (QuestManager.instance != null)
        {
            QuestManager.instance.OnQuestChanged        -= HandleQuestChanged;
            QuestManager.instance.OnQuestProgressChanged -= Refresh;
            QuestManager.instance.OnQuestCompleted       -= HandleQuestCompleted;
        }

        if (_completionCoroutine != null)
        {
            StopCoroutine(_completionCoroutine);
            _completionCoroutine = null;
        }
        _showingCompletion = false;

        ReleaseMarker();
    }

    // 완료 연출 코루틴이 도중에 GameObject 비활성화로 강제 중단되면 _showingCompletion이
    // true로 영영 남아서(코루틴 끝의 리셋 코드가 실행되지 못함) 그 이후로 퀘스트가 바뀌어도
    // HandleQuestChanged가 계속 스킵돼 패널이 다시는 갱신/숨김되지 않는 버그가 있었음 —
    // 비활성화 시점에 확실히 정리하고, 다시 활성화될 때 최신 상태로 갱신
    void OnDisable()
    {
        if (_completionCoroutine != null)
        {
            StopCoroutine(_completionCoroutine);
            _completionCoroutine = null;
        }
        _showingCompletion = false;
    }

    void OnEnable()
    {
        Refresh();
    }

    // OnQuestChanged는 완료 시에도 함께 발생하는데, 그때는 완료 연출 코루틴이 갱신을 대신 처리하므로
    // 연출 중에는 즉시 갱신을 건너뛴다 (안 그러면 "완료" 문구가 뜨자마자 다음 퀘스트로 덮어써짐)
    private void HandleQuestChanged()
    {
        if (_showingCompletion) return;
        Refresh();
    }

    private void HandleQuestCompleted(QuestData completedQuest)
    {
        if (_completionCoroutine != null) StopCoroutine(_completionCoroutine);
        _completionCoroutine = StartCoroutine(ShowCompletionThenRefresh(completedQuest));
    }

    private IEnumerator ShowCompletionThenRefresh(QuestData completedQuest)
    {
        _showingCompletion = true;

        ReleaseMarker(); // 완료된 퀘스트의 목표지점 마커는 즉시 정리

        if (root != null) root.SetActive(true);
        if (questNameText != null) questNameText.text = completedQuest != null ? completedQuest.questName : "";
        if (objectiveText != null) objectiveText.text = "퀘스트 완료!";

        yield return new WaitForSeconds(completionDisplayDuration);

        _showingCompletion   = false;
        _completionCoroutine = null;
        Refresh();
    }

    private void Refresh()
    {
        var quest = QuestManager.instance != null ? QuestManager.instance.CurrentQuest : null;

        if (quest == null)
        {
            if (root != null) root.SetActive(false);
            ReleaseMarker();
            return;
        }

        if (root != null) root.SetActive(true);
        if (questNameText != null) questNameText.text = quest.questName;
        if (objectiveText != null) objectiveText.text = quest.GetObjectiveText(QuestManager.instance.CurrentProgress);

        UpdateMarker(quest);
    }

    private void UpdateMarker(QuestData quest)
    {
        if (quest.objectiveType != QuestObjectiveType.NpcDialogue)
        {
            ReleaseMarker();
            return;
        }

        NpcInteractable npc = null;
        foreach (var n in NpcInteractable.AllInstances)
        {
            if (n.NpcId == quest.targetNpcId) { npc = n; break; }
        }

        if (npc == null)
        {
            ReleaseMarker();
            return;
        }

        if (_activeMarker == null)
        {
            if (ObjectPoolManager.instance == null) return;
            _activeMarker = ObjectPoolManager.instance.GetGo(markerPoolKey);
            if (_activeMarker == null) return;
        }

        _activeMarker.transform.position = npc.transform.position + Vector3.up * markerHeightOffset;
    }

    private void ReleaseMarker()
    {
        if (_activeMarker == null) return;

        var poolAble = _activeMarker.GetComponent<PoolAble>();
        if (poolAble != null) poolAble.ReleaseObject();
        else                  Destroy(_activeMarker);

        _activeMarker = null;
    }
}

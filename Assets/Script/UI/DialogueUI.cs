using System;
using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 씬에 하나만 배치하는 NPC 대화 패널.
/// F 또는 Enter로 대사 진행 / 타이핑 스킵.
/// NPC·플레이어 발화자 구분으로 이름 텍스트를 자동 전환.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI instance;

    [Header("UI 참조")]
    [SerializeField] GameObject      panel;
    [SerializeField] TextMeshProUGUI speakerNameText;  // NPC 또는 플레이어 이름 표시
    [SerializeField] TextMeshProUGUI dialogueText;

    [Header("플레이어 이름")]
    [Tooltip("DialogueLine.Speaker.Player일 때 표시할 이름")]
    [SerializeField] string playerName = "주인공";

    [Header("대화 중 숨길 전투 UI")]
    [Tooltip("대화창이 열릴 때 비활성화할 전투 UI 루트 오브젝트들")]
    [SerializeField] GameObject[] combatUIObjects;

    [Header("타이프라이터")]
    [Tooltip("글자 1자당 출력 대기 시간 (초)")]
    [SerializeField] float charDelay = 0.03f;

    public bool IsOpen => panel != null && panel.activeSelf;

    private DialogueLine[] _lines;
    private int            _lineIndex;
    private Action         _onComplete;
    private string         _npcName;
    private bool           _isTyping;
    private Coroutine      _typeRoutine;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        panel?.SetActive(false);
    }

    void Update()
    {
        if (!IsOpen) return;
        if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return))
            Advance();
    }

    // ─────────────────────────────────────────────────────────────────
    // 공개 API
    // ─────────────────────────────────────────────────────────────────

    /// <param name="npcName">NPC 대사 줄에 표시할 이름</param>
    /// <param name="lines">DialogueLine 배열 (NPC·플레이어 혼합 가능)</param>
    /// <param name="onComplete">모든 대사 완료 후 콜백 (상점/강화 UI 열기 등)</param>
    public void Open(string npcName, DialogueLine[] lines, Action onComplete)
    {
        if (lines == null || lines.Length == 0) { onComplete?.Invoke(); return; }

        _npcName    = npcName;
        _lines      = lines;
        _lineIndex  = 0;
        _onComplete = onComplete;

        panel.SetActive(true);
        SetCombatUI(false);

        ShowLine(0);
    }

    public void Close()
    {
        StopTyping();
        panel?.SetActive(false);
        SetCombatUI(true);
    }

    // ─────────────────────────────────────────────────────────────────
    // 내부 로직
    // ─────────────────────────────────────────────────────────────────

    void Advance()
    {
        if (_isTyping)
        {
            StopTyping();
            dialogueText.maxVisibleCharacters = _lines[_lineIndex].text.Length;
            return;
        }

        _lineIndex++;
        if (_lineIndex < _lines.Length)
            ShowLine(_lineIndex);
        else
        {
            Close();
            _onComplete?.Invoke();
        }
    }

    void ShowLine(int index)
    {
        StopTyping();

        DialogueLine line = _lines[index];

        // 발화자 이름 전환
        if (speakerNameText != null)
            speakerNameText.text = line.speaker == DialogueLine.Speaker.Player
                ? playerName
                : _npcName;

        _typeRoutine = StartCoroutine(TypeLine(line.text));
    }

    IEnumerator TypeLine(string line)
    {
        _isTyping = true;
        dialogueText.text                 = line;
        dialogueText.maxVisibleCharacters = 0;

        for (int i = 1; i <= line.Length; i++)
        {
            dialogueText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(charDelay);
        }

        _isTyping = false;
    }

    void StopTyping()
    {
        if (_typeRoutine != null) { StopCoroutine(_typeRoutine); _typeRoutine = null; }
        _isTyping = false;
    }

    void SetCombatUI(bool active)
    {
        if (combatUIObjects == null) return;
        foreach (var obj in combatUIObjects)
            if (obj != null) obj.SetActive(active);
    }
}

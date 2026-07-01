using System;
using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 씬에 하나만 배치하는 NPC 대화 패널.
/// F 또는 Enter로 대사 진행 / 타이핑 스킵.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI instance;

    [Header("UI 참조")]
    [SerializeField] GameObject        panel;
    [SerializeField] TextMeshProUGUI   npcNameText;
    [SerializeField] TextMeshProUGUI   dialogueText;

    [Header("타이프라이터")]
    [Tooltip("글자 1자당 출력 대기 시간 (초)")]
    [SerializeField] float charDelay = 0.03f;

    public bool IsOpen => panel != null && panel.activeSelf;

    private string[]  _lines;
    private int       _lineIndex;
    private Action    _onComplete;
    private bool      _isTyping;
    private Coroutine _typeRoutine;

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

    /// <param name="npcName">대화창 상단에 표시할 NPC 이름</param>
    /// <param name="lines">대사 배열 (순서대로 출력)</param>
    /// <param name="onComplete">모든 대사 완료 후 실행할 콜백 (상점/강화 UI 열기 등)</param>
    public void Open(string npcName, string[] lines, Action onComplete)
    {
        if (lines == null || lines.Length == 0) { onComplete?.Invoke(); return; }

        _lines      = lines;
        _lineIndex  = 0;
        _onComplete = onComplete;

        if (npcNameText != null) npcNameText.text = npcName;
        panel.SetActive(true);

        ShowLine(0);
    }

    public void Close()
    {
        StopTyping();
        panel?.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────
    // 내부 로직
    // ─────────────────────────────────────────────────────────────────

    void Advance()
    {
        if (_isTyping)
        {
            // 타이핑 중이면 현재 줄 즉시 완성
            StopTyping();
            dialogueText.maxVisibleCharacters = _lines[_lineIndex].Length;
            return;
        }

        _lineIndex++;
        if (_lineIndex < _lines.Length)
        {
            ShowLine(_lineIndex);
        }
        else
        {
            Close();
            _onComplete?.Invoke();
        }
    }

    void ShowLine(int index)
    {
        StopTyping();
        _typeRoutine = StartCoroutine(TypeLine(_lines[index]));
    }

    IEnumerator TypeLine(string line)
    {
        _isTyping = true;
        dialogueText.text             = line;
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
}

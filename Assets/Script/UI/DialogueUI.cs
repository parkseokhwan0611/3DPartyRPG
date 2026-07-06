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

    [Header("캐릭터 기본 이름 (DataManager charName 미설정 시 사용)")]
    [SerializeField] string fallbackChar0 = "캐릭터1";
    [SerializeField] string fallbackChar1 = "캐릭터2";
    [SerializeField] string fallbackChar2 = "캐릭터3";

    [Header("대화 중 숨길 전투 UI")]
    [Tooltip("대화창이 열릴 때 비활성화할 전투 UI 루트 오브젝트들")]
    [SerializeField] GameObject[] combatUIObjects;

    [Header("타이프라이터")]
    [Tooltip("글자 1자당 출력 대기 시간 (초)")]
    [SerializeField] float charDelay = 0.03f;

    public static bool IsOpen { get; private set; }

    private DialogueLine[] _lines;
    private int            _lineIndex;
    private Action         _onComplete;
    private string         _npcName;
    private bool           _isTyping;
    private Coroutine      _typeRoutine;
    private bool           _justOpened;  // Open()된 프레임에서 F키 중복 처리 방지

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        panel?.SetActive(false);
    }

    void OnDestroy()
    {
        IsOpen = false;
    }

    void Update()
    {
        if (!IsOpen) return;
        if (_justOpened) { _justOpened = false; return; }
        if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
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

        IsOpen      = true;
        _justOpened = true;
        panel.SetActive(true);
        SetCombatUI(false);

        ShowLine(0);
    }

    public void Close()
    {
        IsOpen = false;
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
            speakerNameText.text = ResolveSpeakerName(line.speaker);

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

    string ResolveSpeakerName(DialogueLine.Speaker speaker)
    {
        if (speaker == DialogueLine.Speaker.NPC) return _npcName;

        int index = speaker switch
        {
            DialogueLine.Speaker.Character0 => 0,
            DialogueLine.Speaker.Character1 => 1,
            DialogueLine.Speaker.Character2 => 2,
            _                               => -1,
        };

        string fallback = index switch
        {
            0 => fallbackChar0,
            1 => fallbackChar1,
            2 => fallbackChar2,
            _ => "?",
        };

        if (index < 0 || DataManager.instance == null) return fallback;
        var statuses = DataManager.instance.partyStatuses;
        if (index >= statuses.Count) return fallback;

        string charName = statuses[index].charName;
        return string.IsNullOrEmpty(charName) ? fallback : charName;
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

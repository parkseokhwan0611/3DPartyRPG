using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TitleMenuUI : MonoBehaviour
{
    [Header("# 버튼")]
    [SerializeField] Button newGameButton;
    [SerializeField] Button loadGameButton;
    [SerializeField] Button exitButton;
    [SerializeField] Button settingsButton;
    [SerializeField] Button settingsBackButton;

    [Header("# 패널")]
    [Tooltip("New Game / Load Game / Exit 등 타이틀 메인 버튼들이 속한 루트 — 설정 창을 열면 숨김")]
    [SerializeField] GameObject titlePanel;
    [Tooltip("설정 창 루트 (SettingsUI가 붙어있는 오브젝트)")]
    [SerializeField] GameObject settingsPanel;

    [Header("# 씬")]
    [Tooltip("새 게임 시작 시 이동할 씬 — 세계관 프롤로그")]
    [SerializeField] string prologueSceneName = "PrologueScene";
    [Tooltip("이어하기 시 곧바로 이동할 씬 — 이미 프롤로그를 본 상태이므로 스테이지로 바로 진입")]
    [SerializeField] string gameSceneName = "Forest_1_1";

    private bool _isTransitioning = false;

    void Start()
    {
        newGameButton ?.onClick.AddListener(OnNewGameClicked);
        loadGameButton?.onClick.AddListener(OnLoadGameClicked);
        exitButton    ?.onClick.AddListener(OnExitClicked);
        settingsButton?.onClick.AddListener(OnSettingsClicked);
        settingsBackButton?.onClick.AddListener(OnSettingsBackClicked);

        settingsPanel?.SetActive(false);

        // BGM은 AudioManager의 Scene Bgm Map(씬 이름 기반 자동 재생)으로 관리

        // 세이브 파일 없으면 이어하기 비활성화
        if (loadGameButton != null)
            loadGameButton.interactable = SaveManager.instance != null
                                       && SaveManager.instance.HasSaveData;
    }

    private void OnSettingsClicked()
    {
        titlePanel   ?.SetActive(false);
        settingsPanel?.SetActive(true);

        // 클릭한 버튼(설정)이 EventSystem에 계속 "선택됨" 상태로 남아 밑줄(Selected 라인)이
        // 안 사라지는 걸 방지 — 패널 전환 시 선택 상태를 명시적으로 초기화
        EventSystem.current?.SetSelectedGameObject(null);
    }

    private void OnSettingsBackClicked()
    {
        settingsPanel?.SetActive(false);
        titlePanel   ?.SetActive(true);

        EventSystem.current?.SetSelectedGameObject(null);
    }

    private void SetButtonsInteractable(bool value)
    {
        if (newGameButton  != null) newGameButton.interactable  = value;
        if (loadGameButton != null) loadGameButton.interactable = value;
        if (exitButton     != null) exitButton.interactable     = value;
    }

    private void OnNewGameClicked()
    {
        if (_isTransitioning) return;
        _isTransitioning = true;
        SetButtonsInteractable(false);
        DataManager.instance?.InitData();
        QuestManager.instance?.StartNewGame();
        SceneLoader.Load(prologueSceneName);
    }

    private void OnLoadGameClicked()
    {
        if (_isTransitioning) return;
        if (SaveManager.instance == null) return;
        _isTransitioning = true;
        SetButtonsInteractable(false);
        SaveManager.instance.Load();
        SceneLoader.Load(gameSceneName);
    }

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

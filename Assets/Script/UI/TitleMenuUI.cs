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
    [Tooltip("새 게임 시작 시 이동할 씬 — 무기 선택 (그다음 프롤로그 → 게임)")]
    [SerializeField] string newGameSceneName = "WeaponSelect";
    [Tooltip("이어하기는 저장한 씬으로 돌아간다. 저장한 씬을 열 수 없을 때(빌드 목록에서 빠짐 등)만 이 씬으로 이동")]
    [SerializeField] string gameSceneName = "Dungeon_1_1";

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

    void Update()
    {
        // 설정 창이 열려 있을 때 ESC = 뒤로가기 버튼과 동일
        if (Input.GetKeyDown(KeyCode.Escape) && settingsPanel != null && settingsPanel.activeSelf)
            OnSettingsBackClicked();
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
        SceneLoader.Load(newGameSceneName);
    }

    private void OnLoadGameClicked()
    {
        if (_isTransitioning) return;
        if (SaveManager.instance == null) return;
        _isTransitioning = true;
        SetButtonsInteractable(false);

        // 저장한 씬으로 이동 (그 씬을 열 수 없을 때만 gameSceneName). 불러오기에 실패하면 타이틀에 그대로 남는다
        if (!SaveManager.instance.Load(gameSceneName))
        {
            _isTransitioning = false;
            SetButtonsInteractable(true);
            if (loadGameButton != null) loadGameButton.interactable = SaveManager.instance.HasSaveData;
            return;
        }
        SceneLoader.Load(SaveManager.instance.LoadedSceneName);
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

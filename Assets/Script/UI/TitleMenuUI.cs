using UnityEngine;
using UnityEngine.UI;

public class TitleMenuUI : MonoBehaviour
{
    [Header("# 버튼")]
    [SerializeField] Button newGameButton;
    [SerializeField] Button loadGameButton;
    [SerializeField] Button exitButton;

    [Header("# 씬")]
    [SerializeField] string gameSceneName = "RPGTest";

    private bool _isTransitioning = false;

    void Start()
    {
        newGameButton?.onClick.AddListener(OnNewGameClicked);
        loadGameButton?.onClick.AddListener(OnLoadGameClicked);
        exitButton    ?.onClick.AddListener(OnExitClicked);

        // BGM은 AudioManager의 Scene Bgm Map(씬 이름 기반 자동 재생)으로 관리

        // 세이브 파일 없으면 이어하기 비활성화
        if (loadGameButton != null)
            loadGameButton.interactable = SaveManager.instance != null
                                       && SaveManager.instance.HasSaveData;
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
        SceneLoader.Load(gameSceneName);
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

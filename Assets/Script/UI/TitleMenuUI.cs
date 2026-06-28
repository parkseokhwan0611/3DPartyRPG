using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnLoadGameClicked()
    {
        if (_isTransitioning) return;
        if (SaveManager.instance == null) return;
        _isTransitioning = true;
        SetButtonsInteractable(false);
        SaveManager.instance.Load();
        SceneManager.LoadScene(gameSceneName);
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

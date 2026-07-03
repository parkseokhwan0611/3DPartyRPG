using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingWindowUI : MonoBehaviour
{
    [Header("# 세이브")]
    [SerializeField] Button saveButton;
    [SerializeField] TextMeshProUGUI saveResultText;

    [Header("# 메뉴 이동")]
    [SerializeField] Button mainMenuButton;
    [SerializeField] Button exitButton;
    [Tooltip("메인 메뉴 씬 이름")]
    [SerializeField] string titleSceneName = "Title";

    private Coroutine _feedbackCoroutine;

    void Start()
    {
        saveButton    ?.onClick.AddListener(OnSaveClicked);
        mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
        exitButton    ?.onClick.AddListener(OnExitClicked);

        if (saveResultText != null)
            saveResultText.gameObject.SetActive(false);
    }

    private void OnSaveClicked()
    {
        if (SaveManager.instance == null) return;

        bool success = SaveManager.instance.Save();
        ShowFeedback(success ? "저장 완료!" : "보스 룸에서는 저장할 수 없습니다.");
    }

    private void OnMainMenuClicked()
    {
        Time.timeScale = 1f; // 게임 오버 등으로 멈춘 경우 복구
        SceneLoader.Load(titleSceneName);
    }

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowFeedback(string message)
    {
        if (saveResultText == null) return;

        saveResultText.text = message;
        saveResultText.gameObject.SetActive(true);

        if (_feedbackCoroutine != null)
            StopCoroutine(_feedbackCoroutine);
        _feedbackCoroutine = StartCoroutine(HideFeedback(2f));
    }

    private IEnumerator HideFeedback(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (saveResultText != null)
            saveResultText.gameObject.SetActive(false);
        _feedbackCoroutine = null;
    }
}

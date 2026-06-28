using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingWindowUI : MonoBehaviour
{
    [Header("# 세이브")]
    [SerializeField] Button saveButton;
    [SerializeField] TextMeshProUGUI saveResultText;

    private Coroutine _feedbackCoroutine;

    void Start()
    {
        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);

        if (saveResultText != null)
            saveResultText.gameObject.SetActive(false);
    }

    private void OnSaveClicked()
    {
        if (SaveManager.instance == null) return;

        bool success = SaveManager.instance.Save();
        ShowFeedback(success ? "저장 완료!" : "보스 룸에서는 저장할 수 없습니다.");
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

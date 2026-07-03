using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader instance;

    [Header("UI")]
    [SerializeField] GameObject      loadingPanel;
    [SerializeField] Slider          progressBar;
    [SerializeField] TextMeshProUGUI progressText;
    [SerializeField] TextMeshProUGUI tipText;

    [Tooltip("로딩 완료 후 씬 활성화 전 최소 표시 시간 (초)")]
    [SerializeField] float minDisplayTime = 0.5f;

    [Header("팁 메시지")]
    [TextArea(2, 4)]
    [SerializeField] string[] tips =
    {
        "Tip: 파티원을 A / S / D 키로 바꿔가며 전투하면 더 유리합니다.",
        "Tip: 포션은 1번(HP), 2번(MP) 키로 빠르게 사용할 수 있습니다.",
        "Tip: 상점에서 아이템을 구매할 때 재고가 소진되면 더 이상 살 수 없습니다.",
        "Tip: 보스 룸에서는 세이브할 수 없으니 입장 전에 저장해두세요.",
        "Tip: 장비 아이템은 인벤토리에서 캐릭터에게 직접 장착할 수 있습니다.",
        "Tip: 파티원이 쓰러지면 전투가 더 어려워집니다. HP 포션을 챙겨두세요.",
    };

    void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    public static void Load(string sceneName)
    {
        if (instance != null)
            instance.StartCoroutine(instance.LoadAsync(sceneName));
        else
            SceneManager.LoadScene(sceneName);
    }

    private IEnumerator LoadAsync(string sceneName)
    {
        Time.timeScale = 1f;
        if (loadingPanel != null) loadingPanel.SetActive(true);
        ShowRandomTip();
        SetProgress(0f);

        yield return null; // 패널 렌더링 1프레임 대기

        float startTime = Time.realtimeSinceStartup;
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            SetProgress(op.progress / 0.9f);
            yield return null;
        }

        // 최소 표시 시간 보장
        float elapsed = Time.realtimeSinceStartup - startTime;
        if (elapsed < minDisplayTime)
            yield return new WaitForSecondsRealtime(minDisplayTime - elapsed);

        SetProgress(1f);
        yield return new WaitForSecondsRealtime(0.2f);

        op.allowSceneActivation = true;
        yield return new WaitUntil(() => op.isDone);

        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    private void SetProgress(float value)
    {
        if (progressBar != null) progressBar.value = value;
        if (progressText != null) progressText.text = $"{(int)(value * 100)}%";
    }

    private void ShowRandomTip()
    {
        if (tipText == null || tips == null || tips.Length == 0) return;
        tipText.text = tips[Random.Range(0, tips.Length)];
    }
}

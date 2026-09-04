using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 검은 화면에 텍스트만 띄우는 세계관 프롤로그. 클릭/키 입력으로 페이지를 넘기고,
// 마지막 페이지 이후 nextSceneName 씬으로 넘어간다. UI는 전부 코드로 생성하므로
// 이 스크립트가 붙은 빈 오브젝트 하나만 씬에 있으면 된다.
public class PrologueController : MonoBehaviour
{
    [Header("스토리 텍스트 (페이지별로 한 줄씩)")]
    [TextArea(3, 10)]
    public string[] pages;

    [Header("다음 씬")]
    public string nextSceneName = "Forest_1_1";

    [Header("연출")]
    public float fadeDuration = 0.4f;
    public KeyCode[] advanceKeys = { KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter };

    [Header("폰트")]
    [Tooltip("TMP 기본 폰트(LiberationSans)는 한글이 없어 깨져 보임 — 한글이 포함된 TMP 폰트 에셋을 지정")]
    public TMP_FontAsset koreanFont;

    private TextMeshProUGUI _bodyText;
    private CanvasGroup _bodyGroup;
    private int _pageIndex = -1;
    private bool _isBusy;

    void Awake()
    {
        BuildUI();
    }

    void Start()
    {
        ShowPage(0);
    }

    void Update()
    {
        if (_isBusy) return;

        bool advance = Input.GetMouseButtonDown(0);
        if (!advance)
        {
            foreach (var key in advanceKeys)
            {
                if (Input.GetKeyDown(key)) { advance = true; break; }
            }
        }
        if (advance) Advance();
    }

    private void Advance()
    {
        if (pages == null || _pageIndex + 1 >= pages.Length)
            StartCoroutine(FinishRoutine());
        else
            StartCoroutine(ChangePageRoutine(_pageIndex + 1));
    }

    private void ShowPage(int index)
    {
        _pageIndex = index;
        string raw = (pages != null && index < pages.Length) ? pages[index] : "";
        _bodyText.text = WrapWordsNoBreak(raw);
        _bodyGroup.alpha = 1f;
    }

    // TMP는 한글을 음절 단위로 줄바꿈하므로, 공백으로 나눈 단어(어절) 단위로만
    // 줄바꿈되도록 각 단어를 <nobr>로 감싼다
    private static string WrapWordsNoBreak(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        string[] words = raw.Split(' ');
        for (int i = 0; i < words.Length; i++)
            words[i] = $"<nobr>{words[i]}</nobr>";
        return string.Join(" ", words);
    }

    private IEnumerator ChangePageRoutine(int nextIndex)
    {
        _isBusy = true;
        yield return Fade(_bodyGroup, 1f, 0f);
        ShowPage(nextIndex);
        yield return Fade(_bodyGroup, 0f, 1f);
        _isBusy = false;
    }

    private IEnumerator FinishRoutine()
    {
        _isBusy = true;
        yield return Fade(_bodyGroup, 1f, 0f);
        SceneManager.LoadScene(nextSceneName);
    }

    private IEnumerator Fade(CanvasGroup group, float from, float to)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        group.alpha = to;
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("PrologueCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var bgGo = new GameObject("Background", typeof(Image));
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgImage = bgGo.GetComponent<Image>();
        bgImage.color = Color.black;
        bgImage.raycastTarget = false;
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var textGo = new GameObject("BodyText", typeof(TextMeshProUGUI));
        textGo.transform.SetParent(canvasGo.transform, false);
        _bodyText = textGo.GetComponent<TextMeshProUGUI>();
        _bodyText.alignment = TextAlignmentOptions.Center;
        _bodyText.fontSize = 42;
        _bodyText.color = Color.white;
        _bodyText.fontStyle = FontStyles.Bold;
        _bodyText.enableWordWrapping = true;
        if (koreanFont != null) _bodyText.font = koreanFont;
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.15f, 0.3f);
        textRect.anchorMax = new Vector2(0.85f, 0.75f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _bodyGroup = textGo.AddComponent<CanvasGroup>();

        var hintGo = new GameObject("HintText", typeof(TextMeshProUGUI));
        hintGo.transform.SetParent(canvasGo.transform, false);
        var hintText = hintGo.GetComponent<TextMeshProUGUI>();
        hintText.text = "클릭 또는 스페이스바로 계속";
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.fontSize = 20;
        hintText.color = new Color(1, 1, 1, 0.5f);
        if (koreanFont != null) hintText.font = koreanFont;
        var hintRect = hintGo.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.3f, 0.05f);
        hintRect.anchorMax = new Vector2(0.7f, 0.1f);
        hintRect.offsetMin = Vector2.zero;
        hintRect.offsetMax = Vector2.zero;
    }
}

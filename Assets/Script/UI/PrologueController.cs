using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 검은 화면에 텍스트만 띄우는 세계관 프롤로그. 클릭/키 입력으로 페이지를 넘기고,
// 마지막 페이지 이후 nextSceneName 씬으로 넘어간다. UI는 전부 코드로 생성하므로
// 이 스크립트가 붙은 빈 오브젝트 하나만 씬에 있으면 된다.
public class PrologueController : MonoBehaviour
{
    [Header("스토리 텍스트 (페이지별로 한 줄씩)")]
    [TextArea(3, 10)]
    public string[] pages;

    [Header("다음 씬")]
    public string nextSceneName = "Dungeon_Tutorial";

    [Header("연출")]
    public float fadeDuration = 0.4f;
    public KeyCode[] advanceKeys = { KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter };

    [Header("폰트")]
    [Tooltip("TMP 기본 폰트(LiberationSans)는 한글이 없어 깨져 보임 — 한글이 포함된 TMP 폰트 에셋을 지정")]
    public TMP_FontAsset koreanFont;

    [Header("스킵 버튼 (던전 씬 대화 스킵 버튼과 같은 모양·위치)")]
    [Tooltip("비워두면 스킵 버튼을 만들지 않음")]
    public Sprite skipButtonSprite;
    public Vector2 skipButtonSize = new Vector2(69f, 44f);
    public Vector2 skipButtonPosition = new Vector2(-60f, -60f); // 우상단 기준

    private TextMeshProUGUI _bodyText;
    private CanvasGroup _bodyGroup;
    private RectTransform _skipRect;
    private int _pageIndex = -1;
    private bool _isBusy;
    private bool _isFinishing;

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

        // 스킵 버튼 위 클릭은 페이지 넘김으로 처리하지 않음 (버튼 onClick이 처리)
        bool advance = Input.GetMouseButtonDown(0) && !IsPointerOverSkipButton();
        if (!advance)
        {
            foreach (var key in advanceKeys)
            {
                if (Input.GetKeyDown(key)) { advance = true; break; }
            }
        }
        if (advance) Advance();
    }

    private bool IsPointerOverSkipButton()
        => _skipRect != null && RectTransformUtility.RectangleContainsScreenPoint(_skipRect, Input.mousePosition, null);

    // 남은 페이지를 건너뛰고 바로 다음 씬으로 — 페이지 전환 페이드 중에 눌러도 동작
    public void Skip()
    {
        if (_isFinishing) return;
        StopAllCoroutines();
        StartCoroutine(FinishRoutine());
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
        _isFinishing = true;
        if (_skipRect != null) _skipRect.gameObject.SetActive(false);
        yield return Fade(_bodyGroup, _bodyGroup.alpha, 0f);
        // 다른 씬 이동과 같이 로딩 화면을 거친다 — 동기 로딩은 무거운 튜토리얼 씬을 읽는 동안 화면이 멈춤
        SceneLoader.Load(nextSceneName);
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
        // GraphicRaycaster가 없으면 캔버스 위 버튼이 클릭을 못 받음
        var canvasGo = new GameObject("PrologueCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
        _bodyText.raycastTarget = false;
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
        hintText.raycastTarget = false;
        if (koreanFont != null) hintText.font = koreanFont;
        var hintRect = hintGo.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.3f, 0.05f);
        hintRect.anchorMax = new Vector2(0.7f, 0.1f);
        hintRect.offsetMin = Vector2.zero;
        hintRect.offsetMax = Vector2.zero;

        BuildSkipButton(canvasGo.transform);
    }

    private void BuildSkipButton(Transform canvas)
    {
        if (skipButtonSprite == null) return;

        // 프롤로그 씬엔 EventSystem이 없어 버튼 클릭이 안 먹으므로 없을 때만 만든다
        if (FindObjectOfType<EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            esGo.transform.SetParent(transform, false);
        }

        var skipGo = new GameObject("SkipButton", typeof(Image), typeof(Button));
        skipGo.transform.SetParent(canvas, false);
        skipGo.GetComponent<Image>().sprite = skipButtonSprite;
        var button = skipGo.GetComponent<Button>();
        var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav; // Space/Enter로 눌리지 않게
        button.onClick.AddListener(Skip);

        _skipRect = skipGo.GetComponent<RectTransform>();
        _skipRect.anchorMin = Vector2.one;
        _skipRect.anchorMax = Vector2.one;
        _skipRect.sizeDelta = skipButtonSize;
        _skipRect.anchoredPosition = skipButtonPosition;
    }
}

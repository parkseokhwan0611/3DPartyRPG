using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 튜토리얼 씬 전용 — 플레이어가 처음 우클릭 이동을 하기 전까지 화면에 조작 안내 문구를 띄운다.
// PartyManager의 이동 처리와는 별개로 우클릭 입력만 감지하므로 기존 이동 로직에 영향 없음.
public class TutorialMoveHint : MonoBehaviour
{
    [Header("안내 문구")]
    public string message = "마우스 오른쪽 클릭으로 이동하세요";

    [Header("폰트")]
    [Tooltip("TMP 기본 폰트는 한글이 없어 깨져 보임 — 한글이 포함된 TMP 폰트 에셋을 지정")]
    public TMP_FontAsset koreanFont;

    private TextMeshProUGUI _hintText;

    void Awake()
    {
        BuildUI();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            gameObject.SetActive(false);
        }
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("TutorialHintCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var textGo = new GameObject("HintText", typeof(TextMeshProUGUI));
        textGo.transform.SetParent(canvasGo.transform, false);
        _hintText = textGo.GetComponent<TextMeshProUGUI>();
        _hintText.text = message;
        _hintText.alignment = TextAlignmentOptions.Center;
        _hintText.fontSize = 36;
        _hintText.color = Color.white;
        _hintText.fontStyle = FontStyles.Bold;
        _hintText.enableWordWrapping = true;
        if (koreanFont != null) _hintText.font = koreanFont;

        var rect = textGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.2f, 0.85f);
        rect.anchorMax = new Vector2(0.8f, 0.95f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}

using System.Collections.Generic;
using Coffee.UIExtensions;
using UnityEngine;
using UnityEngine.EventSystems;

// UI(인벤토리·설정 창·버튼 등)를 좌클릭하면 마우스 위치에 파티클 이펙트를 띄운다.
// CoreSystem에 붙어 있어 모든 씬에서 동작한다.
// 일반 ParticleSystem은 Screen Space - Overlay 캔버스보다 항상 뒤에 그려지므로, UIParticle(com.coffee.ui-particle)로
// 감싸 UI 요소처럼 그린다. 이펙트 전용 Overlay 캔버스를 런타임에 만들어 다른 모든 UI보다 위에 둔다.
public class UIClickEffect : MonoBehaviour
{
    [Tooltip("클릭 위치에 띄울 파티클 이펙트 프리팹 (일반 ParticleSystem 프리팹 그대로 넣으면 됨)")]
    public GameObject effectPrefab;
    [Tooltip("이펙트 크기 — 파티클 1 유닛을 몇 픽셀로 그릴지 (1920x1080 기준)")]
    public float effectScale = 10f;
    [Tooltip("클릭 지점 기준 이펙트 위치 보정 (1920x1080 기준 픽셀, x 오른쪽 / y 위).\n" +
             "이펙트가 커서 끝과 어긋나 보이면 먼저 Player Settings의 Cursor Hotspot이 커서 이미지의 " +
             "화살표 끝을 가리키는지 확인 — 핫스팟이 틀리면 이펙트뿐 아니라 실제 클릭 지점도 어긋남")]
    public Vector2 positionOffset = Vector2.zero;
    [Tooltip("이펙트 캔버스의 Sort Order — 다른 모든 UI보다 위에 그리도록 큰 값")]
    public int sortingOrder = 30000;
    [Tooltip("동시에 띄울 수 있는 최대 개수 — 연타하면 가장 오래된 것부터 재사용")]
    [Min(1)] public int poolSize = 6;
    [Tooltip("체크하면 UI가 아닌 곳(게임 화면)을 클릭해도 이펙트를 띄움")]
    public bool includeNonUI = false;

    private RectTransform _canvasRect;
    private readonly List<(RectTransform rect, UIParticle particle)> _pool = new List<(RectTransform, UIParticle)>();
    private int _next;

    void Start()
    {
        if (effectPrefab == null) return;
        BuildCanvas();
    }

    void Update()
    {
        if (_canvasRect == null || !Input.GetMouseButtonDown(0)) return;
        if (!includeNonUI && !IsPointerOverUI()) return;

        Spawn(Input.mousePosition);
    }

    private static bool IsPointerOverUI()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    // 이 오브젝트(CoreSystem, DontDestroyOnLoad)의 자식으로 만들어 씬이 바뀌어도 유지
    private void BuildCanvas()
    {
        var go = new GameObject("UIClickEffectCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        go.transform.SetParent(transform, false);
        go.layer = LayerMask.NameToLayer("UI");

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        // 게임 UI 캔버스와 같은 기준 해상도 — 해상도가 바뀌어도 이펙트가 UI와 같은 비율로 커지고 작아짐
        var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        // GraphicRaycaster를 붙이지 않음 — 이펙트가 클릭을 가로채면 안 됨
        _canvasRect = (RectTransform)go.transform;
    }

    private (RectTransform rect, UIParticle particle) CreateEntry()
    {
        var particle = UIParticleUtil.Create(effectPrefab, _canvasRect, effectScale, "ClickFX");
        return ((RectTransform)particle.transform, particle);
    }

    private void Spawn(Vector2 screenPos)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, null, out var local))
            return;

        if (_pool.Count < poolSize) _pool.Add(CreateEntry());
        var entry = _pool[_next % _pool.Count];
        _next = (_next + 1) % poolSize;

        entry.rect.anchoredPosition = local + positionOffset;
        UIParticleUtil.Replay(entry.particle); // 맨 위로 올려 가장 최근 클릭이 위에 보이도록
    }
}

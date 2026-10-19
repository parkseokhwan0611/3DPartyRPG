using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// 카메라와 살아 있는 파티원 사이를 가리는 벽·장애물을 반투명하게 만들어 캐릭터가 보이게 한다.
// 씬에 따로 배치할 필요 없음 — Resources/OcclusionFadeLibrary가 있으면 게임 시작 시 스스로 생성되어 씬 전환에도 유지된다.
//
// 방식: 가리는 렌더러의 머티리얼을 같은 텍스처의 반투명 버전으로 바꿔 끼우고, _BaseColor 알파를
// MaterialPropertyBlock으로 서서히 내린다. 더 이상 가리지 않으면 알파를 되돌린 뒤 원래 머티리얼로 복구.
// (Synty Generic_Basic 셰이더는 Alpha = BaseColor.a × 텍스처 알파)
public class OcclusionFader : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private OcclusionFadeLibrary _lib;
    private readonly Dictionary<Material, Material> _fadeOf = new Dictionary<Material, Material>();
    private readonly Dictionary<Renderer, FadeState> _states = new Dictionary<Renderer, FadeState>();
    private readonly Dictionary<Collider, Renderer[]> _renderersOf = new Dictionary<Collider, Renderer[]>();
    private readonly HashSet<Renderer> _occluding = new HashSet<Renderer>();
    private readonly List<Renderer> _toRemove = new List<Renderer>();
    private readonly RaycastHit[] _hits = new RaycastHit[32];
    private readonly Collider[]   _overlaps = new Collider[16];
    private MaterialPropertyBlock _mpb;
    private float _nextCheck;

    private class FadeState
    {
        public Material[] originals;
        public Material[] fades;
        public Color[]    baseColors;
        public float      alpha = 1f;
        public bool       swapped;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var lib = Resources.Load<OcclusionFadeLibrary>("OcclusionFadeLibrary");
        if (lib == null || !lib.fadeEnabled) return;
        var go = new GameObject("[OcclusionFader]");
        DontDestroyOnLoad(go);
        go.AddComponent<OcclusionFader>()._lib = lib;
    }

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void Start()
    {
        foreach (var p in _lib.pairs)
            if (p.original != null && p.transparent != null) _fadeOf[p.original] = p.transparent;
    }

    void OnDestroy() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    // 이전 씬 렌더러는 이미 파괴됐으므로 상태만 비운다
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _states.Clear();
        _renderersOf.Clear();
        _occluding.Clear();
    }

    void LateUpdate()
    {
        if (Time.unscaledTime >= _nextCheck)
        {
            _nextCheck = Time.unscaledTime + _lib.checkInterval;
            FindOccluders();
        }
        UpdateFades();
    }

    // ─────────────────────────────────────────────────────────────────
    // 가림 판정 — 카메라에서 살아 있는 파티원마다 굵은 레이를 쏴서 걸리는 렌더러를 모은다
    // ─────────────────────────────────────────────────────────────────

    private void FindOccluders()
    {
        _occluding.Clear();
        var cam = Camera.main;
        var pm  = PartyManager.instance;
        if (cam == null || pm == null) return;

        Vector3 origin = cam.transform.position;

        // SphereCast는 시작 지점에서 이미 겹쳐 있는 콜라이더를 잡지 못한다 — 카메라가 벽·천장 두께 안에 들어간 경우를 따로 잡음
        int overlapCount = Physics.OverlapSphereNonAlloc(origin, _lib.castRadius, _overlaps, _lib.occluderMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlapCount; i++)
            foreach (var r in RenderersOf(_overlaps[i]))
                _occluding.Add(r);

        foreach (var member in pm.partyMembers)
        {
            if (member == null || !member.gameObject.activeInHierarchy) continue;
            if (member.CurrentState == PartyMemberScript.MemberState.Dead) continue;

            Vector3 target = member.transform.position + Vector3.up * _lib.targetHeight;
            Vector3 dir    = target - origin;
            float   dist   = dir.magnitude - _lib.stopShort;
            if (dist <= 0f) continue;

            int count = Physics.SphereCastNonAlloc(origin, _lib.castRadius, dir / dir.magnitude, _hits, dist,
                                                   _lib.occluderMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                foreach (var r in RenderersOf(_hits[i].collider))
                    _occluding.Add(r);
        }

        foreach (var r in _occluding)
            if (!_states.ContainsKey(r)) TryCreateState(r);
    }

    // 콜라이더와 같은 오브젝트(없으면 자식, 그래도 없으면 부모)의 메시 렌더러
    private Renderer[] RenderersOf(Collider col)
    {
        if (_renderersOf.TryGetValue(col, out var cached)) return cached;
        Renderer[] rs = col.GetComponents<Renderer>();
        if (rs.Length == 0) rs = col.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0)
        {
            var parent = col.GetComponentInParent<Renderer>();
            rs = parent != null ? new[] { parent } : new Renderer[0];
        }
        var list = new List<Renderer>();
        foreach (var r in rs) if (r is MeshRenderer || r is SkinnedMeshRenderer) list.Add(r);
        rs = list.ToArray();
        _renderersOf[col] = rs;
        return rs;
    }

    private void TryCreateState(Renderer r)
    {
        Material[] originals = r.sharedMaterials;
        var fades  = new Material[originals.Length];
        var colors = new Color[originals.Length];
        bool any = false;
        for (int i = 0; i < originals.Length; i++)
        {
            Material fade = GetFadeMaterial(originals[i]);
            fades[i]  = fade != null ? fade : originals[i];
            colors[i] = fade != null && fade.HasProperty(BaseColorId) ? fade.GetColor(BaseColorId) : Color.white;
            any |= fade != null;
        }
        if (!any) return; // 반투명을 지원하지 않는 셰이더뿐 — 건드리지 않음
        _states[r] = new FadeState { originals = originals, fades = fades, baseColors = colors };
    }

    private Material GetFadeMaterial(Material src)
    {
        if (src == null) return null;
        if (_fadeOf.TryGetValue(src, out var fade)) return fade;
        // 목록에 없는 머티리얼 — 셰이더가 지원하면 실행 중에 만들어 쓴다 (에디터에선 문제없지만,
        // 빌드에선 반투명 셰이더 변형이 빠질 수 있으니 Tools → Occlusion으로 목록을 갱신해 둘 것)
        fade = SupportsFade(src) ? CreateFadeMaterial(src) : null;
        _fadeOf[src] = fade;
        return fade;
    }

    // ─────────────────────────────────────────────────────────────────
    // 페이드
    // ─────────────────────────────────────────────────────────────────

    private void UpdateFades()
    {
        if (_states.Count == 0) return;
        float step = _lib.fadeDuration > 0f ? Time.unscaledDeltaTime / _lib.fadeDuration * (1f - _lib.fadedAlpha) : 1f;

        _toRemove.Clear();
        foreach (var kv in _states)
        {
            Renderer r = kv.Key;
            FadeState s = kv.Value;
            if (r == null) { _toRemove.Add(r); continue; }

            float target = _occluding.Contains(r) ? _lib.fadedAlpha : 1f;
            if (Mathf.Approximately(s.alpha, target) && (s.swapped || target >= 1f))
            {
                if (target >= 1f) { Restore(r, s); _toRemove.Add(r); }
                continue;
            }

            s.alpha = Mathf.MoveTowards(s.alpha, target, step);
            if (!s.swapped)
            {
                r.sharedMaterials = s.fades;
                s.swapped = true;
            }
            for (int i = 0; i < s.fades.Length; i++)
            {
                if (s.fades[i] == s.originals[i]) continue;
                Color c = s.baseColors[i];
                c.a *= s.alpha;
                r.GetPropertyBlock(_mpb, i);
                _mpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(_mpb, i);
            }
        }
        foreach (var r in _toRemove) _states.Remove(r);
    }

    private void Restore(Renderer r, FadeState s)
    {
        if (!s.swapped) return;
        r.sharedMaterials = s.originals;
        for (int i = 0; i < s.originals.Length; i++) r.SetPropertyBlock(null, i);
        s.swapped = false;
    }

    // ─────────────────────────────────────────────────────────────────
    // 반투명 머티리얼 만들기 — 에디터 툴(Tools → Occlusion)에서도 같은 설정을 쓴다
    // URP 셰이더 그래프의 "Allow Material Override"가 켜져 있어야 Surface를 바꿀 수 있음 (Synty Generic_Basic은 켜져 있음)
    // ─────────────────────────────────────────────────────────────────

    public static bool SupportsFade(Material m) =>
        m != null && m.HasProperty("_Surface") && m.HasProperty(BaseColorId);

    public static Material CreateFadeMaterial(Material src)
    {
        var m = new Material(src) { name = src.name + "_Fade" };
        SetupTransparent(m);
        return m;
    }

    public static void SetupTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);   // Transparent
        m.SetFloat("_Blend", 0f);     // Alpha
        if (m.HasProperty("_AlphaClip"))   m.SetFloat("_AlphaClip", 0f);
        if (m.HasProperty("_AlphaToMask")) m.SetFloat("_AlphaToMask", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.DisableKeyword("_ALPHAMODULATE_ON");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        // 깊이를 쓰지 않으니 깊이 전용 패스도 꺼서 반투명 벽이 뒤쪽(캐릭터)의 깊이를 가리지 않게
        m.SetShaderPassEnabled("DepthOnly", false);
        m.SetShaderPassEnabled("DepthNormals", false);
    }
}

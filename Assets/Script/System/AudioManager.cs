using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 이름(문자열 키) + AudioClip 한 쌍. 인스펙터 리스트에 등록해서 사용.
[Serializable]
public class SoundEntry
{
    public string name;
    public AudioClip clip;
    [Tooltip("이 사운드에만 적용되는 개별 보정값. 최종 재생 볼륨 = SFX 볼륨 × 이 값 " +
             "(1 = 그대로, 0.5 = 절반 크기로 재생)")]
    [Range(0f, 2f)]
    public float volumeMultiplier = 1f;
}

// 씬 이름 → 그 씬이 속한 구역의 BGM 키. 같은 구역의 여러 스테이지(1-1, 1-2 등)에
// 같은 bgmKey를 넣어두면, 스테이지 이동 시 PlayBGM의 "이미 재생 중이면 무시" 로직 덕분에
// BGM이 끊기지 않고 이어짐.
[Serializable]
public class SceneBgmEntry
{
    public string sceneName;
    public string bgmKey;
}

// 배경음악(BGM) + 효과음(SFX) 재생을 전담하는 싱글톤 매니저.
// 재생할 사운드는 인스펙터에 문자열 키로 등록해두고, PlayBGM(key)/PlaySFX(key)로 재생한다.
public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Header("BGM")]
    [SerializeField] private List<SoundEntry> bgmClips = new List<SoundEntry>();
    [SerializeField] [Range(0f, 1f)] private float bgmVolume = 0.5f;
    [Tooltip("BGM 전환 시 페이드 아웃/인 시간 (초)")]
    [SerializeField] private float bgmFadeDuration = 1f;

    [Header("SFX")]
    [SerializeField] private List<SoundEntry> sfxClips = new List<SoundEntry>();
    [SerializeField] [Range(0f, 1f)] private float sfxVolume = 1f;
    [Tooltip("동시에 겹쳐 재생 가능한 2D SFX 채널 수")]
    [SerializeField] private int sfxSourceCount = 8;

    [Header("구역별 BGM (씬 이름 기준)")]
    [SerializeField] private List<SceneBgmEntry> sceneBgmMap = new List<SceneBgmEntry>();

    [Header("전역 UI 클릭음")]
    [Tooltip("Button/Toggle 등 클릭 가능한 UI 요소를 전역으로 감지해 자동으로 재생. " +
             "개별 버튼에 별도 컴포넌트를 붙일 필요 없음")]
    [SerializeField] private bool   enableGlobalUiClick = true;
    [SerializeField] private string uiClickSfxKey = "UIClick";

    private AudioSource bgmSource;
    private AudioSource[] sfxSources;
    private int sfxIndex;

    private readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>();
    private PointerEventData _uiPointerEventData;

    private Dictionary<string, AudioClip> bgmDict;
    private Dictionary<string, SoundEntry> sfxDict;

    private string currentBgmKey;
    private Coroutine fadeCoroutine;

    // ─────────────────────────────────────────────────────────────────
    // 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        bgmDict = BuildDict(bgmClips);
        sfxDict = BuildSfxDict(sfxClips);

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop         = true;
        bgmSource.playOnAwake  = false;
        bgmSource.spatialBlend = 0f;
        bgmSource.volume       = bgmVolume;

        sfxSources = new AudioSource[Mathf.Max(1, sfxSourceCount)];
        for (int i = 0; i < sfxSources.Length; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake  = false;
            src.spatialBlend = 0f; // 방향성 없는 2D 사운드 전용 채널
            sfxSources[i] = src;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void Update()
    {
        if (!enableGlobalUiClick) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current == null) return;

        if (_uiPointerEventData == null) _uiPointerEventData = new PointerEventData(EventSystem.current);
        _uiPointerEventData.position = Input.mousePosition;
        _uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(_uiPointerEventData, _uiRaycastResults);

        foreach (var result in _uiRaycastResults)
        {
            // Button/Toggle/Dropdown 등 표준 UI 요소(Selectable) + DragItemBase처럼
            // IPointerClickHandler를 직접 구현한 커스텀 클릭 대상까지 포괄
            if (result.gameObject.GetComponentInParent<Selectable>() != null ||
                result.gameObject.GetComponentInParent<IPointerClickHandler>() != null)
            {
                PlaySFX(uiClickSfxKey);
                return;
            }
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var entry in sceneBgmMap)
        {
            if (entry == null || entry.sceneName != scene.name) continue;
            PlayBGM(entry.bgmKey);
            return;
        }
    }

    private static Dictionary<string, AudioClip> BuildDict(List<SoundEntry> entries)
    {
        var dict = new Dictionary<string, AudioClip>();
        foreach (var e in entries)
            if (e != null && !string.IsNullOrEmpty(e.name) && e.clip != null)
                dict[e.name] = e.clip;
        return dict;
    }

    private static Dictionary<string, SoundEntry> BuildSfxDict(List<SoundEntry> entries)
    {
        var dict = new Dictionary<string, SoundEntry>();
        foreach (var e in entries)
            if (e != null && !string.IsNullOrEmpty(e.name) && e.clip != null)
                dict[e.name] = e;
        return dict;
    }

    // ─────────────────────────────────────────────────────────────────
    // BGM
    // ─────────────────────────────────────────────────────────────────

    public void PlayBGM(string key, bool fade = true)
    {
        if (currentBgmKey == key && bgmSource.isPlaying) return;
        if (!bgmDict.TryGetValue(key, out var clip))
        {
            Debug.LogWarning($"[AudioManager] BGM 키 '{key}'를 찾을 수 없습니다.");
            return;
        }

        currentBgmKey = key;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

        if (fade && bgmSource.isPlaying)
            fadeCoroutine = StartCoroutine(FadeToNewBgm(clip));
        else
        {
            bgmSource.clip   = clip;
            bgmSource.volume = bgmVolume;
            bgmSource.Play();
        }
    }

    public void StopBGM(bool fade = true)
    {
        currentBgmKey = null;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

        if (fade)
            fadeCoroutine = StartCoroutine(FadeOutAndStop());
        else
            bgmSource.Stop();
    }

    private IEnumerator FadeToNewBgm(AudioClip newClip)
    {
        yield return FadeVolume(bgmSource.volume, 0f);
        bgmSource.clip = newClip;
        bgmSource.Play();
        yield return FadeVolume(0f, bgmVolume);
    }

    private IEnumerator FadeOutAndStop()
    {
        yield return FadeVolume(bgmSource.volume, 0f);
        bgmSource.Stop();
        bgmSource.volume = bgmVolume;
    }

    private IEnumerator FadeVolume(float from, float to)
    {
        float t = 0f;
        while (t < bgmFadeDuration)
        {
            t += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(from, to, t / bgmFadeDuration);
            yield return null;
        }
        bgmSource.volume = to;
    }

    // ─────────────────────────────────────────────────────────────────
    // SFX
    // ─────────────────────────────────────────────────────────────────

    /// <summary>방향성 없는 2D 효과음 (UI 클릭, 레벨업, 퀘스트 완료 등)</summary>
    public void PlaySFX(string key)
    {
        if (!sfxDict.TryGetValue(key, out var entry))
        {
            Debug.LogWarning($"[AudioManager] SFX 키 '{key}'를 찾을 수 없습니다.");
            return;
        }
        var src = sfxSources[sfxIndex];
        sfxIndex = (sfxIndex + 1) % sfxSources.Length;
        src.PlayOneShot(entry.clip, sfxVolume * entry.volumeMultiplier);
    }

    /// <summary>월드 위치가 있는 3D 효과음 (타격음, 몬스터 사망음 등)</summary>
    public void PlaySFXAtPosition(string key, Vector3 position)
    {
        if (!sfxDict.TryGetValue(key, out var entry))
        {
            Debug.LogWarning($"[AudioManager] SFX 키 '{key}'를 찾을 수 없습니다.");
            return;
        }
        AudioSource.PlayClipAtPoint(entry.clip, position, sfxVolume * entry.volumeMultiplier);
    }

    // ─────────────────────────────────────────────────────────────────
    // 볼륨 (설정 UI 연동용)
    // ─────────────────────────────────────────────────────────────────

    public float BgmVolume => bgmVolume;
    public float SfxVolume => sfxVolume;

    public void SetBgmVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (bgmSource != null) bgmSource.volume = bgmVolume;
    }

    public void SetSfxVolume(float volume) => sfxVolume = Mathf.Clamp01(volume);
}

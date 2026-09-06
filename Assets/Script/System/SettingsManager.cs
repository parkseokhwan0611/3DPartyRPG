using System.Collections.Generic;
using UnityEngine;

// 그래픽/볼륨 등 "환경설정"을 PlayerPrefs에 저장 — 세이브 파일(GameSaveData)과는 별개로,
// 캐릭터 진행 상황과 무관하게 기기/플레이어 단위로 유지되는 값. 이 오브젝트가 처음 생성될 때
// (타이틀 씬 등에 배치) 저장된 값을 즉시 적용하고 씬 전환에도 유지된다.
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager instance;

    private const string KeyBgmVolume  = "Settings_BgmVolume";
    private const string KeySfxVolume  = "Settings_SfxVolume";
    private const string KeyQuality    = "Settings_QualityLevel";
    private const string KeyResWidth   = "Settings_ResolutionWidth";
    private const string KeyResHeight  = "Settings_ResolutionHeight";
    private const string KeyFullscreen = "Settings_Fullscreen";

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // AudioManager.instance가 자신의 Awake()에서 할당되는데, 서로 다른 루트 오브젝트라
    // Awake() 호출 순서가 보장되지 않는다. 모든 Awake()가 끝난 뒤 실행되는 Start()에서
    // 적용하면 AudioManager.instance가 이미 준비된 상태임이 보장됨
    void Start()
    {
        ApplySavedSettings();
    }

    private void ApplySavedSettings()
    {
        AudioManager.instance?.SetBgmVolume(GetBgmVolume());
        AudioManager.instance?.SetSfxVolume(GetSfxVolume());

        int quality = Mathf.Clamp(GetQuality(), 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(quality, true);

        bool fullscreen = GetFullscreen();
        int width  = PlayerPrefs.GetInt(KeyResWidth, -1);
        int height = PlayerPrefs.GetInt(KeyResHeight, -1);
        if (width > 0 && height > 0)
        {
            // Screen.resolutions의 인덱스가 아니라 실제 가로/세로 값을 직접 저장해둔 것을 그대로
            // 적용 — 모니터 구성이 바뀌면 Screen.resolutions의 순서/구성 자체가 달라질 수 있어
            // 인덱스로 저장하면 전혀 다른 해상도를 가리키게 될 수 있음
            Screen.SetResolution(width, height, fullscreen);
            return;
        }
        Screen.fullScreen = fullscreen;
    }

    // 같은 해상도가 여러 주사율로 중복 등록되는 것을 걸러낸 목록 — SettingsUI 드롭다운과 공용
    public static List<Resolution> GetDistinctResolutions()
    {
        var seen = new HashSet<string>();
        var list = new List<Resolution>();
        foreach (var r in Screen.resolutions)
        {
            string key = $"{r.width}x{r.height}";
            if (seen.Add(key)) list.Add(r);
        }
        return list;
    }

    // ─────────────────────────────────────────────────────────────────
    // 저장 / 조회
    // ─────────────────────────────────────────────────────────────────

    public void SaveBgmVolume(float v) => PlayerPrefs.SetFloat(KeyBgmVolume, v);
    public void SaveSfxVolume(float v) => PlayerPrefs.SetFloat(KeySfxVolume, v);
    public void SaveQuality(int level) => PlayerPrefs.SetInt(KeyQuality, level);

    public void SaveResolution(int width, int height, bool fullscreen)
    {
        PlayerPrefs.SetInt(KeyResWidth, width);
        PlayerPrefs.SetInt(KeyResHeight, height);
        PlayerPrefs.SetInt(KeyFullscreen, fullscreen ? 1 : 0);
    }

    public void SaveFullscreen(bool fullscreen) => PlayerPrefs.SetInt(KeyFullscreen, fullscreen ? 1 : 0);

    public float GetBgmVolume()  => PlayerPrefs.GetFloat(KeyBgmVolume, 0.5f);
    public float GetSfxVolume()  => PlayerPrefs.GetFloat(KeySfxVolume, 0.5f);
    public int   GetQuality()    => PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel());
    public bool  GetFullscreen() => PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
}

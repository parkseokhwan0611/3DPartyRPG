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
    private const string KeyResIndex   = "Settings_ResolutionIndex";
    private const string KeyFullscreen = "Settings_Fullscreen";

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        ApplySavedSettings();
    }

    private void ApplySavedSettings()
    {
        AudioManager.instance?.SetBgmVolume(GetBgmVolume());
        AudioManager.instance?.SetSfxVolume(GetSfxVolume());

        int quality = Mathf.Clamp(GetQuality(), 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(quality, true);

        bool fullscreen = GetFullscreen();
        int resIndex = PlayerPrefs.GetInt(KeyResIndex, -1);
        if (resIndex >= 0)
        {
            var resolutions = GetDistinctResolutions();
            if (resIndex < resolutions.Count)
            {
                var r = resolutions[resIndex];
                Screen.SetResolution(r.width, r.height, fullscreen);
                return;
            }
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

    public void SaveResolution(int index, bool fullscreen)
    {
        PlayerPrefs.SetInt(KeyResIndex, index);
        PlayerPrefs.SetInt(KeyFullscreen, fullscreen ? 1 : 0);
    }

    public void SaveFullscreen(bool fullscreen) => PlayerPrefs.SetInt(KeyFullscreen, fullscreen ? 1 : 0);

    public float GetBgmVolume()  => PlayerPrefs.GetFloat(KeyBgmVolume, 0.5f);
    public float GetSfxVolume()  => PlayerPrefs.GetFloat(KeySfxVolume, 1f);
    public int   GetQuality()    => PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel());
    public bool  GetFullscreen() => PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
}

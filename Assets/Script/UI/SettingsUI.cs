using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 설정 창(볼륨 + 그래픽) UI 로직. 조작키는 읽기전용 안내라 별도 스크립트 없이
// 고정 텍스트로 표시 — 이 스크립트가 다루는 건 아니다.
public class SettingsUI : MonoBehaviour
{
    [Header("# 볼륨 (0~10 표시, 내부적으로는 0~1로 환산해 AudioManager에 전달)")]
    [SerializeField] Slider          bgmSlider;
    [SerializeField] TextMeshProUGUI bgmValueText;
    [SerializeField] Slider          sfxSlider;
    [SerializeField] TextMeshProUGUI sfxValueText;

    [Header("# 그래픽 - 품질 (0=낮음, 1=보통, 2=높음)")]
    [SerializeField] Button qualityLowButton;
    [SerializeField] Button qualityMidButton;
    [SerializeField] Button qualityHighButton;
    [Tooltip("현재 선택된 품질 버튼 아래 표시할 \"[선택됨]\" 텍스트 (선택된 것만 보이게 됨)")]
    [SerializeField] TextMeshProUGUI qualityLowSelectedText;
    [SerializeField] TextMeshProUGUI qualityMidSelectedText;
    [SerializeField] TextMeshProUGUI qualityHighSelectedText;

    [Header("# 그래픽 - 해상도 / 창모드")]
    [SerializeField] TMP_Dropdown resolutionDropdown;
    [SerializeField] Toggle       fullscreenToggle;

    private List<Resolution> _resolutions;

    void Start()
    {
        // 슬라이더 자체 범위를 0~10으로 강제 (에디터 기본값이 0~1이어도 항상 맞도록)
        if (bgmSlider != null) { bgmSlider.minValue = 0f; bgmSlider.maxValue = 10f; }
        if (sfxSlider != null) { sfxSlider.minValue = 0f; sfxSlider.maxValue = 10f; }

        BuildResolutionDropdown();
        RefreshFromSaved();

        bgmSlider?.onValueChanged.AddListener(OnBgmChanged);
        sfxSlider?.onValueChanged.AddListener(OnSfxChanged);
        qualityLowButton ?.onClick.AddListener(() => SetQuality(0));
        qualityMidButton ?.onClick.AddListener(() => SetQuality(1));
        qualityHighButton?.onClick.AddListener(() => SetQuality(2));
        resolutionDropdown?.onValueChanged.AddListener(OnResolutionChanged);
        fullscreenToggle  ?.onValueChanged.AddListener(OnFullscreenChanged);
    }

    private void RefreshFromSaved()
    {
        var sm = SettingsManager.instance;
        if (sm == null) return;

        if (bgmSlider != null)
        {
            bgmSlider.SetValueWithoutNotify(sm.GetBgmVolume() * 10f);
            UpdateVolumeText(bgmValueText, "배경음", bgmSlider.value);
        }
        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(sm.GetSfxVolume() * 10f);
            UpdateVolumeText(sfxValueText, "효과음", sfxSlider.value);
        }

        HighlightQuality(sm.GetQuality());

        if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(sm.GetFullscreen());

        if (resolutionDropdown != null)
        {
            int idx = _resolutions.FindIndex(r =>
                r.width == Screen.currentResolution.width && r.height == Screen.currentResolution.height);
            resolutionDropdown.SetValueWithoutNotify(Mathf.Max(0, idx));
        }
    }

    private void BuildResolutionDropdown()
    {
        _resolutions = SettingsManager.GetDistinctResolutions();
        if (resolutionDropdown == null) return;

        var options = new List<string>();
        foreach (var r in _resolutions)
            options.Add($"{r.width} x {r.height}");

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
    }

    // v는 슬라이더 표시값(0~10) — AudioManager/저장에는 0~1로 환산해서 넘김
    private void OnBgmChanged(float v)
    {
        AudioManager.instance?.SetBgmVolume(v / 10f);
        SettingsManager.instance?.SaveBgmVolume(v / 10f);
        UpdateVolumeText(bgmValueText, "배경음", v);
    }

    private void OnSfxChanged(float v)
    {
        AudioManager.instance?.SetSfxVolume(v / 10f);
        SettingsManager.instance?.SaveSfxVolume(v / 10f);
        UpdateVolumeText(sfxValueText, "효과음", v);
    }

    private void UpdateVolumeText(TextMeshProUGUI text, string label, float displayValue)
    {
        if (text != null) text.text = $"{label}: {Mathf.RoundToInt(displayValue)}";
    }

    private void SetQuality(int level)
    {
        QualitySettings.SetQualityLevel(level, true);
        SettingsManager.instance?.SaveQuality(level);
        HighlightQuality(level);
    }

    private void HighlightQuality(int level)
    {
        if (qualityLowSelectedText  != null) qualityLowSelectedText.gameObject.SetActive(level == 0);
        if (qualityMidSelectedText  != null) qualityMidSelectedText.gameObject.SetActive(level == 1);
        if (qualityHighSelectedText != null) qualityHighSelectedText.gameObject.SetActive(level == 2);
    }

    private void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= _resolutions.Count) return;

        var  r          = _resolutions[index];
        bool fullscreen = fullscreenToggle != null ? fullscreenToggle.isOn : Screen.fullScreen;

        Screen.SetResolution(r.width, r.height, fullscreen);
        SettingsManager.instance?.SaveResolution(r.width, r.height, fullscreen);
    }

    private void OnFullscreenChanged(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        SettingsManager.instance?.SaveFullscreen(isFullscreen);
    }
}

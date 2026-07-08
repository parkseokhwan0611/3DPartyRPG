using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelExpUI : MonoBehaviour
{
    [Header("# References")]
    public TextMeshProUGUI levelText;
    public Image expBar;

    void OnEnable()
    {
        // DataManager 이벤트 구독
        if (DataManager.instance == null) return;
        DataManager.instance.OnLevelUp  += UpdateUI;
        DataManager.instance.OnExpGained += UpdateUI;
    }

    void OnDisable()
    {
        if (DataManager.instance == null) return;
        DataManager.instance.OnLevelUp  -= UpdateUI;
        DataManager.instance.OnExpGained -= UpdateUI;
    }

    void Start()
    {
        UpdateUI(); // 초기값 세팅
    }

    void UpdateUI()
    {
        var dm = DataManager.instance;
        if (dm == null) return;

        levelText.text   = $"{dm.partyLevel}";
        expBar.fillAmount = (float)dm.partyExp / dm.GetRequiredExp(dm.partyLevel);
    }
}

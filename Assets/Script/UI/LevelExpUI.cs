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

    // OnEnable은 DataManager.Awake()보다 먼저 실행될 수 있어(오브젝트 간 실행 순서 미보장)
    // 구독이 조용히 누락될 수 있음 — Awake는 항상 모든 Start보다 먼저 끝나는 것이 보장되므로
    // Start에서 구독
    void Start()
    {
        if (DataManager.instance != null)
        {
            DataManager.instance.OnLevelUp   += UpdateUI;
            DataManager.instance.OnExpGained += UpdateUI;
        }
        UpdateUI(); // 초기값 세팅
    }

    void OnDestroy()
    {
        if (DataManager.instance == null) return;
        DataManager.instance.OnLevelUp  -= UpdateUI;
        DataManager.instance.OnExpGained -= UpdateUI;
    }

    void UpdateUI()
    {
        var dm = DataManager.instance;
        if (dm == null) return;

        levelText.text   = $"{dm.partyLevel}";
        expBar.fillAmount = (float)dm.partyExp / dm.GetRequiredExp(dm.partyLevel);
    }
}

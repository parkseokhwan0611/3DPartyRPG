using UnityEngine;
using TMPro;

// 파티원 전체 공통 자동전투 상태 표시 (T키로 PartyManager.AutoSkillEnabled 토글).
// 씬에 하나만 배치.
public class AutoSkillIndicatorUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI autoSkillText;

    // OnEnable은 PartyManager.Awake()보다 먼저 실행될 수 있어(오브젝트 간 실행 순서 미보장)
    // 구독이 누락될 수 있음 — Awake는 항상 모든 Start보다 먼저 끝나는 것이 보장되므로 Start에서 구독
    void Start()
    {
        if (PartyManager.instance == null) return;
        PartyManager.instance.OnAutoSkillToggled += UpdateText;
        UpdateText(PartyManager.instance.AutoSkillEnabled);
    }

    void OnDestroy()
    {
        if (PartyManager.instance != null)
            PartyManager.instance.OnAutoSkillToggled -= UpdateText;
    }

    void UpdateText(bool enabled)
    {
        if (autoSkillText == null) return;
        autoSkillText.text = enabled ? "자동 스킬 사용 ON" : "자동 스킬 사용 OFF";
    }
}

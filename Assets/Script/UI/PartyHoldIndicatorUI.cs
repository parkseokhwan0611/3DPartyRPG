using UnityEngine;
using TMPro;

// 파티원 고정 상태 표시 (스페이스바로 PartyManager.PartyHoldEnabled 토글).
// 씬에 하나만 배치.
public class PartyHoldIndicatorUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI partyHoldText;

    // OnEnable은 PartyManager.Awake()보다 먼저 실행될 수 있어(오브젝트 간 실행 순서 미보장)
    // 구독이 누락될 수 있음 — Awake는 항상 모든 Start보다 먼저 끝나는 것이 보장되므로 Start에서 구독
    void Start()
    {
        if (PartyManager.instance == null) return;
        PartyManager.instance.OnPartyHoldToggled += UpdateText;
        UpdateText(PartyManager.instance.PartyHoldEnabled);
    }

    void OnDestroy()
    {
        if (PartyManager.instance != null)
            PartyManager.instance.OnPartyHoldToggled -= UpdateText;
    }

    void UpdateText(bool enabled)
    {
        if (partyHoldText == null) return;
        partyHoldText.text = enabled ? "파티원 고정 ON" : "파티원 고정 OFF";
    }
}

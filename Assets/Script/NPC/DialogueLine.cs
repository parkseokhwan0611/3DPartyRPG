using UnityEngine;

/// <summary>
/// 대화 한 줄의 데이터. 발화자 구분 및 텍스트 보관.
/// NPC·파티원 혼합 대화 지원 + 원하는 이름의 화자(예: "시스템") 직접 지정 가능.
/// </summary>
[System.Serializable]
public class DialogueLine
{
    public enum Speaker
    {
        NPC,
        Character0,   // 파티 인덱스 0 (탱커 등)
        Character1,   // 파티 인덱스 1
        Character2,   // 파티 인덱스 2
        Custom,       // customSpeakerName에 적은 이름 그대로 표시 (예: 시스템, 나레이션, 의문의 목소리)
    }

    [Tooltip("NPC: NPC(보스) 이름 / Character0~2: 해당 파티원 이름 (DataManager에서 자동 조회) / Custom: 아래 이름 칸에 적은 화자")]
    public Speaker speaker = Speaker.NPC;

    [Tooltip("Speaker가 Custom일 때 이름 칸에 표시할 화자 이름 (예: 시스템). 비워두면 이름 칸이 빈 채로 출력")]
    public string customSpeakerName;

    [TextArea(2, 4)]
    public string text;
}

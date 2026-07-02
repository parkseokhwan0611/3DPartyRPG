using UnityEngine;

/// <summary>
/// 대화 한 줄의 데이터. 발화자 구분 및 텍스트 보관.
/// NPC·파티원 혼합 대화 지원.
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
    }

    [Tooltip("NPC: NPC 이름 / Character0~2: 해당 파티원 이름 (DataManager에서 자동 조회)")]
    public Speaker speaker = Speaker.NPC;

    [TextArea(2, 4)]
    public string text;
}

using UnityEngine;

/// <summary>
/// 대화 한 줄의 데이터. NPC·플레이어 구분 및 텍스트 보관.
/// NpcInteractable의 dialogueLines 배열에서 사용.
/// </summary>
[System.Serializable]
public class DialogueLine
{
    public enum Speaker { NPC, Player }

    [Tooltip("NPC: NPC 이름 표시 / Player: 플레이어 이름 표시")]
    public Speaker speaker = Speaker.NPC;

    [TextArea(2, 4)]
    public string text;
}

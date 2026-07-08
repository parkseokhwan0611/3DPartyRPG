using System.Collections.Generic;
using UnityEngine;

// 퀘스트 완료 시 지급되는 아이템 보상 1건
[System.Serializable]
public class QuestRewardItem
{
    public ItemData item;
    [Min(1)] public int count = 1;
}

public enum QuestObjectiveType
{
    NpcDialogue,  // 특정 NPC와 대화
    CollectItem,  // 특정 아이템 N개 수집
    KillMonster,  // 특정 몬스터 N마리 처치
}

[CreateAssetMenu(fileName = "Quest_New", menuName = "RPG/Quest")]
public class QuestData : ScriptableObject
{
    [Header("식별 정보")]
    [Tooltip("세이브 데이터에 사용되는 고유 ID. 절대 중복·변경 금지")]
    public string questId;
    public string questName;
    [TextArea(2, 5)]
    public string description;

    [Header("목표 유형")]
    public QuestObjectiveType objectiveType;

    [Header("목표 — NpcDialogue")]
    [Tooltip("NpcInteractable.NpcId와 일치해야 함")]
    public string targetNpcId;
    [Tooltip("HUD/퀘스트로그에 표시할 NPC 이름 (예: '촌장 하리드')")]
    public string targetNpcDisplayName;

    [Header("목표 — CollectItem")]
    public ItemData targetItem;
    public int targetItemCount = 1;

    [Header("목표 — KillMonster")]
    [Tooltip("EnemyHp.monsterId와 일치해야 함")]
    public string targetMonsterId;
    [Tooltip("HUD/퀘스트로그에 표시할 몬스터 이름 (예: '고블린')")]
    public string targetMonsterDisplayName;
    public int targetMonsterCount = 1;

    [Header("보상")]
    public float rewardExp;
    public int   rewardGold;
    public List<QuestRewardItem> rewardItems = new List<QuestRewardItem>();

    // 목표 타입별 목표 수량 (Dialogue는 수량 개념이 없으므로 1)
    public int TargetCount => objectiveType switch
    {
        QuestObjectiveType.CollectItem => Mathf.Max(1, targetItemCount),
        QuestObjectiveType.KillMonster => Mathf.Max(1, targetMonsterCount),
        _                               => 1,
    };

    // HUD/퀘스트로그에 표시할 목표 설명 (진행도 포함)
    public string GetObjectiveText(int currentProgress)
    {
        int shown = Mathf.Min(currentProgress, TargetCount);
        switch (objectiveType)
        {
            case QuestObjectiveType.NpcDialogue:
                string npcLabel = !string.IsNullOrEmpty(targetNpcDisplayName) ? targetNpcDisplayName : targetNpcId;
                return $"{npcLabel}와(과) 대화하기";
            case QuestObjectiveType.CollectItem:
                string itemLabel = targetItem != null ? targetItem.itemName : "(미지정 아이템)";
                return $"{itemLabel} 수집 ({shown}/{TargetCount})";
            case QuestObjectiveType.KillMonster:
                string monsterLabel = !string.IsNullOrEmpty(targetMonsterDisplayName) ? targetMonsterDisplayName : targetMonsterId;
                return $"{monsterLabel} 처치 ({shown}/{TargetCount})";
            default:
                return "";
        }
    }
}

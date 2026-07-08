using System.Collections.Generic;
using UnityEngine;

// 메인 퀘스트를 진행 순서대로 담아두는 체인. 서브 퀘스트 없음 — 하나씩 순차 진행.
[CreateAssetMenu(fileName = "MainQuestChain", menuName = "RPG/Main Quest Chain")]
public class MainQuestChain : ScriptableObject
{
    [Tooltip("진행 순서대로 배치")]
    public List<QuestData> quests = new List<QuestData>();
}

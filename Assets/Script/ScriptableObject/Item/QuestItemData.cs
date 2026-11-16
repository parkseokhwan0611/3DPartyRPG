using UnityEngine;

// ─────────────────────────────────────────────────────────────────
// 퀘스트(스토리) 아이템 SO
// ─────────────────────────────────────────────────────────────────

[CreateAssetMenu(fileName = "QuestItem", menuName = "Scriptable Object/Item/QuestItem")]
public class QuestItemData : ItemData
{
    // 사용·장착·판매 불가 — 스토리 몰입용으로 인벤토리에 보관만 한다 (같은 아이템은 수량으로 합산)
    // 보스 처치 확정 지급은 BossClearActivator.rewardItem에 지정
}

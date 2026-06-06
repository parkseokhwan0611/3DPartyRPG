using UnityEngine;

// ─────────────────────────────────────────────────────────────────
// 재료 아이템 SO
// ─────────────────────────────────────────────────────────────────

[CreateAssetMenu(fileName = "MaterialItem", menuName = "Scriptable Object/Item/MaterialItem")]
public class MaterialData : ItemData
{
    // 재료 아이템은 개별 사용 불가 — NPC 제작에만 사용
    // 추가 필드는 제작 레시피 시스템 구현 시 확장
}

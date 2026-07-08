using UnityEngine.EventSystems;

/// <summary>
/// 인벤토리 슬롯에 붙는 드래그 핸들러.
/// 장비 아이템만 드래그 가능 — 비장비 아이템은 즉시 취소.
/// </summary>
public class InventoryDragItem : DragItemBase
{
    [UnityEngine.HideInInspector] public InventoryUI inventoryUI;

    // 더블클릭으로도 장착(드래그·장착 버튼과 동일한 결과). 슬롯은 자동 결정(반지는 1→2 순서).
    public override void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount < 2) return;
        if (item == null || !item.IsEquipment) return;
        inventoryUI?.EquipItem(item);
    }
}

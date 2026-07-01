using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 장착 슬롯 버튼에 붙는 드롭 핸들러.
/// InventoryUI.Awake에서 AddComponent로 생성되므로 Inspector 노출 없음.
/// </summary>
public class EquipSlotDropTarget : MonoBehaviour, IDropHandler
{
    [HideInInspector] public EquipSlot   slot;
    [HideInInspector] public InventoryUI inventoryUI;

    public void OnDrop(PointerEventData data)
    {
        var drag = data.pointerDrag?.GetComponent<InventoryDragItem>();
        if (drag?.item?.data is not EquipItemData equip) return;
        if (!SlotMatches(equip, slot)) return;

        inventoryUI.EquipFromDrag(drag.item, slot);
    }

    // Ring 아이템은 Ring1·Ring2 어느 슬롯에든 드롭 허용
    private static bool SlotMatches(EquipItemData equip, EquipSlot target)
    {
        EquipSlot natural = equip.GetEquipSlot();
        if (natural == EquipSlot.Ring1)
            return target is EquipSlot.Ring1 or EquipSlot.Ring2;
        return natural == target;
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 강화 대상 슬롯 또는 주문서 슬롯에 붙이는 드롭 타겟.
/// EnhancementUI의 EquipSlot / ScrollSlot GameObject에 컴포넌트로 추가.
/// </summary>
public class EnhancementSlotDropTarget : MonoBehaviour, IDropHandler
{
    public enum SlotType { Equipment, Scroll }
    [HideInInspector] public SlotType slotType;

    public void OnDrop(PointerEventData data)
    {
        var drag = data.pointerDrag?.GetComponent<EnhancementDragItem>();
        if (drag?.item == null) return;

        if (slotType == SlotType.Equipment)
            EnhancementUI.instance?.RegisterEquip(drag.item);
        else
            EnhancementUI.instance?.RegisterScroll(drag.item);
    }
}

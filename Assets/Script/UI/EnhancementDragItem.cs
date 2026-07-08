using UnityEngine.EventSystems;

/// <summary>
/// EnhancementUI 인벤토리 슬롯에 붙는 드래그 핸들러.
/// 장비와 강화 주문서 모두 드래그 가능.
/// </summary>
public class EnhancementDragItem : DragItemBase
{
    // 더블클릭으로도 등록(드래그·등록 버튼과 동일한 결과)
    public override void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount < 2) return;
        if (item == null) return;
        EnhancementUI.instance?.RegisterItem(item);
    }

    protected override bool CanDrag(ItemInstance item)
        => item != null && (item.IsEquipment || IsEnhancementScroll(item));

    private static bool IsEnhancementScroll(ItemInstance item)
        => item.data is ConsumableData cd
        && (cd.consumableType == ConsumableType.WeaponScroll
         || cd.consumableType == ConsumableType.ArmorScroll);
}

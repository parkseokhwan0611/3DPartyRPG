using UnityEngine;

public enum EnhanceResult
{
    Success,
    Fail,
    AlreadyMax,
    InvalidItem,      // 장비가 아니거나 주문서가 아님
    WrongScrollType,  // 무기 주문서→방어구, 또는 방어구 주문서→무기 시도
}

/// <summary>
/// 강화 실행 로직. 성공 시 장착 중인 아이템이면 스탯도 즉시 재계산.
/// </summary>
public static class EnhancementSystem
{
    /// <param name="equipment">강화할 장비 인스턴스</param>
    /// <param name="scroll">강화 주문서 ConsumableData</param>
    /// <param name="ownerPartyIndex">장비 소유자의 파티 인덱스 (-1이면 인벤토리에만 있음)</param>
    public static EnhanceResult TryEnhance(ItemInstance equipment, ConsumableData scroll, int ownerPartyIndex = -1)
    {
        if (equipment == null || scroll == null) return EnhanceResult.InvalidItem;
        if (equipment.data is not EquipItemData equip) return EnhanceResult.InvalidItem;
        if (!IsScrollCompatible(scroll, equip)) return EnhanceResult.WrongScrollType;
        if (equipment.enhancementLevel >= equip.MaxEnhancement) return EnhanceResult.AlreadyMax;

        bool success = equipment.TryEnhance(scroll);

        if (success)
            TryRecalculateIfEquipped(equipment, ownerPartyIndex);

        return success ? EnhanceResult.Success : EnhanceResult.Fail;
    }

    /// <summary>주문서 타입과 장비 종류가 호환되는지 검사.</summary>
    public static bool IsScrollCompatible(ConsumableData scroll, EquipItemData equip)
    {
        return scroll.consumableType switch
        {
            ConsumableType.WeaponScroll => equip.equipType is EquipType.Sword or EquipType.Staff,
            ConsumableType.ArmorScroll  => equip.equipType is EquipType.Hat or EquipType.Chest
                                                           or EquipType.Gloves or EquipType.Boots,
            _ => false,
        };
    }

    // 아이템이 현재 장착 중이라면 CharacterStatus 스탯 재계산
    private static void TryRecalculateIfEquipped(ItemInstance equipment, int partyIndex)
    {
        if (partyIndex < 0) return;
        if (DataManager.instance == null) return;

        var equipments = DataManager.instance.partyEquipments;
        if (partyIndex >= equipments.Count) return;

        var charEquip = equipments[partyIndex];
        bool isEquipped = false;
        foreach (var kvp in charEquip.Slots)
        {
            if (kvp.Value == equipment) { isEquipped = true; break; }
        }

        if (!isEquipped) return;

        var statuses = DataManager.instance.partyStatuses;
        if (partyIndex < statuses.Count)
            charEquip.RecalculateStats(statuses[partyIndex]);
    }
}

using System;
using System.Collections.Generic;

/// <summary>
/// 파티 공유 인벤토리 (100칸).
/// DataManager.sharedInventory 에 보관, DontDestroyOnLoad로 씬 간 유지.
/// </summary>
[Serializable]
public class Inventory
{
    public const int MaxSlots = 100;

    private List<ItemInstance> items = new List<ItemInstance>();

    public IReadOnlyList<ItemInstance> Items => items;
    public int  Count    => items.Count;
    public bool IsFull   => items.Count >= MaxSlots;

    // ─────────────────────────────────────────────────────────────────
    // 추가
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 아이템 추가.
    /// 소비·재료: 같은 SO 아이템이 있으면 스택에 합산 (슬롯 불필요).
    /// 장비:      무조건 새 슬롯. 슬롯이 꽉 차면 false 반환.
    /// </summary>
    public bool TryAddItem(ItemInstance newItem)
    {
        if (newItem == null || newItem.data == null) return false;

        // 소비·재료: 기존 슬롯에 합산
        if (!newItem.IsEquipment)
        {
            var existing = items.Find(i => i.data == newItem.data);
            if (existing != null)
            {
                existing.AddStack(newItem.stackCount);
                return true;
            }
        }

        if (IsFull) return false;
        items.Add(newItem);
        return true;
    }

    // ─────────────────────────────────────────────────────────────────
    // 제거
    // ─────────────────────────────────────────────────────────────────

    /// <summary>인덱스로 슬롯 제거. 해당 ItemInstance 반환 (없으면 null).</summary>
    public ItemInstance RemoveAt(int index)
    {
        if (index < 0 || index >= items.Count) return null;
        var item = items[index];
        items.RemoveAt(index);
        return item;
    }

    /// <summary>특정 인스턴스 제거. 성공 여부 반환.</summary>
    public bool Remove(ItemInstance item)
    {
        return items.Remove(item);
    }

    /// <summary>
    /// 소비·재료 수량 차감. 0이 되면 슬롯 자동 삭제.
    /// 인벤토리에 없거나 수량 부족이면 false.
    /// </summary>
    public bool ConsumeItem(ItemInstance item, int amount = 1)
    {
        if (!items.Contains(item)) return false;
        if (!item.ConsumeStack(amount)) return false;
        if (item.stackCount <= 0) items.Remove(item);
        return true;
    }

    // ─────────────────────────────────────────────────────────────────
    // 정렬
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 소비 → 장비 → 재료 순.
    /// 소비 아이템 내: HP물약 → MP물약 → 주문서.
    /// 같은 카테고리/타입 내에서는 등급 높은 순.
    /// </summary>
    public void Sort()
    {
        items.Sort((a, b) =>
        {
            int typeComp = GetTypeOrder(a) - GetTypeOrder(b);
            if (typeComp != 0) return typeComp;
            return (int)b.data.grade - (int)a.data.grade; // 높은 등급 먼저
        });
    }

    private static int GetTypeOrder(ItemInstance item)
    {
        if (item.IsConsumable)
        {
            var consumable = (ConsumableData)item.data;
            return consumable.consumableType switch
            {
                ConsumableType.HpPotion          => 0,
                ConsumableType.MpPotion          => 1,
                ConsumableType.EnhancementScroll => 2,
                _                                => 3,
            };
        }
        if (item.IsEquipment) return 4;
        return 5; // Material
    }
}

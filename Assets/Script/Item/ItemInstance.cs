using System;

/// <summary>
/// 인벤토리·장착 슬롯에 실제로 존재하는 아이템 인스턴스.
/// SO(ItemData)는 아이템 템플릿, ItemInstance는 그 사본.
/// 같은 SO라도 인벤토리 칸을 별도로 차지(장비) or 수량으로 합산(소비·재료).
/// </summary>
[Serializable]
public class ItemInstance
{
    // ── 공통 ──────────────────────────────────
    public ItemData data;

    // ── 장비 전용 ─────────────────────────────
    /// <summary>현재 강화 단계 (장비만 사용, 그 외 0)</summary>
    public int enhancementLevel;

    // ── 소비·재료 전용 ────────────────────────
    /// <summary>스택 수량 (소비·재료만 사용, 그 외 1)</summary>
    public int stackCount;

    // ─────────────────────────────────────────────────────────────
    // 생성자
    // ─────────────────────────────────────────────────────────────

    public ItemInstance(ItemData data, int stack = 1)
    {
        this.data             = data;
        this.enhancementLevel = 0;
        this.stackCount       = stack;
    }

    // ─────────────────────────────────────────────────────────────
    // 헬퍼
    // ─────────────────────────────────────────────────────────────

    public bool IsEquipment  => data is EquipItemData;
    public bool IsConsumable => data is ConsumableData;
    public bool IsMaterial   => data is MaterialData;

    /// <summary>소비·재료 아이템에 수량 추가. 장비는 무시.</summary>
    public void AddStack(int amount)
    {
        if (!IsEquipment) stackCount += amount;
    }

    /// <summary>소비·재료 아이템에서 수량 차감. 0 이하면 false 반환.</summary>
    public bool ConsumeStack(int amount = 1)
    {
        if (IsEquipment || stackCount < amount) return false;
        stackCount -= amount;
        return true;
    }

    /// <summary>장비 강화 시도. 성공 여부 반환.</summary>
    public bool TryEnhance(ConsumableData scroll)
    {
        if (scroll.consumableType != ConsumableType.EnhancementScroll) return false;
        if (data is not EquipItemData equip) return false;
        if (enhancementLevel >= equip.MaxEnhancement) return false;

        bool success = UnityEngine.Random.value <= scroll.successRate;
        if (success)
            enhancementLevel++;

        return success;
    }

    /// <summary>강화 포함 메인 옵션 최종값 (장비만).</summary>
    public float GetMainValue()
    {
        if (data is EquipItemData equip)
            return equip.GetEnhancedMainValue(enhancementLevel);
        return 0f;
    }
}

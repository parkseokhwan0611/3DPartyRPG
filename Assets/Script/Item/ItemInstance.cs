using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────
// 강화 누적 보너스 (옵션별 독립 추적)
// ─────────────────────────────────────────────────────────────────
[Serializable]
public class OptionBonus
{
    public MainOptionType type;
    public float          value;
}

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
    /// <summary>강화 주문서로 누적된 메인 옵션 보너스 (옵션별 독립값)</summary>
    public List<OptionBonus> enhancementBonuses = new List<OptionBonus>();

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

    /// <summary>장비 강화 시도. 성공 시 주문서 증가량을 누적 보너스에 적용. 성공 여부 반환.</summary>
    public bool TryEnhance(ConsumableData scroll)
    {
        if (scroll.consumableType != ConsumableType.WeaponScroll &&
            scroll.consumableType != ConsumableType.ArmorScroll) return false;
        if (data is not EquipItemData equip) return false;
        if (enhancementLevel >= equip.MaxEnhancement) return false;

        bool success = UnityEngine.Random.value < scroll.successRate;
        if (success)
        {
            enhancementLevel++;
            foreach (var opt in equip.mainOptions)
                AddBonus(opt.type, scroll.GetIncrementFor(opt.type));
        }

        return success;
    }

    /// <summary>강화 포함 메인 옵션 최종값 목록 (장비만). 순회용.</summary>
    public IEnumerable<(MainOptionType type, float value)> GetMainValues()
    {
        if (data is not EquipItemData equip) yield break;
        foreach (var opt in equip.mainOptions)
            yield return (opt.type, opt.value + GetEnhancementBonus(opt.type));
    }

    /// <summary>특정 옵션의 누적 강화 보너스 반환.</summary>
    public float GetEnhancementBonus(MainOptionType type)
    {
        if (enhancementBonuses == null) return 0f;
        foreach (var b in enhancementBonuses)
            if (b.type == type) return b.value;
        return 0f;
    }

    private void AddBonus(MainOptionType type, float amount)
    {
        if (amount == 0f) return;
        if (enhancementBonuses == null) enhancementBonuses = new List<OptionBonus>();
        foreach (var b in enhancementBonuses)
        {
            if (b.type == type) { b.value += amount; return; }
        }
        enhancementBonuses.Add(new OptionBonus { type = type, value = amount });
    }
}

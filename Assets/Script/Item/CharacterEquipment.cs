using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 1명의 장비 슬롯 (총 8칸).
/// DataManager.partyEquipments[partyIndex] 에 보관.
/// </summary>
[Serializable]
public class CharacterEquipment
{
    // 슬롯 8개: Weapon / Hat / Chest / Gloves / Boots / Necklace / Ring1 / Ring2
    private Dictionary<EquipSlot, ItemInstance> slots = new Dictionary<EquipSlot, ItemInstance>();

    public IReadOnlyDictionary<EquipSlot, ItemInstance> Slots => slots;

    // ─────────────────────────────────────────────────────────────────
    // 슬롯 조회
    // ─────────────────────────────────────────────────────────────────

    /// <summary>해당 슬롯에 장착된 아이템 반환. 없으면 null.</summary>
    public ItemInstance GetSlot(EquipSlot slot)
    {
        return slots.TryGetValue(slot, out var item) ? item : null;
    }

    // ─────────────────────────────────────────────────────────────────
    // 장착 / 해제
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 아이템 장착.
    /// Ring은 Ring1 → Ring2 순서로 자동 배치.
    /// 이미 슬롯이 차 있으면 기존 아이템을 반환 (호출자가 인벤토리에 돌려놓음).
    /// </summary>
    public ItemInstance Equip(ItemInstance newItem)
    {
        if (newItem?.data is not EquipItemData equip) return null;

        EquipSlot slot = DetermineSlot(equip);
        slots.TryGetValue(slot, out var prev);
        slots[slot] = newItem;
        return prev;
    }

    /// <summary>
    /// 특정 슬롯을 직접 지정해서 장착 (UI에서 Ring1/Ring2 명시적 교체 시 사용).
    /// </summary>
    public ItemInstance EquipToSlot(ItemInstance newItem, EquipSlot targetSlot)
    {
        if (newItem?.data is not EquipItemData) return null;

        slots.TryGetValue(targetSlot, out var prev);
        slots[targetSlot] = newItem;
        return prev;
    }

    /// <summary>
    /// 슬롯 해제. 해제된 아이템 반환 (호출자가 인벤토리에 돌려놓음).
    /// </summary>
    public ItemInstance Unequip(EquipSlot slot)
    {
        if (!slots.TryGetValue(slot, out var item)) return null;
        slots.Remove(slot);
        return item;
    }

    // ─────────────────────────────────────────────────────────────────
    // 스탯 재계산
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 장착 아이템 전체를 기준으로 CharacterStatus의 장비 보너스를 재계산.
    /// 장착/해제 시마다 호출.
    /// </summary>
    /// <param name="preserveHpDeficit">
    /// true면 이번 재계산으로 최대체력이 바뀐 만큼 현재체력도 같은 폭으로 조정(델타 유지).
    /// 세이브 파일 복원처럼 currentHp가 이미 최종값으로 세팅돼 있는 경우엔 false로 호출해서
    /// 장비 보너스가 중복 반영되지 않도록 해야 함.
    /// </param>
    public void RecalculateStats(CharacterStatus status, bool preserveHpDeficit = true)
    {
        if (status == null) return;

        float oldMaxHp = status.MaxHp;

        // 1. 장비 보너스 전부 초기화
        status.equipStr = status.equipVit = status.equipInt = status.equipFht = 0f;
        status.equipAtk = status.equipAp  = status.equipMaxHp = 0f;
        status.equipDef = status.equipMagicRes  = 0f;
        status.equipCritRate = status.equipCritDmg  = 0f;
        status.equipCDReduce = status.equipMpReduce = 0f;
        status.equipPhysDmg  = status.equipMagicDmg = 0f;

        // 2. 장착된 아이템별 스탯 누적
        foreach (var kvp in slots)
        {
            if (kvp.Value?.data is not EquipItemData equip) continue;

            // 메인 옵션 (강화 포함, 무기 1개 / 방어구 3개 / 장신구 0개)
            foreach (var (type, val) in kvp.Value.GetMainValues())
            {
                switch (type)
                {
                    case MainOptionType.PhysAtk:  status.equipAtk      += val; break;
                    case MainOptionType.MagicAtk: status.equipAp       += val; break;
                    case MainOptionType.MaxHP:    status.equipMaxHp    += val; break;
                    case MainOptionType.PhysDef:  status.equipDef      += val; break;
                    case MainOptionType.MagicRes: status.equipMagicRes += val; break;
                }
            }

            // 서브 옵션 (고정값)
            foreach (var sub in equip.subOptions)
                ApplySubOption(status, sub);
        }

        // 3. 장비로 인해 최대체력이 바뀐 만큼 현재체력도 같은 폭으로 조정
        if (preserveHpDeficit)
        {
            float delta = status.MaxHp - oldMaxHp;
            if (delta != 0f)
                status.currentHp = Mathf.Max(0f, status.currentHp + delta);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 내부 헬퍼
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Ring: Ring1이 비어있으면 Ring1, 아니면 Ring2 자동 배치.</summary>
    private EquipSlot DetermineSlot(EquipItemData equip)
    {
        EquipSlot base_ = equip.GetEquipSlot();
        if (base_ == EquipSlot.Ring1)
        {
            bool ring1Empty = !slots.ContainsKey(EquipSlot.Ring1) || slots[EquipSlot.Ring1] == null;
            return ring1Empty ? EquipSlot.Ring1 : EquipSlot.Ring2;
        }
        return base_;
    }

    private static void ApplySubOption(CharacterStatus status, SubOption sub)
    {
        switch (sub.type)
        {
            case SubOptionType.STR:           status.equipStr      += sub.value; break;
            case SubOptionType.VIT:           status.equipVit      += sub.value; break;
            case SubOptionType.INT:           status.equipInt      += sub.value; break;
            case SubOptionType.FTH:           status.equipFht      += sub.value; break;
            case SubOptionType.PhysDef:       status.equipDef      += sub.value; break;
            case SubOptionType.MagicRes:      status.equipMagicRes += sub.value; break;
            case SubOptionType.CritRate:      status.equipCritRate += sub.value; break;
            case SubOptionType.CritDmg:       status.equipCritDmg  += sub.value; break;
            case SubOptionType.SkillCDReduce: status.equipCDReduce += sub.value; break;
            case SubOptionType.MpCostReduce:  status.equipMpReduce += sub.value; break;
            case SubOptionType.PhysDmgBonus:  status.equipPhysDmg  += sub.value; break;
            case SubOptionType.MagicDmgBonus: status.equipMagicDmg += sub.value; break;
        }
    }
}

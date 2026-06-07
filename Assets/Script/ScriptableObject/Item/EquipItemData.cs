using UnityEngine;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────
// 장비 관련 열거형
// ─────────────────────────────────────────────────────────────────

public enum WeaponType    { Sword, Staff }
public enum ArmorType     { Hat, Chest, Gloves, Boots }
public enum AccessoryType { Necklace, Ring }

// 장비 슬롯 (캐릭터 장착 슬롯 인덱스)
public enum EquipSlot
{
    Weapon,
    Hat, Chest, Gloves, Boots,
    Necklace, Ring1, Ring2
}

// 메인 옵션 종류
public enum MainOptionType
{
    PhysAtk,  // 물리 공격력 (무기)
    MagicAtk, // 마법 공격력 (무기)
    MaxHP,    // 최대 체력   (방어구)
    PhysDef,  // 물리 방어력 (방어구)
    MagicRes, // 마법 저항력 (방어구)
}

// 서브 옵션 종류
// - 무기/방어구: STR~CritDmg
// - 장신구:      STR~MagicDmgBonus (SkillCDReduce·MpCostReduce·PhysDmgBonus·MagicDmgBonus 추가)
public enum SubOptionType
{
    // 기본 스탯
    STR, VIT, INT, FTH,
    // 전투 수치
    PhysDef, MagicRes,
    CritRate, CritDmg,
    // 특수 (장신구 전용 권장)
    SkillCDReduce,  // 스킬 쿨타임 감소 (0.1 = 10%)
    MpCostReduce,   // 마나 소모 감소  (0.1 = 10%)
    PhysDmgBonus,   // 물리 피해 증가  (0.1 = 10%)
    MagicDmgBonus,  // 마법 피해 증가  (0.1 = 10%)
}

// ─────────────────────────────────────────────────────────────────
// 메인 옵션 (강화 포함)
// ─────────────────────────────────────────────────────────────────

[System.Serializable]
public class MainOption
{
    public MainOptionType type;
    public float          value;
    [Tooltip("강화 1단계당 증가량. 장신구는 0.")]
    public float          enhancementBonusPerLevel = 0f;

    public float GetEnhancedValue(int level) => value + enhancementBonusPerLevel * level;
}

// ─────────────────────────────────────────────────────────────────
// 서브 옵션 (값 고정, SO에서 직접 설정)
// ─────────────────────────────────────────────────────────────────

[System.Serializable]
public class SubOption
{
    public SubOptionType type;
    public float         value;
}

// ─────────────────────────────────────────────────────────────────
// 장비 아이템 SO (무기 / 방어구 / 장신구 공통)
// ─────────────────────────────────────────────────────────────────

[CreateAssetMenu(fileName = "EquipItem", menuName = "Scriptable Object/Item/EquipItem")]
public class EquipItemData : ItemData
{
    // ── 장비 종류 ──────────────────────────────
    [Header("장비 분류")]
    public WeaponType    weaponType;    // itemType == Weapon 일 때
    public ArmorType     armorType;     // itemType == Armor  일 때
    public AccessoryType accessoryType; // itemType == Accessory 일 때

    // ── 메인 옵션 ──────────────────────────────
    [Header("메인 옵션")]
    [Tooltip("무기: 1개 / 방어구: 3개 (MaxHP·PhysDef·MagicRes) / 장신구: 0개")]
    public List<MainOption> mainOptions = new List<MainOption>();

    // ── 서브 옵션 (등급에 따라 1~4개 설정) ────
    [Header("서브 옵션")]
    public List<SubOption> subOptions = new List<SubOption>();

    // ─────────────────────────────────────────────────────────────
    // 헬퍼
    // ─────────────────────────────────────────────────────────────

    /// <summary>등급별 최대 강화 수치</summary>
    public int MaxEnhancement => grade switch
    {
        ItemGrade.Normal    => 3,
        ItemGrade.Advanced  => 5,
        ItemGrade.Elite     => 7,
        ItemGrade.Legendary => 9,
        ItemGrade.Mythic    => 12,
        _                   => 0,
    };

    /// <summary>이 장비가 들어가는 슬롯 (Ring은 Ring1 반환 — 런타임에서 빈 슬롯 판별)</summary>
    public EquipSlot GetEquipSlot()
    {
        return itemType switch
        {
            ItemType.Weapon    => EquipSlot.Weapon,
            ItemType.Armor     => armorType switch
            {
                ArmorType.Hat    => EquipSlot.Hat,
                ArmorType.Chest  => EquipSlot.Chest,
                ArmorType.Gloves => EquipSlot.Gloves,
                ArmorType.Boots  => EquipSlot.Boots,
                _                => EquipSlot.Hat,
            },
            ItemType.Accessory => accessoryType switch
            {
                AccessoryType.Necklace => EquipSlot.Necklace,
                AccessoryType.Ring     => EquipSlot.Ring1, // 런타임에서 Ring2 여부 결정
                _                      => EquipSlot.Necklace,
            },
            _ => EquipSlot.Weapon,
        };
    }
}

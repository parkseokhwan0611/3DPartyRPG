using UnityEngine;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────
// 장비 관련 열거형
// ─────────────────────────────────────────────────────────────────

/// <summary>장비 종류 — 이 하나로 슬롯과 카테고리를 모두 결정합니다.</summary>
public enum EquipType
{
    // 무기
    Sword, Staff,
    // 방어구
    Hat, Chest, Gloves, Boots,
    // 장신구
    Necklace, Ring,
}

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
// 에셋·세이브에 정수로 저장되므로 번호는 고정 — 인스펙터 목록 순서만 선언 순서로 정렬 (DEX는 나중에 추가돼 12번)
public enum SubOptionType
{
    // 기본 스탯
    STR = 0, DEX = 12, VIT = 1, INT = 2, FTH = 3,
    // 전투 수치 (방어력·마법 저항력은 고정 수치)
    PhysDef = 4, MagicRes = 5,
    CritRate = 6, CritDmg = 7,
    // 특수 (장신구 전용 권장)
    SkillCDReduce = 8,  // 스킬 쿨타임 감소 (0.1 = 10%)
    MpCostReduce  = 9,  // 마나 소모 감소  (0.1 = 10%)
    PhysDmgBonus  = 10, // 물리 피해 증가  (0.1 = 10%)
    MagicDmgBonus = 11, // 마법 피해 증가  (0.1 = 10%)
}

// ─────────────────────────────────────────────────────────────────
// 메인 옵션 (강화 포함)
// ─────────────────────────────────────────────────────────────────

[System.Serializable]
public class MainOption
{
    public MainOptionType type;
    public float          value;
    [Tooltip("강화 1단계당 증가량 (강화 주문서 성공 시 이 값만큼 오름). 장신구는 0.\n" +
             "강화 수치는 저장하지 않고 단계 × 이 값으로 계산하므로, 바꾸면 이미 강화된 장비에도 바로 반영됨")]
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

    // 비율로 저장되는 옵션(0.02 = 2%) — UI에는 %로 표시
    public bool IsPercent => type == SubOptionType.CritRate      || type == SubOptionType.CritDmg
                          || type == SubOptionType.SkillCDReduce || type == SubOptionType.MpCostReduce
                          || type == SubOptionType.PhysDmgBonus  || type == SubOptionType.MagicDmgBonus;

    /// <summary>툴팁 표시용 값 — "+20" 또는 "+2%"</summary>
    public string FormatValue() => IsPercent ? $"+{value * 100f:0.##}%" : $"+{value:0.##}";
}

// ─────────────────────────────────────────────────────────────────
// 장비 아이템 SO (무기 / 방어구 / 장신구 공통)
// ─────────────────────────────────────────────────────────────────

[CreateAssetMenu(fileName = "EquipItem", menuName = "Scriptable Object/Item/EquipItem")]
public class EquipItemData : ItemData
{
    // ── 장비 종류 ──────────────────────────────
    [Header("장비 분류")]
    public EquipType equipType;

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
    public EquipSlot GetEquipSlot() => equipType switch
    {
        EquipType.Sword    => EquipSlot.Weapon,
        EquipType.Staff    => EquipSlot.Weapon,
        EquipType.Hat      => EquipSlot.Hat,
        EquipType.Chest    => EquipSlot.Chest,
        EquipType.Gloves   => EquipSlot.Gloves,
        EquipType.Boots    => EquipSlot.Boots,
        EquipType.Necklace => EquipSlot.Necklace,
        EquipType.Ring     => EquipSlot.Ring1, // 런타임에서 Ring2 여부 결정
        _                  => EquipSlot.Weapon,
    };
}

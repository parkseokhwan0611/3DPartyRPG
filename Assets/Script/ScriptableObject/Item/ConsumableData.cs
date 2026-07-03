using UnityEngine;

public enum ConsumableType
{
    HpPotion  = 0,
    MpPotion  = 1,
    WeaponScroll = 2,  // 무기 강화 주문서 (검/지팡이)  ← 구 EnhancementScroll 자리 유지
    ArmorScroll  = 3,  // 방어구 강화 주문서 (모자/갑옷/장갑/신발)
}

// ─────────────────────────────────────────────────────────────────
// 소비 아이템 SO (물약 + 강화 주문서 공통)
// ─────────────────────────────────────────────────────────────────

[CreateAssetMenu(fileName = "ConsumableItem", menuName = "Scriptable Object/Item/ConsumableItem")]
public class ConsumableData : ItemData
{
    [Header("소비 아이템 분류")]
    public ConsumableType consumableType;

    // ── 물약 ───────────────────────────────────
    [Header("물약 설정 (HpPotion / MpPotion)")]
    [Tooltip("회복량 (HP 또는 MP)")]
    public float healAmount;
    [Tooltip("물약 쿨타임 (초)")]
    public float cooldown;

    // ── 강화 주문서 공통 ───────────────────────
    [Header("강화 주문서 공통 (WeaponScroll / ArmorScroll)")]
    [Tooltip("강화 성공 확률 (0~1)")]
    [Range(0f, 1f)]
    public float successRate;

    // ── 무기 주문서 ────────────────────────────
    [Header("무기 주문서 설정 (WeaponScroll)")]
    [Tooltip("성공 시 물리 공격력 또는 마법 공격력 증가량")]
    public float weaponAtkIncrement;

    // ── 방어구 주문서 ──────────────────────────
    [Header("방어구 주문서 설정 (ArmorScroll)")]
    [Tooltip("성공 시 최대 체력 증가량")]
    public float hpIncrement;
    [Tooltip("성공 시 물리 방어력 증가량")]
    public float physDefIncrement;
    [Tooltip("성공 시 마법 저항력 증가량")]
    public float magicResIncrement;

    /// <summary>메인 옵션 타입별 증가량 반환. 해당 없는 타입은 0.</summary>
    public float GetIncrementFor(MainOptionType type) => type switch
    {
        MainOptionType.PhysAtk  => weaponAtkIncrement,
        MainOptionType.MagicAtk => weaponAtkIncrement,
        MainOptionType.MaxHP    => hpIncrement,
        MainOptionType.PhysDef  => physDefIncrement,
        MainOptionType.MagicRes => magicResIncrement,
        _                       => 0f,
    };
}

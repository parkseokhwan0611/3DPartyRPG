using UnityEngine;

public enum ConsumableType
{
    HpPotion  = 0,
    MpPotion  = 1,
    WeaponScroll = 2,  // 무기 강화 주문서 (검/지팡이)  ← 구 EnhancementScroll 자리 유지
    ArmorScroll  = 3,  // 방어구 강화 주문서 (모자/갑옷/장갑/신발)
    SkillPoint   = 4,  // 사용 시 파티원 전체 스킬 포인트 증가 (희귀 드랍·퀘스트 보상)
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
    [Tooltip("강화 성공 확률 (0~1).\n강화 시 스탯 증가량은 주문서가 아니라 장비 SO의 메인 옵션별 " +
             "Enhancement Bonus Per Level에서 정한다")]
    [Range(0f, 1f)]
    public float successRate;

    // ── 스킬 포인트 아이템 ──────────────────────
    [Header("스킬 포인트 아이템 설정 (SkillPoint)")]
    [Tooltip("사용 시 파티원 전원에게 지급할 스킬 포인트")]
    [Min(1)]
    public int skillPointAmount = 1;

    /// <summary>인벤토리에서 '사용'할 수 있는 소비 아이템인지 (물약은 퀵슬롯, 주문서는 강화 창에서 쓰므로 제외)</summary>
    public bool IsUsableFromInventory => consumableType == ConsumableType.SkillPoint;
}

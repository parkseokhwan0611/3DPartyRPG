using UnityEngine;

public enum ConsumableType { HpPotion, MpPotion, EnhancementScroll }

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

    // ── 강화 주문서 ────────────────────────────
    [Header("강화 주문서 설정 (EnhancementScroll)")]
    [Tooltip("강화 성공 확률 (0~1)")]
    [Range(0f, 1f)]
    public float successRate;
    [Tooltip("성공 시 메인 옵션 증가량")]
    public float enhancementIncrement;
}

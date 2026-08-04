using UnityEngine;

public class PotionCombatSlotUI : MonoBehaviour
{
    [Header("# 포션 슬롯 (1: HP, 2: MP)")]
    public CombatQuickSlot slotHp;
    public CombatQuickSlot slotMp;

    // OnEnable은 PotionQuickSlotManager.Awake()보다 먼저 실행될 수 있어(오브젝트 간 실행 순서
    // 미보장) 구독이 조용히 누락될 수 있음 — Awake는 항상 모든 Start보다 먼저 끝나는 것이
    // 보장되므로 Start에서 구독 (AutoSkillIndicatorUI와 동일한 패턴)
    void Start()
    {
        if (PotionQuickSlotManager.instance != null)
        {
            PotionQuickSlotManager.instance.OnHpSlotChanged -= RefreshHpSlot;
            PotionQuickSlotManager.instance.OnHpSlotChanged += RefreshHpSlot;
            PotionQuickSlotManager.instance.OnMpSlotChanged -= RefreshMpSlot;
            PotionQuickSlotManager.instance.OnMpSlotChanged += RefreshMpSlot;
        }
        RefreshHpSlot();
        RefreshMpSlot();
    }

    void OnDestroy()
    {
        if (PotionQuickSlotManager.instance == null) return;
        PotionQuickSlotManager.instance.OnHpSlotChanged -= RefreshHpSlot;
        PotionQuickSlotManager.instance.OnMpSlotChanged -= RefreshMpSlot;
    }

    // 이벤트 구독 타이밍(씬 전환 직후 등)으로 갱신 신호를 놓쳐도 화면이 실제 개수와 어긋난 채
    // 남아있지 않도록, 매 프레임 실제 스택 수량과 비교해서 다르면 자동으로 다시 맞춤
    private int _lastHpCount = int.MinValue;
    private int _lastMpCount = int.MinValue;

    void Update()
    {
        if (PotionQuickSlotManager.instance == null) return;
        if (slotHp != null) UpdateCooldown(slotHp, ConsumableType.HpPotion);
        if (slotMp != null) UpdateCooldown(slotMp, ConsumableType.MpPotion);

        SyncSlotCount(ConsumableType.HpPotion, slotHp, ref _lastHpCount);
        SyncSlotCount(ConsumableType.MpPotion, slotMp, ref _lastMpCount);
    }

    private void SyncSlotCount(ConsumableType type, CombatQuickSlot slot, ref int lastCount)
    {
        if (slot == null) return;

        var item  = PotionQuickSlotManager.instance.GetSlot(type);
        int count = item != null ? item.stackCount : -1; // -1 = 빈 슬롯

        if (count == lastCount) return;
        lastCount = count;

        if (type == ConsumableType.HpPotion) RefreshHpSlot();
        else                                  RefreshMpSlot();
    }

    private void RefreshHpSlot()
    {
        if (slotHp == null) return;
        var item = PotionQuickSlotManager.instance?.GetSlot(ConsumableType.HpPotion);
        slotHp.SetPotion(item);
        if (slotHp.keyText != null)
            slotHp.keyText.text = item != null ? $"x{item.stackCount}" : "";
    }

    private void RefreshMpSlot()
    {
        if (slotMp == null) return;
        var item = PotionQuickSlotManager.instance?.GetSlot(ConsumableType.MpPotion);
        slotMp.SetPotion(item);
        if (slotMp.keyText != null)
            slotMp.keyText.text = item != null ? $"x{item.stackCount}" : "";
    }

    private void UpdateCooldown(CombatQuickSlot slot, ConsumableType type)
    {
        float ratio     = PotionQuickSlotManager.instance.GetCooldownRatio(type);
        float remaining = PotionQuickSlotManager.instance.GetCooldownRemaining(type);
        slot.UpdateCooldown(ratio, remaining, 0f);
    }
}

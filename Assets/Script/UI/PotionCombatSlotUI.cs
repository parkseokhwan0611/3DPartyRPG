using UnityEngine;

public class PotionCombatSlotUI : MonoBehaviour
{
    [Header("# 포션 슬롯 (1: HP, 2: MP)")]
    public CombatQuickSlot slotHp;
    public CombatQuickSlot slotMp;

    void Start()
    {
        // 이벤트 구독은 OnEnable에서 처리됨 (Awake→OnEnable→Start 순서상 OnEnable이 항상 먼저 실행되므로
        // 여기서 다시 구독하면 이중 구독이 됨). 여기서는 초기 아이콘 표시만 갱신.
        RefreshHpSlot();
        RefreshMpSlot();
    }

    void OnEnable()
    {
        // Start 이후 비활성화→활성화 시 재구독 및 아이콘 갱신
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

    void OnDisable()
    {
        if (PotionQuickSlotManager.instance == null) return;
        PotionQuickSlotManager.instance.OnHpSlotChanged -= RefreshHpSlot;
        PotionQuickSlotManager.instance.OnMpSlotChanged -= RefreshMpSlot;
    }

    void Update()
    {
        if (PotionQuickSlotManager.instance == null) return;
        if (slotHp != null) UpdateCooldown(slotHp, ConsumableType.HpPotion);
        if (slotMp != null) UpdateCooldown(slotMp, ConsumableType.MpPotion);
    }

    private void RefreshHpSlot()
    {
        if (slotHp == null) return;
        var item = PotionQuickSlotManager.instance?.GetSlot(ConsumableType.HpPotion);
        slotHp.SetIcon(item?.data?.icon);
        if (slotHp.keyText != null)
            slotHp.keyText.text = item != null ? $"x{item.stackCount}" : "";
    }

    private void RefreshMpSlot()
    {
        if (slotMp == null) return;
        var item = PotionQuickSlotManager.instance?.GetSlot(ConsumableType.MpPotion);
        slotMp.SetIcon(item?.data?.icon);
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

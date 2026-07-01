using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ShopEntry
{
    public ItemData item;

    [Tooltip("true면 maxStock 수량까지만 구매 가능 (포션 등 무제한은 false)")]
    public bool hasStockLimit;

    [Tooltip("hasStockLimit = true일 때만 사용. 구매 가능 총 수량.")]
    public int maxStock = 1;
}

/// <summary>
/// NPC 상점 판매 목록 SO.
/// 가격은 ItemData.buyPrice 고정값 사용.
/// 런타임 재고는 NpcInteractable이 별도로 추적.
/// </summary>
[CreateAssetMenu(fileName = "ShopData", menuName = "Scriptable Object/ShopData")]
public class ShopData : ScriptableObject
{
    [Header("판매 목록")]
    public List<ShopEntry> entries = new List<ShopEntry>();
}

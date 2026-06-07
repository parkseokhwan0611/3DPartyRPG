using UnityEngine;

// ─────────────────────────────────────────────────────────────────
// 공통 열거형
// ─────────────────────────────────────────────────────────────────

public enum ItemGrade { Normal, Advanced, Elite, Legendary, Mythic }

// ─────────────────────────────────────────────────────────────────
// 베이스 SO
// ─────────────────────────────────────────────────────────────────

public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    public string itemName;
    public string description;
    public Sprite icon;
    public ItemGrade grade;
    public int       sellPrice;
}

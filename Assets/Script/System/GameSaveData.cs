using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────
// 세이브 파일 최상위 데이터
// ─────────────────────────────────────────────────────────────────

[System.Serializable]
public class GameSaveData
{
    public int partyLevel;
    public int partyExp;
    public int selectedPartyIndex;
    public int gold;
    public List<CharacterSaveData> characters = new List<CharacterSaveData>();
    public List<ItemSaveEntry>     inventory  = new List<ItemSaveEntry>();
    public List<EquipSaveEntry>    equipped   = new List<EquipSaveEntry>();

    // 포션 퀵슬롯
    public string hpPotionSlotItemId = "";
    public string mpPotionSlotItemId = "";

    // 씬 / 위치
    public string         sceneName      = "";
    public UnityEngine.Vector3 playerPosition;
}

// ─────────────────────────────────────────────────────────────────
// 캐릭터 1명분 세이브 데이터
// ─────────────────────────────────────────────────────────────────

[System.Serializable]
public class CharacterSaveData
{
    public float currentHp, currentMp;
    public int   statPoint, skillPoint;
    public float addedStr, addedVit, addedInt, addedFht;
    public List<SkillLevelEntry> skillLevels = new List<SkillLevelEntry>();
}

[System.Serializable]
public class SkillLevelEntry
{
    public string skillId;
    public int    level;
}

// ─────────────────────────────────────────────────────────────────
// 인벤토리 / 장착 아이템 세이브 데이터
// ─────────────────────────────────────────────────────────────────

[System.Serializable]
public class ItemSaveEntry
{
    public string itemId;
    public int    stackCount;
    public int    enhancementLevel;
}

[System.Serializable]
public class EquipSaveEntry
{
    public int      characterIndex;
    public EquipSlot slot;
    public string   itemId;
    public int      enhancementLevel;
}

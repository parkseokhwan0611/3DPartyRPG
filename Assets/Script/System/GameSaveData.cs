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
    public string                    sceneName      = "";
    public List<UnityEngine.Vector3> partyPositions = new List<UnityEngine.Vector3>();
    public UnityEngine.Vector3       cameraPosition;

    // 스킬 퀵슬롯 / 쿨타임
    public List<CharacterQuickSlotSave> quickSlots = new List<CharacterQuickSlotSave>();

    // NPC 재고
    public List<NpcStockSave> npcStocks = new List<NpcStockSave>();

    // 퀘스트 진행 상황
    public int currentQuestIndex = 0;
    public int currentQuestProgress = 0;
    public List<string> completedQuestIds = new List<string>();
}

// ─────────────────────────────────────────────────────────────────
// 스킬 퀵슬롯 세이브 데이터 (캐릭터 1명분)
// ─────────────────────────────────────────────────────────────────

[System.Serializable]
public class CharacterQuickSlotSave
{
    public string slot0 = "", slot1 = "", slot2 = "", slot3 = "";
    public float  cd0,   cd1,   cd2,   cd3;
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
// NPC 재고 세이브 데이터
// ─────────────────────────────────────────────────────────────────

[System.Serializable]
public class NpcStockSave
{
    public string           npcId;
    public List<NpcStockEntry> stocks = new List<NpcStockEntry>();
}

[System.Serializable]
public class NpcStockEntry
{
    public int entryIndex;
    public int remaining;
}

// ─────────────────────────────────────────────────────────────────
// 인벤토리 / 장착 아이템 세이브 데이터
// ─────────────────────────────────────────────────────────────────

[System.Serializable]
public class ItemSaveEntry
{
    public string           itemId;
    public int              stackCount;
    public int              enhancementLevel;
    public List<OptionBonus> enhancementBonuses = new List<OptionBonus>();
}

[System.Serializable]
public class EquipSaveEntry
{
    public int               characterIndex;
    public EquipSlot          slot;
    public string            itemId;
    public int               enhancementLevel;
    public List<OptionBonus> enhancementBonuses = new List<OptionBonus>();
}

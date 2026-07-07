using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StartItemEntry
{
    public ItemData item;
    [Min(1)]
    public int count = 1;
}

[System.Serializable]
public class StartEquipEntry
{
    [Tooltip("장착시킬 파티원 인덱스 (0=첫번째, 1=두번째, 2=세번째)")]
    public int characterIndex;
    public EquipItemData item;
}

public class DataManager : MonoBehaviour
{
    public static DataManager instance;

    public List<CharacterStatus>    partyStatuses   = new List<CharacterStatus>();
    public List<CharacterEquipment> partyEquipments = new List<CharacterEquipment>();
    public Inventory                sharedInventory = new Inventory();
    public List<ClassData>          baseDataList;
    public List<ClassSkillTree>     skillTrees;

    public int partyLevel = 1;
    public int partyExp   = 0;
    public int gold       = 0;

    // 스탯창·인벤토리창이 공유하는 선택된 파티원 인덱스
    public int selectedPartyIndex = 0;

    [Header("테스트 설정")]
    [Tooltip("게임 시작 시 각 캐릭터에게 지급할 스킬 포인트")]
    public int startSkillPoint = 1;

    [Header("시작 아이템")]
    [Tooltip("게임 시작 시 인벤토리에 지급할 아이템 목록 (count로 수량 설정)")]
    public List<StartItemEntry> startItems;
    [Tooltip("게임 시작 시 캐릭터에게 미리 장착시킬 아이템 목록")]
    public List<StartEquipEntry> startEquips;

    [Header("아이템 레지스트리")]
    [Tooltip("세이브/로드에 사용할 모든 아이템 SO 목록 (itemId 기준 조회)")]
    public List<ItemData> itemRegistry;

    public event System.Action OnLevelUp;
    public event System.Action OnExpGained;
    public event System.Action OnGoldChanged;
    // InitData / LoadSaveData 완료 후 발생 — CharacterStat 재바인딩용
    public event System.Action OnDataInitialized;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitData();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 초기화 (새 게임 시작 시에도 호출)
    // ─────────────────────────────────────────────────────────────────

    public void InitData()
    {
        if (baseDataList == null || baseDataList.Count == 0)
        {
            Debug.LogWarning("DataManager: baseDataList가 비어있습니다!");
            return;
        }

        // 전체 상태 초기화
        // partyLevel은 여기서 강제로 1로 되돌리지 않음 — 인스펙터에 설정된 값을 시작 레벨로 사용
        // (테스트용으로 미리 높은 레벨을 넣어두거나, 씬 재진입 시에도 유지되도록)
        partyStatuses.Clear();
        partyEquipments.Clear();
        sharedInventory    = new Inventory();
        partyExp           = 0;
        gold               = 0;
        selectedPartyIndex = 0;

        // 시작 아이템 지급
        if (startItems != null)
            foreach (var entry in startItems)
                if (entry?.item != null)
                    sharedInventory.TryAddItem(new ItemInstance(entry.item, Mathf.Max(1, entry.count)));

        // 파티원 초기화
        foreach (var baseData in baseDataList)
        {
            var newStatus = new CharacterStatus();
            newStatus.classData  = baseData;
            newStatus.charName   = baseData.name;
            newStatus.currentHp  = newStatus.MaxHp;
            newStatus.currentMp  = newStatus.MaxMp;
            newStatus.skillPoint = startSkillPoint;
            partyStatuses.Add(newStatus);
            partyEquipments.Add(new CharacterEquipment());
        }

        // 시작 장착 아이템 적용
        if (startEquips != null)
        {
            foreach (var entry in startEquips)
            {
                if (entry.item == null) continue;
                if (entry.characterIndex < 0 || entry.characterIndex >= partyEquipments.Count) continue;
                var inst = new ItemInstance(entry.item);
                partyEquipments[entry.characterIndex].Equip(inst);
                partyEquipments[entry.characterIndex].RecalculateStats(partyStatuses[entry.characterIndex]);
            }
        }

        OnDataInitialized?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────
    // 세이브 / 로드
    // ─────────────────────────────────────────────────────────────────

    public GameSaveData GetSaveData()
    {
        var save = new GameSaveData
        {
            partyLevel         = partyLevel,
            partyExp           = partyExp,
            selectedPartyIndex = selectedPartyIndex,
            gold               = gold,
        };

        // 캐릭터
        foreach (var status in partyStatuses)
        {
            var charSave = new CharacterSaveData
            {
                currentHp  = status.currentHp,
                currentMp  = status.currentMp,
                statPoint  = status.statPoint,
                skillPoint = status.skillPoint,
                addedStr   = status.addedStr,
                addedVit   = status.addedVit,
                addedInt   = status.addedInt,
                addedFht   = status.addedFht,
            };

            foreach (var kvp in status.skillLevels)
            {
                if (kvp.Key == null || string.IsNullOrEmpty(kvp.Key.skillId)) continue;
                charSave.skillLevels.Add(new SkillLevelEntry
                {
                    skillId = kvp.Key.skillId,
                    level   = kvp.Value,
                });
            }

            save.characters.Add(charSave);
        }

        // 인벤토리
        foreach (var item in sharedInventory.Items)
        {
            if (item?.data == null || string.IsNullOrEmpty(item.data.itemId)) continue;
            save.inventory.Add(new ItemSaveEntry
            {
                itemId             = item.data.itemId,
                stackCount         = item.stackCount,
                enhancementLevel   = item.enhancementLevel,
                enhancementBonuses = item.enhancementBonuses ?? new System.Collections.Generic.List<OptionBonus>(),
            });
        }

        // 장착 아이템
        for (int i = 0; i < partyEquipments.Count; i++)
        {
            foreach (var kvp in partyEquipments[i].Slots)
            {
                if (kvp.Value?.data == null || string.IsNullOrEmpty(kvp.Value.data.itemId)) continue;
                save.equipped.Add(new EquipSaveEntry
                {
                    characterIndex     = i,
                    slot               = kvp.Key,
                    itemId             = kvp.Value.data.itemId,
                    enhancementLevel   = kvp.Value.enhancementLevel,
                    enhancementBonuses = kvp.Value.enhancementBonuses ?? new System.Collections.Generic.List<OptionBonus>(),
                });
            }
        }

        return save;
    }

    public void LoadSaveData(GameSaveData save)
    {
        if (save == null || baseDataList == null) return;

        partyLevel         = save.partyLevel;
        partyExp           = save.partyExp;
        selectedPartyIndex = save.selectedPartyIndex;
        gold               = save.gold;

        partyStatuses.Clear();
        partyEquipments.Clear();
        sharedInventory = new Inventory();

        // 캐릭터 복원
        for (int i = 0; i < baseDataList.Count && i < save.characters.Count; i++)
        {
            var saved  = save.characters[i];
            var status = new CharacterStatus();
            status.classData  = baseDataList[i];
            status.charName   = baseDataList[i].name;
            status.currentHp  = saved.currentHp;
            status.currentMp  = saved.currentMp;
            status.statPoint  = saved.statPoint;
            status.skillPoint = saved.skillPoint;
            status.addedStr   = saved.addedStr;
            status.addedVit   = saved.addedVit;
            status.addedInt   = saved.addedInt;
            status.addedFht   = saved.addedFht;

            // 스킬 레벨 복원 — LevelUpSkill로 패시브 효과 재적용
            foreach (var entry in saved.skillLevels)
            {
                SkillData skill = FindSkillById(entry.skillId);
                if (skill == null) continue;
                for (int lv = 0; lv < entry.level; lv++)
                    status.LevelUpSkill(skill);
            }

            partyStatuses.Add(status);
            partyEquipments.Add(new CharacterEquipment());
        }

        // 인벤토리 복원
        foreach (var entry in save.inventory)
        {
            ItemData data = FindItemById(entry.itemId);
            if (data == null) continue;
            var inst = new ItemInstance(data, entry.stackCount);
            inst.enhancementLevel   = entry.enhancementLevel;
            inst.enhancementBonuses = entry.enhancementBonuses ?? new System.Collections.Generic.List<OptionBonus>();
            sharedInventory.TryAddItem(inst);
        }

        // 장착 아이템 복원
        foreach (var entry in save.equipped)
        {
            if (entry.characterIndex < 0 || entry.characterIndex >= partyEquipments.Count) continue;
            if (FindItemById(entry.itemId) is not EquipItemData equipData) continue;
            var inst = new ItemInstance(equipData);
            inst.enhancementLevel   = entry.enhancementLevel;
            inst.enhancementBonuses = entry.enhancementBonuses ?? new System.Collections.Generic.List<OptionBonus>();
            partyEquipments[entry.characterIndex].EquipToSlot(inst, entry.slot);
            partyEquipments[entry.characterIndex].RecalculateStats(partyStatuses[entry.characterIndex]);
        }

        OnDataInitialized?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────
    // 경험치 / 레벨업
    // ─────────────────────────────────────────────────────────────────

    public void AddExp(float exp)
    {
        partyExp += (int)exp;

        while (partyExp >= GetRequiredExp(partyLevel))
        {
            partyExp -= GetRequiredExp(partyLevel);
            partyLevel++;
            LevelUpAllMembers();
            OnLevelUp?.Invoke();
        }

        OnExpGained?.Invoke();
    }

    private void LevelUpAllMembers()
    {
        foreach (var status in partyStatuses)
        {
            status.statPoint  += 5;
            status.skillPoint += 1;
        }
    }

    public int GetRequiredExp(int level) => level * 100;

    // ─────────────────────────────────────────────────────────────────
    // 골드
    // ─────────────────────────────────────────────────────────────────

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        gold += amount;
        OnGoldChanged?.Invoke();
    }

    // 골드가 부족하면 false 반환
    public bool SpendGold(int amount)
    {
        if (amount <= 0 || gold < amount) return false;
        gold -= amount;
        OnGoldChanged?.Invoke();
        return true;
    }

    public ClassSkillTree GetSkillTree(ClassData.ClassType classType)
        => skillTrees.Find(t => t.classType == classType);

    // ─────────────────────────────────────────────────────────────────
    // ID 조회 헬퍼
    // ─────────────────────────────────────────────────────────────────

    public SkillData FindSkillById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var tree in skillTrees)
        {
            if (tree == null) continue;
            foreach (var list in new[] { tree.mainSkills, tree.subSkills, tree.passiveSkills })
                foreach (var skill in list)
                    if (skill != null && skill.skillId == id) return skill;
        }
        return null;
    }

    public ItemData FindItemById(string id)
    {
        if (string.IsNullOrEmpty(id) || itemRegistry == null) return null;
        return itemRegistry.Find(item => item != null && item.itemId == id);
    }
}

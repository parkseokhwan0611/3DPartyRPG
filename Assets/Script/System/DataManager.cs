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

// 씬 파일 이름(영문) → 화면에 표시할 이름. 미니맵/포탈/퀘스트 등 씬 이름을 플레이어에게 보여주는
// 모든 곳이 이 하나의 목록을 공유한다 (DontDestroyOnLoad라 씬이 바뀌어도 유지됨)
[System.Serializable]
public class SceneDisplayNameEntry
{
    public string sceneName;
    public string displayName;
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

    [Header("씬 표시 이름")]
    [Tooltip("씬 파일 이름(영문) → 화면에 표시할 이름 매핑. 목록에 없는 씬은 씬 이름을 그대로 표시")]
    public List<SceneDisplayNameEntry> sceneDisplayNames = new List<SceneDisplayNameEntry>();

    // 미니맵/포탈/퀘스트 UI가 공통으로 사용하는 조회 함수
    public string GetSceneDisplayName(string sceneName)
    {
        foreach (var entry in sceneDisplayNames)
        {
            if (entry != null && entry.sceneName == sceneName)
                return entry.displayName;
        }
        return sceneName;
    }

    // 실제 조작 중인 파티 리더의 partyMembers 리스트 인덱스 — PartyManager는 씬 로컬이라
    // 포탈 등으로 씬이 바뀌면 항상 0번(리스트 첫 캐릭터)으로 리더가 초기화됨. 여기(DataManager,
    // DontDestroyOnLoad)에 실시간으로 기록해뒀다가 새 씬 시작 시 그대로 이어받는다
    public int currentLeaderIndex = 0;

    // 새 게임 시작 시 true — 게임 씬 로드 후 PotionQuickSlotManager가 시작 포션을
    // 퀵슬롯에 자동 등록하는 데 사용되고, 등록 즉시 소비(false)됨. 로드 게임은
    // 세이브된 슬롯 정보로 복원되므로 이 플래그를 쓰지 않음
    public bool pendingAutoRegisterPotions = false;

    // PartyManager/SkillManager/PotionQuickSlotManager는 씬 로컬 컴포넌트라 포탈 등으로 씬이
    // 바뀌면 파괴 후 재생성됨 — DontDestroyOnLoad인 여기(DataManager)에 실시간으로 배정을
    // 들고 있다가 새 씬 진입 시 다시 적용한다. 아직 한 번도 등록 안 한 상태(빈 리스트)면
    // 프리팹 기본 스킬 슬롯을 그대로 사용
    public List<CharacterQuickSlotSave> partyQuickSlots = new List<CharacterQuickSlotSave>();
    public string hpPotionSlotItemId = "";
    public string mpPotionSlotItemId = "";

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
        // 다른 싱글톤들과 달리 중복 파괴 처리가 없었음 — 씬마다 DataManager를 배치해두면
        // 나중에 로드되는 쪽은 instance가 되지 못한 채 파괴되지 않고 살아남는 문제가 있었음.
        // 단, 이 오브젝트에는 QuestManager/AudioManager/SaveManager처럼 진짜 DontDestroyOnLoad로
        // 남아야 하는 컴포넌트와 PotionQuickSlotManager처럼 씬마다 새로 초기화돼야 하는 컴포넌트가
        // 함께 붙어있을 수 있음 — Destroy(gameObject)로 오브젝트 전체를 지우면 아직 자기 차례의
        // Awake를 실행하지 못한 형제 컴포넌트까지 함께 사라져버리므로, 이 컴포넌트 자신만 제거한다
        if (instance != null && instance != this) { Destroy(this); return; }

        instance = this;
        DontDestroyOnLoad(gameObject);
        InitData();
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

        // 새 게임에서는 퀵슬롯 배정을 비워둠 — 각 캐릭터 프리팹의 기본 슬롯을 그대로 사용
        partyQuickSlots.Clear();
        hpPotionSlotItemId = "";
        mpPotionSlotItemId = "";
        currentLeaderIndex = 0;

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

        pendingAutoRegisterPotions = true;
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

        partyLevel                 = save.partyLevel;
        partyExp                   = save.partyExp;
        selectedPartyIndex         = save.selectedPartyIndex;
        gold                       = save.gold;
        pendingAutoRegisterPotions = false;

        partyStatuses.Clear();
        partyEquipments.Clear();
        sharedInventory = new Inventory();

        // 퀵슬롯 배정 복원 — SaveManager.RestoreQuickSlots/RestorePotionSlots가 실제 SkillManager/
        // PotionQuickSlotManager에 적용하지만, 이후 씬 전환 시에도 유지되도록 여기에도 반영
        partyQuickSlots     = new List<CharacterQuickSlotSave>(save.quickSlots);
        hpPotionSlotItemId  = save.hpPotionSlotItemId;
        mpPotionSlotItemId  = save.mpPotionSlotItemId;

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
            // currentHp는 이미 세이브 파일의 최종값(saved.currentHp)으로 세팅돼 있으므로
            // 장비 복원으로 인한 최대체력 증가분을 또 더하면 중복 반영됨 — false로 스킵
            partyEquipments[entry.characterIndex].RecalculateStats(partyStatuses[entry.characterIndex], preserveHpDeficit: false);
        }

        OnDataInitialized?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────
    // 경험치 / 레벨업
    // ─────────────────────────────────────────────────────────────────

    public void AddExp(float exp)
    {
        partyExp += (int)exp;

        // 한 번에 여러 레벨을 올려도(대량 경험치 획득 등) 사운드/이펙트는 한 번만 재생
        bool leveledUp = false;
        while (partyExp >= GetRequiredExp(partyLevel))
        {
            partyExp -= GetRequiredExp(partyLevel);
            partyLevel++;
            LevelUpAllMembers();
            leveledUp = true;
        }

        if (leveledUp)
        {
            AudioManager.instance?.PlaySFX("LevelUp");
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

    [Header("경험치 계산식")]
    [Tooltip("필요 경험치 = baseExp + expGrowthRate * level^2")]
    public int   baseExp       = 50;
    public float expGrowthRate = 8f;

    public int GetRequiredExp(int level) => baseExp + Mathf.RoundToInt(expGrowthRate * level * level);

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
    // 스킬 퀵슬롯 배정 (씬 전환 후에도 유지되도록 실시간 보관)
    // ─────────────────────────────────────────────────────────────────

    // partyIndex에 해당하는 저장 항목이 아직 없으면 null — 이 경우 호출측은 프리팹 기본값 유지
    public CharacterQuickSlotSave GetQuickSlotSave(int partyIndex)
    {
        if (partyIndex < 0 || partyIndex >= partyQuickSlots.Count) return null;
        return partyQuickSlots[partyIndex];
    }

    public void SetQuickSlotSkillId(int partyIndex, int slotIndex, string skillId)
    {
        if (partyIndex < 0) return;

        // 리스트를 늘려야 할 때 사이 인덱스는 null로 채움 — 실제 빈 CharacterQuickSlotSave를
        // 넣으면 "한 번도 안 건드림"과 "명시적으로 다 지움"을 구분 못 해서, 예를 들어
        // 캐릭터2를 먼저 커스터마이징하면 한 번도 안 건드린 캐릭터0/1의 슬롯까지
        // 다음 씬에서 전부 빈 슬롯으로 취급돼버리는 버그가 있었음
        while (partyQuickSlots.Count <= partyIndex)
            partyQuickSlots.Add(null);

        if (partyQuickSlots[partyIndex] == null)
            partyQuickSlots[partyIndex] = new CharacterQuickSlotSave();

        var entry = partyQuickSlots[partyIndex];
        switch (slotIndex)
        {
            case 0: entry.slot0 = skillId; break;
            case 1: entry.slot1 = skillId; break;
            case 2: entry.slot2 = skillId; break;
            case 3: entry.slot3 = skillId; break;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // ID 조회 헬퍼
    // ─────────────────────────────────────────────────────────────────

    private Dictionary<string, SkillData> _skillByIdCache;
    private Dictionary<string, ItemData>  _itemByIdCache;

    public SkillData FindSkillById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (_skillByIdCache == null)
        {
            _skillByIdCache = new Dictionary<string, SkillData>();
            foreach (var tree in skillTrees)
            {
                if (tree == null) continue;
                foreach (var list in new[] { tree.mainSkills, tree.subSkills, tree.passiveSkills })
                    foreach (var skill in list)
                        if (skill != null && !_skillByIdCache.ContainsKey(skill.skillId))
                            _skillByIdCache[skill.skillId] = skill;
            }
        }

        return _skillByIdCache.TryGetValue(id, out var found) ? found : null;
    }

    public ItemData FindItemById(string id)
    {
        if (string.IsNullOrEmpty(id) || itemRegistry == null) return null;

        if (_itemByIdCache == null)
        {
            _itemByIdCache = new Dictionary<string, ItemData>();
            foreach (var item in itemRegistry)
                if (item != null && !_itemByIdCache.ContainsKey(item.itemId))
                    _itemByIdCache[item.itemId] = item;
        }

        return _itemByIdCache.TryGetValue(id, out var found) ? found : null;
    }
}

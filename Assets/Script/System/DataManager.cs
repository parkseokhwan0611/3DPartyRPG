using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    // 현재 파티원들의 실시간 정보를 담는 리스트나 사전(Dictionary)
    public List<CharacterStatus>    partyStatuses   = new List<CharacterStatus>();
    public List<CharacterEquipment> partyEquipments = new List<CharacterEquipment>();
    public Inventory                sharedInventory = new Inventory();
    public List<ClassData> baseDataList;
    public List<ClassSkillTree> skillTrees;
    
    public int partyLevel = 1;
    public int partyExp = 0;

    [Header("테스트 설정")]
    [Tooltip("게임 시작 시 각 캐릭터에게 지급할 스킬 포인트")]
    public int startSkillPoint = 1;

    [Header("시작 아이템")]
    [Tooltip("게임 시작 시 인벤토리에 지급할 아이템 목록")]
    public List<ItemData> startItems;
    [Tooltip("게임 시작 시 캐릭터에게 미리 장착시킬 아이템 목록")]
    public List<StartEquipEntry> startEquips;

    public event System.Action OnLevelUp;
    public event System.Action OnExpGained;

    void Awake() {
        if (instance == null) {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitData(); // 처음 시작할 때 SO로부터 값을 복사해옴
        }
    }
    public void InitData()
    {
        if (baseDataList == null || baseDataList.Count == 0)
        {
            Debug.LogWarning("DataManager: baseDataList가 비어있습니다!");
            return;
        }
        partyStatuses.Clear();
        partyEquipments.Clear();

        // 시작 아이템 지급
        sharedInventory = new Inventory();
        if (startItems != null)
        {
            foreach (var itemData in startItems)
                sharedInventory.TryAddItem(new ItemInstance(itemData));
        }

        foreach (var baseData in baseDataList)
        {
            CharacterStatus newStatus = new CharacterStatus();

            // 1. 핵심: SO(원본)를 먼저 할당해줘야 MaxHp, TotalAtk 계산식이 작동합니다.
            newStatus.classData = baseData;
            newStatus.charName  = baseData.name;

            // 2. 실시간 변수 초기화
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

                var instance = new ItemInstance(entry.item);
                partyEquipments[entry.characterIndex].Equip(instance);
                partyEquipments[entry.characterIndex].RecalculateStats(partyStatuses[entry.characterIndex]);
            }
        }
    }
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
            status.statPoint += 5; // 캐릭터당 5포인트
            status.skillPoint += 1;
        }
    }

    public int GetRequiredExp(int level)
    {
        return level * 100; // 나중에 공식 조정
    }

    public ClassSkillTree GetSkillTree(ClassData.ClassType classType)
    {
        return skillTrees.Find(t => t.classType == classType);
    }
}
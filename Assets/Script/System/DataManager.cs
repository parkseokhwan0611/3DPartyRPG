using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager instance;

    // 현재 파티원들의 실시간 정보를 담는 리스트나 사전(Dictionary)
    public List<CharacterStatus> partyStatuses = new List<CharacterStatus>();
    public List<ClassData> baseDataList;

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

        foreach (var baseData in baseDataList)
        {
            CharacterStatus newStatus = new CharacterStatus();
            
            // 1. 핵심: SO(원본)를 먼저 할당해줘야 MaxHp, TotalAtk 계산식이 작동합니다.
            newStatus.classData = baseData;
            newStatus.charName = baseData.name; // 혹은 ClassData에 name 변수 추가

            // 2. 실시간 변수 초기화
            // MaxHp는 이제 classData를 기반으로 자동 계산되므로 currentHp에 바로 대입 가능합니다.
            newStatus.currentHp = newStatus.MaxHp; 
            newStatus.currentMp = newStatus.MaxMp;
            
            partyStatuses.Add(newStatus);
        }
    }
}
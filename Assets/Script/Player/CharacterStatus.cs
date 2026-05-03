using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System; // Action을 사용하기 위해 필요

public class CharacterStatus
{
public string charName;
    public float currentHp;
    public float currentMp;

    // 이 캐릭터의 원본 데이터(SO)를 참조로 들고 있게 합니다.
    public ClassData classData;

    // 계산식들을 이쪽으로 옮겨옵니다.
    public float MaxHp => classData.hp + (classData.baseVit * classData.hpPerVit);
    public float MaxMp => classData.mp; // 필요시 공식 추가
    public float TotalAtk => classData.baseStr * classData.atkPerStr;
    public float TotalAp => (classData.baseInt * classData.apPerInt) + (classData.baseFht * classData.apPerFth);

    // 이벤트를 데이터 클래스에 넣으면 UI 업데이트가 더 쉬워집니다.
    public event Action OnHpChanged;
    public void RaiseHpChanged()
    {
        OnHpChanged?.Invoke();
    }
}

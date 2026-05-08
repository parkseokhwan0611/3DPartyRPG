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

    public int statPoint = 0;  // 개인 스탯 포인트
    public float addedStr = 0;
    public float addedVit = 0;
    public float addedInt = 0;
    public float addedFht = 0;

    public float MaxHp   => classData.hp + ((classData.baseVit + addedVit) * classData.hpPerVit);
    public float MaxMp => classData.mp; // 필요시 공식 추가
    public float TotalAtk => (classData.baseStr + addedStr) * classData.atkPerStr;
    public float TotalAp  => ((classData.baseInt + addedInt) * classData.apPerInt)
                        + ((classData.baseFht + addedFht) * classData.apPerFth);

    // 이벤트를 데이터 클래스에 넣으면 UI 업데이트가 더 쉬워집니다.
    public event Action OnHpChanged;
    public void RaiseHpChanged()
    {
        OnHpChanged?.Invoke();
    }
}

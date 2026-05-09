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

    // 패시브/아이템으로 누적되는 추가 수치
    public float addedCritRate   = 0f;
    public float addedCritDamage = 0f;

    // 최종 치명타 수치
    public float TotalCritRate   => classData.baseCritRate + addedCritRate;
    public float TotalCritDamage => classData.baseCritDamage + addedCritDamage;
    //방어력
    public float addedDef = 0f; // 아이템으로 추가되는 방어력

    public float TotalDef => ((classData.baseVit + addedVit) * classData.defPerVit) + addedDef;

    // 이벤트를 데이터 클래스에 넣으면 UI 업데이트가 더 쉬워집니다.
    public event Action OnHpChanged;
    public void RaiseHpChanged()
    {
        OnHpChanged?.Invoke();
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Class", menuName = "Scriptable Object/ClassData")]
public class ClassData : ScriptableObject
{
    public enum ClassType { Tanker, Dealer, Healer }
    public ClassType classType;
    public int level;
    public float hp;
    public float mp;
    public List<int> learnedSkillIds; // 배운 스킬 ID 리스트
    public List<int> inventoryItemIds; // 소지 아이템 ID 리스트
    public float baseStr;    // 기본 힘
    public float baseVit;    // 기본 체력
    public float baseInt;    // 기본 체력
    public float baseFht;    // 기본 체력
    public float atkPerStr;  // 힘 1당 증가할 ATK 계수
    public float hpPerVit;  // 체력 1당 증가할 HP 계수
    public float apPerInt;  // 지능 1당 증가할 AP 계수
    public float apPerFth;  // 신앙 1당 증가할 AP 계수
}

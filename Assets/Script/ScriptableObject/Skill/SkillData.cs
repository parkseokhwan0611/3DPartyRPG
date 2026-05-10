using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Skill", menuName = "Scriptable Object/SkillData")]
public class SkillData : ScriptableObject
{
    [Header("기본 정보")]
    public string skillName;
    public string description;
    public Sprite icon;
    public ClassData.ClassType requiredClass; // 사용 가능 직업
    public enum SkillType { Damage, Buff, Passive }
    public enum SkillCategory { Main, Sub, Passive }
    [Header("스킬 분류")]
    public SkillType skillType;         // 내부 로직용
    public SkillCategory skillCategory; // UI 배치용

    [Header("스킬 레벨")]
    public int maxLevel = 5;
    [Header("스킬 레벨업 비용")]
    public int[] skillPointCost; 

    [Header("공통 수치 (레벨별)")]
    public float[] mpCost;       // 레벨별 MP 소비
    public float[] cooldown;     // 레벨별 쿨다운
}
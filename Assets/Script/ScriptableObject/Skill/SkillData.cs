using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Skill", menuName = "Scriptable Object/SkillData")]
public class SkillData : ScriptableObject
{
    [Header("기본 정보")]
    public string skillName;
    public string description;
    public Sprite icon;
    public ClassData.ClassType requiredClass;

    [Header("스킬 분류")]
    public SkillType skillType;
    public SkillCategory skillCategory;

    [Header("스킬 레벨")]
    public int maxLevel = 5;
    public int requiredCharLevel;

    [Header("공통 수치 (레벨별)")]
    public float[] mpCost;
    public float[] cooldown;
    public int[] skillPointCost;

    public enum SkillType { Damage, Buff, Heal, Debuff, Passive }
    public enum SkillCategory { Main, Sub, Passive }
}
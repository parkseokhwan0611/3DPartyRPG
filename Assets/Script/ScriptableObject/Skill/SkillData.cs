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

    [Header("스킬 레벨")]
    public int maxLevel = 5;

    [Header("공통 수치 (레벨별)")]
    public float[] mpCost;       // 레벨별 MP 소비
    public float[] cooldown;     // 레벨별 쿨다운

    public enum SkillType { Damage, Buff, Passive }
    public SkillType skillType;
}
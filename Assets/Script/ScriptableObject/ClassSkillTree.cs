using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SkillTree", menuName = "Scriptable Object/ClassSkillTree")]
public class ClassSkillTree : ScriptableObject
{
    public ClassData.ClassType classType;

    [Header("Main 스킬 (6개 / 1, 10, 20, 30, 40, 50레벨)")]
    public List<SkillData> mainSkills = new List<SkillData>();

    [Header("Sub 스킬 (4개 / 1, 10, 30, 50레벨)")]
    public List<SkillData> subSkills = new List<SkillData>();

    [Header("Passive 스킬 (6개 / 1, 10, 20, 30, 40, 50레벨)")]
    public List<SkillData> passiveSkills = new List<SkillData>();
}
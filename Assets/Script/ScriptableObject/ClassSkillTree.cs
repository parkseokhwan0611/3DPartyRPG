using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillTree", menuName = "Scriptable Object/ClassSkillTree")]
public class ClassSkillTree : ScriptableObject
{
    public ClassData.ClassType classType;

    [System.Serializable]
    public class SkillTierData
    {
        public int requiredLevel;        // 이 티어 스킬 습득 가능 레벨
        public List<SkillData> primary;  // Primary 열 스킬
        public List<SkillData> secondary;// Secondary 열 스킬
        public List<SkillData> passive;  // Passive 열 스킬
    }

    public List<SkillTierData> tiers;
}
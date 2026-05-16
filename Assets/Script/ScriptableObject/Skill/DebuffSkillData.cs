using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "DebuffSkill", menuName = "Scriptable Object/DebuffSkillData")]
public class DebuffSkillData : SkillData
{
    [Header("디버프 설정")]
    public bool isAoe = false;
    public float aoeRange = 5f;

    [Header("애니메이션 / 이펙트")]
    public string animTriggerName;
    public float animDuration;
    public float effectSpawnDelay;
    public string effectPoolKey;

    [Header("디버프 효과 목록 (최대 3개)")]
    public List<DebuffEffect> debuffEffects = new List<DebuffEffect>();

    [System.Serializable]
    public class DebuffEffect
    {
        public StatusEffectType effectType;
        public float baseValue     = 0f;
        public float valuePerLevel = 0f;
        public float baseDuration     = 3f;
        public float durationPerLevel = 0.5f;

        public float GetValue(int level)    => baseValue + (valuePerLevel * (level - 1));
        public float GetDuration(int level) => baseDuration + (durationPerLevel * (level - 1));
    }
}
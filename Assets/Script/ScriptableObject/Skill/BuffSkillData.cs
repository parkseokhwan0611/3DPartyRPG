using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BuffSkill", menuName = "Scriptable Object/BuffSkillData")]
public class BuffSkillData : SkillData
{
    [Header("버프 설정")]
    public float baseDuration = 5f;
    public float durationPerLevel = 1f;
    public float baseAtkBonus = 0f;
    public float atkBonusPerLevel = 0f;
    public float baseApBonus = 0f;
    public float apBonusPerLevel = 0f;
    public float baseDefBonus = 0f;
    public float defBonusPerLevel = 0f;

    [Header("애니메이션 / 이펙트")]
    public string animTriggerName;
    public float animDuration;
    public float effectSpawnDelay;
    public string effectPoolKey;

    public float GetDuration(int level)    => baseDuration + (durationPerLevel * (level - 1));
    public float GetAtkBonus(int level)    => baseAtkBonus + (atkBonusPerLevel * (level - 1));
    public float GetApBonus(int level)     => baseApBonus + (apBonusPerLevel * (level - 1));
    public float GetDefBonus(int level)    => baseDefBonus + (defBonusPerLevel * (level - 1));
}
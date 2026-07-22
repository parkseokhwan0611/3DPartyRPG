using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class SkillSfxEntry
{
    [Tooltip("AudioManager에 등록한 SFX 키 (예: Tanker_Main1)")]
    public string sfxKey;
    [Tooltip("애니메이션 시작 후 몇 초 뒤에 재생할지")]
    public float delay = 0f;
}

[CreateAssetMenu(fileName = "Skill", menuName = "Scriptable Object/SkillData")]
public class SkillData : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("세이브/로드용 고유 ID — 절대 중복·변경 금지")]
    public string skillId;
    public string skillName;
    public string description;
    public Sprite icon;
    public ClassData.ClassType requiredClass;

    [Header("스킬 분류")]
    public SkillType skillType;
    public SkillCategory skillCategory;

    [Header("사운드")]
    [Tooltip("애니메이션 시작 시점을 기준으로, 각 사운드가 몇 초 뒤에 재생될지 지정")]
    public List<SkillSfxEntry> sfxEntries = new List<SkillSfxEntry>();

    [Header("팔로워 자동 사용 우선순위")]
    [Tooltip("숫자가 낮을수록 먼저 사용 (패시브 무시). 같은 우선순위면 랜덤 선택.")]
    public int skillPriority = 0;

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
using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class SkillSfxEntry
{
    [Tooltip("AudioManager에 등록한 SFX 키 (예: Tanker_Main1)")]
    public string sfxKey;
    [Tooltip("애니메이션 시작 후 몇 초 뒤에 재생할지 (반복 재생이면 첫 번째 재생 시점)")]
    public float delay = 0f;
    [Tooltip("재생 횟수 (1이면 한 번만). 예: 3이면 Delay 시점부터 Repeat Interval 간격으로 3번 재생")]
    [Min(1)] public int repeatCount = 1;
    [Tooltip("반복 재생 간격 (초). Repeat Count가 2 이상일 때만 사용")]
    [Min(0f)] public float repeatInterval = 0.5f;

    // 기존 에셋·새로 추가한 항목에서 0으로 들어와도 최소 1번은 재생
    public int PlayCount => Mathf.Max(1, repeatCount);
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

    // 쿨타임 초기화 효과를 가진 스킬인지 — 쿨 초기화 스킬끼리는 서로 초기화하지 않는다 (무한 연쇄 방지)
    public virtual bool IsCooldownResetSkill => false;

    // 에셋에 정수로 저장되므로 새 타입은 항상 맨 끝에 추가
    public enum SkillType { Damage, Buff, Heal, Debuff, Passive, Summon }
    public enum SkillCategory { Main, Sub, Passive }
}
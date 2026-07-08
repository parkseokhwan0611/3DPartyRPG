using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DamageSkill", menuName = "Scriptable Object/DamageSkillData")]
public class DamageSkillData : SkillData
{
    public enum ScalingStat
    {
        None, // 스탯 비례 없음
        Str,  // 힘
        Vit,  // 체력
        Int,  // 지능
        Fth,  // 신앙
    }

    [Header("데미지 설정")]
    public float baseDamageMultiplier = 1.0f;  // 기본 배율
    public float damageMultiplierPerLevel = 0.1f; // 레벨당 증가 배율
    public bool useAp; //True면 AP, False면 AD

    [System.Serializable]
    public class StatScaling
    {
        [Tooltip("비례할 스탯. None이면 비활성.")]
        public ScalingStat stat         = ScalingStat.None;
        [Tooltip("기본 계수 (예: 0.5 = 해당 스탯의 50%를 데미지에 추가)")]
        public float       coeff        = 0f;
        [Tooltip("레벨당 계수 증가")]
        public float       coeffPerLevel = 0f;

        public float GetScaling(int level) => coeff + (coeffPerLevel * (level - 1));
    }

    [Header("스탯 배율 (선택, 최대 2개)")]
    public List<StatScaling> statScalings = new List<StatScaling>();
    [Header("사거리 설정")]
    public float castRange = 3f;  
    public float baseRange = 3f;
    public float rangePerLevel = 0f;
    public bool isAoe;
    [Header("부가 디버프 효과 (선택)")]
    public List<DebuffSkillData.DebuffEffect> onHitDebuffs = new List<DebuffSkillData.DebuffEffect>();

    [Header("다음 스킬 연계 버프 (선택)")]
    public bool hasNextSkillBuff       = false;
    public float nextSkillDamageBonus  = 0.2f;  // 20% 증가
    public float nextSkillBuffDuration = 4f;    // 4초
    [Header("어그로 설정")]
    public bool  hasAggroEffect = false;
    public float aggroAmount    = 50f;
    public float aggroRange     = 10f;

    [Header("장판 설정")]
    [Tooltip("true = 타겟 위치에 이펙트 스폰, 풀 오브젝트의 SkillZone이 직접 데미지 판정")]
    public bool spawnAtTarget = false;
    [Tooltip("true = 한 번만 판정 / false = zoneDamageInterval마다 반복 판정")]
    public bool zoneHitOnce = false;
    [Tooltip("스폰 후 첫 판정까지의 대기 시간 (초). 베이가 W처럼 이펙트 먼저 보여주고 뒤늦게 데미지를 줄 때 사용")]
    public float zoneActivationDelay = 0f;
    [Tooltip("장판 데미지 틱 간격 (초). zoneHitOnce = false일 때만 사용")]
    public float zoneDamageInterval = 0.5f;
    [Tooltip("장판 최대 지속시간 (초). zoneHitOnce = false일 때, 이 시간이 지나면 SkillZone이 스스로 데미지 판정을 멈춤 (PoolableObject 설정 누락 시 무한 루프 방지용 안전장치)")]
    public float zoneDuration = 5f;

    [Header("애니메이션 / 이펙트")]
    public string animTriggerName;    // 애니메이터 트리거 이름
    public float animDuration;        // 애니메이션 전체 길이
    public float effectSpawnDelay;    // 이펙트 생성 타이밍 (선딜)
    public string effectPoolKey;
    public Vector3 effectSpawnOffset;   // 스폰 위치 오프셋 (로컬 스페이스)
    public Vector3 effectSpawnRotation; // 스폰 회전 오프셋 (오일러각, 로컬 스페이스)

    // 최종 수치 계산
    public float GetDamageMultiplier(int level)
        => baseDamageMultiplier + (damageMultiplierPerLevel * (level - 1));

    public float GetRange(int level)
        => baseRange + (rangePerLevel * (level - 1));

    // statScalings 전체 합산
    public float GetTotalStatBonus(int level, System.Func<ScalingStat, float> getStatValue)
    {
        float total = 0f;
        foreach (var s in statScalings)
        {
            if (s.stat == ScalingStat.None) continue;
            total += getStatValue(s.stat) * s.GetScaling(level);
        }
        return total;
    }
}
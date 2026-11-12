using UnityEngine;
using System.Collections.Generic;

// 소환사 스킬 — 소환수를 부르는 스킬(Summon)과, 불러둔 소환수에게 명령하는 스킬(Taunt·Strike)을 하나의 SO로 다룬다.
// 소환수 능력치는 시전자(루나리스) 능력치 × 비율이라 장비·버프·패시브가 자연스럽게 반영된다
[CreateAssetMenu(fileName = "SummonSkill", menuName = "Scriptable Object/SummonSkillData")]
public class SummonSkillData : SkillData
{
    public enum SummonAction
    {
        Summon, // 소환수 소환 (같은 스킬로 부른 소환수가 있으면 교체)
        Taunt,  // 지정 종류의 소환수 주변 몬스터 어그로를 소환수에게 끌어옴 (해당 소환수가 1기 이상 있어야 사용 가능)
        Strike, // 지정 종류의 소환수 주변 몬스터에게 데미지만 줌 — 어그로 없음 (해당 소환수가 1기 이상 있어야 사용 가능)
    }

    public SummonAction action = SummonAction.Summon;

    // 소환수에게 내리는 명령(도발·공격)인지 — 명령은 해당 종류 소환수가 있어야 쓸 수 있다
    public bool IsCommand => action == SummonAction.Taunt || action == SummonAction.Strike;

    [Tooltip("소환할 프리팹 — SummonUnit 컴포넌트가 붙어 있어야 함")]
    public SummonUnit summonPrefab;
    [Tooltip("레벨별 소환 수 (0번 칸 = 1레벨). 레벨이 배열보다 높으면 마지막 값 사용. 예: [1, 1, 2, 2, 2]")]
    public int[] summonCountByLevel = { 1 };
    [Tooltip("지속시간 (초)")]
    public float baseDuration     = 15f;
    public float durationPerLevel = 0f;
    [Tooltip("소환수 최대 체력 = 시전자 최대 체력 × 이 값 (0.5 = 50%). 소환 시점 기준으로 고정")]
    public float baseHpRatio     = 0.5f;
    public float hpRatioPerLevel = 0f;
    [Tooltip("소환수 공격력 = 시전자 마법 공격력(Use Ap 끄면 물리 공격력) × 이 값 (0.3 = 30%). 시전자 능력치가 바뀌면 바로 반영")]
    public float baseAtkRatio     = 0.3f;
    public float atkRatioPerLevel = 0f;
    [Tooltip("켜면 마법 공격력 기준 마법 피해, 끄면 물리 공격력 기준 물리 피해 (소환수 공격력·도발 데미지 공통)")]
    public bool useAp = true;
    [Tooltip("소환수 방어력·마법 저항력 = 시전자 방어력·마법 저항력 × 이 값 (1 = 100%)")]
    public float baseDefRatio     = 1f;
    public float defRatioPerLevel = 0f;

    // ── 명령 공통 (Taunt·Strike) — 필드 이름은 기존 에셋 호환을 위해 taunt* 그대로 유지 ──
    [Tooltip("명령을 받을 소환수 종류 (도발·공격 공통)")]
    public SummonUnit.Kind tauntKind = SummonUnit.Kind.Melee;
    [Tooltip("각 소환수 주변 이 반경(m) 안의 몬스터가 대상 (도발·공격 공통)")]
    public float tauntRadius = 6f;
    [Tooltip("명령 데미지 = (시전자 마법 공격력(Use Ap 끄면 물리 공격력) + 스탯 비례) × 이 값 (0.5 = 50%). " +
             "범위 안의 몬스터마다 한 번 (소환수가 여럿이라 범위가 겹쳐도 몬스터당 한 번만). " +
             "도발은 0이면 데미지 없이 어그로만, 공격(Strike)은 0이면 아무 효과 없음")]
    public float baseTauntDamageRatio     = 0f;
    public float tauntDamageRatioPerLevel = 0f;
    [Tooltip("명령 데미지에 더할 시전자 스탯 비례 (데미지 스킬의 Stat Scalings와 같은 방식). 예: 지능 × 0.5")]
    public List<DamageSkillData.StatScaling> tauntStatScalings = new List<DamageSkillData.StatScaling>();
    [Tooltip("명령을 받은 소환수 위치에 스폰할 이펙트 풀 키 (비우면 없음, 도발·공격 공통). 모션이 있으면 타격 시점에 스폰")]
    public string tauntEffectPoolKey;

    // ── 명령 모션 (소환수가 직접 동작) ──
    [Tooltip("명령을 받은 소환수가 재생할 애니메이터 Trigger (예: skill1). 비우면 모션 없이 시전 즉시 효과. " +
             "소환수 컨트롤러에 없는 트리거면 모션만 생략하고 타이밍은 그대로")]
    public string commandAnimTrigger;
    [Tooltip("모션 시작부터 이펙트·효과음까지 걸리는 시간 (초) — 보스 몬스터 스킬의 Windup Duration")]
    public float commandHitDelay = 0f;
    [Tooltip("이펙트·효과음 뒤 데미지가 들어가기까지의 시간 (초) — 보스 몬스터 스킬의 Attack Duration")]
    public float commandDamageDelay = 0f;
    [Tooltip("모션 전체 길이 (초) — 이 시간 동안 소환수는 이동·평타를 멈춘다. " +
             "보스 몬스터 스킬의 Windup + Attack + Recovery Duration 합")]
    public float commandMotionDuration = 0f;
    [Tooltip("모션이 끝났는데 아직 스킬 모션 중이면 이 상태로 끊어서 넘김 (보스 몬스터의 Recovery End State). 비우면 애니메이터 전이에 맡김")]
    public string commandEndState = "Idle";
    [Tooltip("이펙트 시점에 재생할 효과음 키 — 소환수가 여럿이어도 시전당 한 번 (비우면 없음)")]
    public string commandHitSfxKey;
    [Tooltip("이 소환 스킬로 부른 소환수에게만 명령 (비우면 받을 소환수 종류가 같은 소환수 전부). " +
             "같은 근접이라도 특정 소환수(예: 스켈레톤 보스)만 쓰게 할 때")]
    public SummonSkillData commandSourceSkill;

    // 이 소환수가 명령을 받을 대상인지 (종류 + 소환 스킬 제한)
    public bool IsCommandTarget(SummonUnit s, CharacterStat owner)
        => s != null && s.IsAlive && s.Owner == owner && s.kind == tauntKind
           && (commandSourceSkill == null || s.SourceSkill == commandSourceSkill);

    // ── 도발 전용 (Taunt) ──
    [Tooltip("[도발 전용] 몬스터에게 더할 어그로 수치 — 다른 어그로(탱커 도발 등)와 우선순위를 겨룰 때만 쓰인다. " +
             "도발이 얼마나 가는지는 아래 Taunt Duration이 정함")]
    public float baseTauntAggro     = 100f;
    public float tauntAggroPerLevel = 0f;
    [Tooltip("[도발 전용] 도발 지속시간 (초). 끝나면 도발로 준 어그로가 사라지고 몬스터는 원래 대상(가장 가까운 파티원 등)으로 돌아간다")]
    public float baseTauntDuration     = 5f;
    public float tauntDurationPerLevel = 0f;

    public string  animTriggerName;
    public float   animDuration;
    public float   effectSpawnDelay;
    public string  effectPoolKey;
    public Vector3 effectSpawnOffset;   // 스폰 위치 오프셋 (로컬 스페이스)
    public Vector3 effectSpawnRotation; // 스폰 회전 오프셋 (오일러각, 로컬 스페이스)

    public int GetSummonCount(int level)
    {
        if (summonCountByLevel == null || summonCountByLevel.Length == 0) return 1;
        return Mathf.Max(1, summonCountByLevel[Mathf.Clamp(level - 1, 0, summonCountByLevel.Length - 1)]);
    }

    public float GetDuration(int level)   => baseDuration  + durationPerLevel   * (level - 1);
    public float GetHpRatio(int level)    => baseHpRatio   + hpRatioPerLevel    * (level - 1);
    public float GetAtkRatio(int level)   => baseAtkRatio  + atkRatioPerLevel   * (level - 1);
    public float GetDefRatio(int level)   => baseDefRatio  + defRatioPerLevel   * (level - 1);
    public float GetTauntAggro(int level) => baseTauntAggro + tauntAggroPerLevel * (level - 1);
    public float GetTauntDuration(int level) => Mathf.Max(0.1f, baseTauntDuration + tauntDurationPerLevel * (level - 1));
    public float GetTauntDamageRatio(int level) => Mathf.Max(0f, baseTauntDamageRatio + tauntDamageRatioPerLevel * (level - 1));

    public float GetTauntStatBonus(int level, CharacterStat caster)
    {
        float total = 0f;
        if (caster == null || tauntStatScalings == null) return total;
        foreach (var s in tauntStatScalings)
        {
            if (s == null || s.stat == DamageSkillData.ScalingStat.None) continue;
            total += DamageSkillData.GetCasterStat(caster, s.stat) * s.GetScaling(level);
        }
        return total;
    }

    // 치명타 전 도발 데미지 = (공격력 + 스탯 비례) × 비율 × (1 + 물리/마법 데미지 증가) — 데미지 스킬(GetRawDamage)과 같은 공식
    public float GetTauntRawDamage(int level, CharacterStat caster)
    {
        float ratio = GetTauntDamageRatio(level);
        if (caster == null || ratio <= 0f) return 0f;
        float baseStat = useAp ? caster.TotalAp : caster.TotalAtk;
        float dmgBonus = useAp ? caster.MagicDmgBonus : caster.PhysDmgBonus;
        return (baseStat + GetTauntStatBonus(level, caster)) * ratio * (1f + dmgBonus);
    }

    // SkillManager는 skillType으로 실행 컴포넌트를 고르므로, 이 SO는 항상 Summon 타입으로 고정
    private void OnValidate() => skillType = SkillType.Summon;

    // 소환할 종류 (프리팹 기준) — 프리팹이 비어 있으면 근접으로 취급
    public SummonUnit.Kind SummonKind => summonPrefab != null ? summonPrefab.kind : SummonUnit.Kind.Melee;
}

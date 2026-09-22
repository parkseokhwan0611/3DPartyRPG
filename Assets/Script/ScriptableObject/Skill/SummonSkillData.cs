using UnityEngine;

// 소환사 스킬 — 소환수를 부르는 스킬(Summon)과, 불러둔 소환수에게 명령하는 스킬(Taunt)을 하나의 SO로 다룬다.
// 소환수 능력치는 시전자(루나리스) 능력치 × 비율이라 장비·버프·패시브가 자연스럽게 반영된다
[CreateAssetMenu(fileName = "SummonSkill", menuName = "Scriptable Object/SummonSkillData")]
public class SummonSkillData : SkillData
{
    public enum SummonAction
    {
        Summon, // 소환수 소환 (같은 스킬로 부른 소환수가 있으면 교체)
        Taunt,  // 지정 종류의 소환수 주변 몬스터 어그로를 소환수에게 끌어옴 (해당 소환수가 1기 이상 있어야 사용 가능)
    }

    [Header("동작")]
    public SummonAction action = SummonAction.Summon;

    [Header("소환 (Action = Summon)")]
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
    [Tooltip("켜면 마법 공격력 기준 마법 피해, 끄면 물리 공격력 기준 물리 피해")]
    public bool useAp = true;
    [Tooltip("소환수 방어력·마법 저항력 = 시전자 방어력·마법 저항력 × 이 값 (1 = 100%)")]
    public float baseDefRatio     = 1f;
    public float defRatioPerLevel = 0f;
    [Tooltip("시전자로부터 이 거리만큼 앞쪽 부채꼴로 나란히 소환")]
    public float spawnRadius = 1.5f;

    [Header("도발 (Action = Taunt)")]
    [Tooltip("명령을 받을 소환수 종류")]
    public SummonUnit.Kind tauntKind = SummonUnit.Kind.Melee;
    [Tooltip("각 소환수 주변 이 반경(m) 안의 몬스터가 대상")]
    public float tauntRadius = 6f;
    [Tooltip("몬스터에게 더할 어그로 수치. 몬스터마다 초당 Aggro Decay Rate(기본 5)씩 줄어들어서 100이면 약 20초 유지")]
    public float baseTauntAggro     = 100f;
    public float tauntAggroPerLevel = 0f;
    [Tooltip("도발한 소환수 위치에 스폰할 이펙트 풀 키 (비우면 없음)")]
    public string tauntEffectPoolKey;

    [Header("애니메이션 / 이펙트 (시전자)")]
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

    // SkillManager는 skillType으로 실행 컴포넌트를 고르므로, 이 SO는 항상 Summon 타입으로 고정
    private void OnValidate() => skillType = SkillType.Summon;

    // 소환할 종류 (프리팹 기준) — 프리팹이 비어 있으면 근접으로 취급
    public SummonUnit.Kind SummonKind => summonPrefab != null ? summonPrefab.kind : SummonUnit.Kind.Melee;
}

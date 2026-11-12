using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DamageSkill", menuName = "Scriptable Object/DamageSkillData")]
public class DamageSkillData : SkillData
{
    public enum ScalingStat
    {
        // 에셋에 정수로 저장되므로 번호는 고정 — 인스펙터 목록 순서만 선언 순서(힘·민첩·체력·지능·신앙)로
        None = 0, // 스탯 비례 없음
        Str  = 1, // 힘
        Dex  = 5, // 민첩 (나중에 추가돼 5번)
        Vit  = 2, // 체력
        Int  = 3, // 지능
        Fth  = 4, // 신앙
    }

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

    public List<StatScaling> statScalings = new List<StatScaling>();
    public float castRange = 3f;  
    public float baseRange = 3f;
    public float rangePerLevel = 0f;
    public bool isAoe;
    public List<DebuffSkillData.DebuffEffect> onHitDebuffs = new List<DebuffSkillData.DebuffEffect>();

    public List<CastBuffEffect> onCastBuffs = new List<CastBuffEffect>();

    [System.Serializable]
    public class CastBuffEffect
    {
        public StatusEffectType effectType;
        [Tooltip("AtkUp/ApUp/DefUp/MagicResUp/MaxHpUp, DmgReductionUp에서만 사용.\n" +
                 "Percent: 0.1 = 기본 수치의 +10% / DmgReductionUp은 받는 데미지 10% 감소\n" +
                 "Flat: 20 = +20 / DmgReductionUp은 한 대마다 20 감소 (최소 1은 들어감)")]
        public ModifierMode valueMode = ModifierMode.Flat;
        public float baseValue     = 0f;
        public float valuePerLevel = 0f;
        public float baseDuration     = 3f;
        public float durationPerLevel = 0.5f;

        [Tooltip("스탯 비례 계수 (시전자 기준, 선택). 최종 수치 = 기본 수치 + 스탯 × 계수\n" +
                 "예: Shield에 Vit·0.5 = 보호막 수치 + 체력 스탯의 50%. Percent 모드면 같은 단위(0.001 = 스탯 1당 +0.1%)로 더해짐")]
        public ScalingStat scalingStat     = ScalingStat.None;
        public float       scalingCoeff    = 0f; // 기본 계수 (예: 0.3 = 스탯의 30%)
        public float       scalingPerLevel = 0f; // 레벨당 계수 증가

        [Tooltip("체크하면 이 버프가 걸려 있는 동안 시전자에게 아우라를 켠다 (버프 스킬의 Aura 스타일과 같은 슬롯 사용)")]
        public bool useAura   = false;
        [Tooltip("Use Aura일 때 켤 아우라 — 시전자 CharacterStat의 Buff Auras 배열 인덱스")]
        public int  auraIndex = 0;

        public float GetValue(int level)    => baseValue + (valuePerLevel * (level - 1));
        public float GetDuration(int level) => baseDuration + (durationPerLevel * (level - 1));
        public float GetScaling(int level)  => scalingCoeff + (scalingPerLevel * (level - 1));

        // 스탯 비례분 — 시전자를 모르면(설명문 등) 0
        public float GetScalingAmount(int level, CharacterStat caster)
            => scalingStat == ScalingStat.None ? 0f : GetCasterStat(caster, scalingStat) * GetScaling(level);

        public float GetTotalValue(int level, CharacterStat caster) => GetValue(level) + GetScalingAmount(level, caster);

        // 퍼센트가 실제로 적용되는 효과인지 (그 외 타입은 valueMode와 무관)
        public bool IsPercent => valueMode == ModifierMode.Percent
                              && effectType is StatusEffectType.AtkUp or StatusEffectType.ApUp or StatusEffectType.DefUp
                                            or StatusEffectType.MagicResUp or StatusEffectType.MaxHpUp
                                            or StatusEffectType.DmgReductionUp;
    }

    public bool hasNextSkillBuff       = false;
    public float nextSkillDamageBonus  = 0.2f;  // 20% 증가
    public float nextSkillBuffDuration = 4f;    // 4초
    public bool  hasAggroEffect = false;
    public float aggroAmount    = 50f;
    public float aggroRange     = 10f;

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
    [Tooltip("체크하면 장판을 캐릭터가 바라보는 방향(수평)으로 돌려서 생성 — Effect Spawn Offset·Rotation도 그 방향 기준.\n" +
             "해제(기본)면 지금처럼 월드 기준 (Effect Spawn Rotation만 적용)")]
    public bool zoneFaceCaster = false;

    [Tooltip("true = 포물선으로 던져 대상의 발밑에 떨어진 뒤 범위 폭발 (몬스터 수류탄과 같은 GrenadeProjectile 사용). 장판형보다 우선\n" +
             "· Effect Pool Key = 던질 투척물 프리팹의 풀 키 (GrenadeProjectile 컴포넌트 필요)\n" +
             "· 폭발 이펙트·효과음 = 그 프리팹의 GrenadeProjectile에 있는 Explosion Pool Key / Explosion Sfx Key\n" +
             "· Effect Spawn Offset = 던지는 위치 (시전자 기준, 예: 손 높이 y = 1)\n" +
             "· 폭발 반경 = 사거리 설정의 Base Range (+ Range Per Level)\n" +
             "· 착지 지점은 던지는 순간의 대상 위치로 고정 — 날아가는 동안 대상이 움직이면 빗나갈 수 있음")]
    public bool  throwGrenade          = false;
    [Tooltip("목표 지점까지 날아가는 시간 (초)")]
    public float grenadeFlightDuration = 0.8f;
    [Tooltip("포물선 최고 높이 (m)")]
    public float grenadeArcHeight      = 3f;

    [Tooltip("true = 투사체를 날려서 맞는 순간 데미지 (수류탄형 다음, 장판형보다 우선)\n" +
             "· Effect Pool Key = 투사체 프리팹의 풀 키 (ProjectileScript + Rigidbody + Collider 필요 — 평타 총알 프리팹 그대로 써도 됨)\n" +
             "· 발사 위치 = 평타 총구(RangedAttack의 클래스별 발사 위치), 없으면 Effect Spawn Offset\n" +
             "· 한 발마다 데미지 공식 전체가 들어가고 치명타도 발마다 따로 굴림 (3발 다 맞으면 3배)\n" +
             "· 총구 섬광은 투사체 프리팹의 Muzzle Flash가 있으면 발마다 재생")]
    public bool  fireProjectile        = false;
    [Tooltip("발사할 투사체 개수")]
    [Min(1)] public int projectileCount = 1;
    [Tooltip("발사 간격 (초). 0 = 전부 동시에 발사, 0.1 = 0.1초마다 한 발씩 연사")]
    [Min(0f)] public float projectileInterval    = 0f;
    [Tooltip("퍼짐 각도 (도). 첫 발~마지막 발 사이의 전체 각도 — 0 = 전부 같은 방향, 30 = 좌우 15도씩 부채꼴")]
    [Min(0f)] public float projectileSpreadAngle = 0f;
    [Tooltip("발사할 때마다 재생할 AudioManager SFX 키 (비우면 없음). 연사면 한 발마다, 동시 발사면 한 번만 재생.\n" +
             "위의 사운드(Sfx Entries)는 모션 시작 기준으로 한 번만 나므로, 총소리는 여기에 넣고 Sfx Entries에서는 빼기")]
    public string projectileShotSfxKey;
    [Tooltip("투사체가 맞았을 때 데미지를 누가 주는지\n" +
             "· Projectile: 직접 맞은 대상 하나만 (기존 방식). 프리팹의 '맞으면 폭발'이 켜져 있으면 폭발 이펙트는 연출로만 나옴\n" +
             "· Explosion: 맞은 지점의 폭발 범위(Base Range) 안 적 전부\n" +
             "· Both: 직접 맞은 대상 + 폭발 범위 안 나머지 적 (직접 맞은 대상은 한 번만)\n" +
             "폭발 이펙트는 투사체 프리팹(ProjectileScript)의 Explosion Pool Key — 비우면 이펙트 없이 판정만")]
    public ProjectileScript.HitDamageMode projectileHitDamage = ProjectileScript.HitDamageMode.Projectile;

    public enum ReappearPosition
    {
        Origin,        // 숨었던 자리 그대로
        FrontOfTarget, // 대상 앞 (시전자가 있던 쪽)
        BehindTarget,  // 대상 뒤 (관통해서 지나간 느낌)
    }

    [Tooltip("true = 시전 순간부터 무적, 이펙트가 나오는 동안 시전자 모습을 감췄다가 끝나면 다시 나타남\n" +
             "· 숨는 시점 = 모션 시작 후 Vanish Start Time초, 나타나는 시점 = 그로부터 Vanish Duration초 뒤\n" +
             "· 시전~다시 나타날 때까지 무적(데미지·디버프·스턴 무시)이고, 다른 스킬로 끊을 수 없음\n" +
             "· 모습만 감출 뿐 몬스터 어그로는 그대로 (계속 이 캐릭터를 노리지만 데미지는 0)\n" +
             "· 스킬이 끝날 때(Anim Duration)까지 이동·공격 명령 불가, 파티원 교체·포탈·NPC·아이템 줍기도 불가\n" +
             "· 시전자 아래 렌더러(몸·무기·그림자·아우라)를 전부 끔 — 스크립트·애니메이터는 계속 동작\n" +
             "· 데미지는 위의 장판 설정(Spawn At Target + 틱 간격)으로 여러 번 주는 걸 권장\n" +
             "· Anim Duration(전체 길이)은 나타난 뒤 모션까지 포함한 길이로 — 나타나기 전엔 끝나지 않음")]
    public bool  vanishCaster   = false;
    [Min(0f)] public float vanishStartTime = 0.2f;
    [Min(0f)] public float vanishDuration  = 1.5f;
    [Tooltip("다시 나타날 위치. 대상이 사라졌으면 마지막으로 본 대상 위치 기준")]
    public ReappearPosition reappearPosition = ReappearPosition.Origin;
    [Tooltip("FrontOfTarget / BehindTarget일 때 대상 중심에서 떨어진 거리 (m)")]
    [Min(0f)] public float reappearDistance = 1.5f;
    [Tooltip("나타나는 순간 재생할 애니메이터 트리거 (마무리 베기 등). 비우면 없음")]
    public string reappearAnimTrigger;

    public float VanishEndTime => vanishStartTime + vanishDuration;

    // 범위 판정 스킬인지 (설명문 [광역] 표기용) — 수류탄형은 항상 범위 폭발, 장판형은 범위 안의 적 전부 판정
    // (투사체·수류탄이 장판형보다 우선하므로 그 경우의 장판 설정은 무시)
    public bool IsZone       => spawnAtTarget && !throwGrenade && !fireProjectile;
    public bool IsAreaDamage => isAoe || throwGrenade || IsZone || IsExplodingProjectile;
    [Tooltip("대상에게 걸린 디버프 1개당 데미지 증가 (0.25 = +25%, 0이면 없음).\n" +
             "맞는 순간 그 대상의 디버프 수로 대상마다 따로 계산 — 단일·광역·장판(틱마다)·투사체·폭발·수류탄 모두 적용.\n" +
             "디버프는 종류 기준으로 셈 (같은 디버프 중복은 1개, 기절 포함). 이 스킬이 거는 디버프는 데미지 뒤에 걸려서 안 셈")]
    public float debuffBonusPerStack = 0f;
    [Tooltip("레벨당 디버프 1개당 증가량 추가 (0.05 = 레벨마다 +5%p)")]
    public float debuffBonusPerLevel = 0f;
    [Tooltip("최대 몇 개까지 셀지 (0 = 제한 없음)")]
    [Min(0)] public int maxDebuffStacks = 0;

    public float GetDebuffBonusPerStack(int level) => Mathf.Max(0f, debuffBonusPerStack + debuffBonusPerLevel * (level - 1));
    public bool  HasDebuffBonus(int level)         => GetDebuffBonusPerStack(level) > 0f;
    public float GetDebuffMultiplier(int level, EnemyHp target)
        => target != null ? target.DebuffDamageMultiplier(GetDebuffBonusPerStack(level), maxDebuffStacks) : 1f;

    // 맞으면 폭발 범위 판정을 하는 투사체 스킬인지 (수류탄형이 투사체형보다 우선)
    public bool IsExplodingProjectile
        => fireProjectile && !throwGrenade && projectileHitDamage != ProjectileScript.HitDamageMode.Projectile;

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

    // 치명타·연계 보너스 적용 전 데미지 = (공격력 + 스탯 배율) × 스킬 계수 × (1 + 물리/마법 데미지 증가 [+ 메인 스킬 데미지 증가])
    // DamageSkill 시전과 치명타 번개 패시브(procSkill)가 같은 공식을 쓰도록 여기로 모음.
    // 치명타 번개는 패시브 발동이라 메인 스킬 데미지 증가 버프를 받지 않는다 (includeMainBonus = false)
    public float GetRawDamage(int level, CharacterStat caster, bool includeMainBonus = true)
    {
        if (caster == null) return 0f;
        float baseStat  = useAp ? caster.TotalAp : caster.TotalAtk;
        float statBonus = GetTotalStatBonus(level, s => GetCasterStat(caster, s));
        float dmgBonus  = (useAp ? caster.MagicDmgBonus : caster.PhysDmgBonus)
                        + (includeMainBonus ? GetMainSkillDmgBonus(caster) : 0f);
        return (baseStat + statBonus) * GetDamageMultiplier(level) * (1f + dmgBonus);
    }

    // 메인 스킬일 때만 받는 메인 스킬 데미지 증가 버프 수치
    public float GetMainSkillDmgBonus(CharacterStat caster)
        => caster != null && skillCategory == SkillCategory.Main ? caster.MainSkillDmgBonus : 0f;

    public static float GetCasterStat(CharacterStat caster, ScalingStat stat)
    {
        if (caster == null) return 0f;
        return stat switch
        {
            ScalingStat.Str => caster.TotalStr,
            ScalingStat.Dex => caster.TotalDex,
            ScalingStat.Vit => caster.TotalVit,
            ScalingStat.Int => caster.TotalInt,
            ScalingStat.Fth => caster.TotalFth,
            _               => 0f,
        };
    }
}
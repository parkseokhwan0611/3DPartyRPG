using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Class", menuName = "Scriptable Object/ClassData")]
public class ClassData : ScriptableObject
{
    [Tooltip("대화창 등에 표시할 캐릭터 이름. 비워두면 이 에셋의 파일명을 그대로 사용")]
    public string displayName;

    // 캐릭터 3명 × 무기 2종. 에셋에는 정수로 저장되므로 순서를 바꾸지 말 것
    // 아그니우스: Tanker(검방) / Warrior(딜러), 솔라리스: Mage / GunSlinger, 루나리스: Healer / Summoner
    // (Summoner는 예전 UtilMage 자리 — 값 5 그대로라 기존 에셋은 자동으로 Summoner가 됨)
    public enum ClassType { Tanker, Warrior, Mage, GunSlinger, Healer, Summoner }
    public ClassType classType;
    public int level;
    public float hp;
    public float mp;
    public List<int> learnedSkillIds; // 배운 스킬 ID 리스트
    public List<int> inventoryItemIds; // 소지 아이템 ID 리스트
    // 필드 순서는 인스펙터 표시 순서일 뿐 — 에셋은 이름으로 저장되므로 순서를 바꿔도 값이 유지된다
    [Header("스탯 — 기본 수치")]
    [Tooltip("기본 힘")]   public float baseStr;
    [Tooltip("기본 민첩")] public float baseDex;
    [Tooltip("기본 체력")] public float baseVit;
    [Tooltip("기본 지능")] public float baseInt;
    [Tooltip("기본 신앙")] public float baseFht;

    [Header("스탯 — 1당 증가량")]
    [Tooltip("힘 1당 물리 공격력")]                    public float atkPerStr;
    [Tooltip("민첩 1당 물리 공격력")]                  public float atkPerDex;
    [Tooltip("민첩 1당 치명타 확률 (0.001 = 0.1%)")]   public float critRatePerDex;
    [Tooltip("체력 1당 최대 체력")]                    public float hpPerVit;
    [Tooltip("체력 1당 방어력")]                       public float defPerVit;
    [Tooltip("체력 1당 초당 체력 재생")]               public float hpRegenPerVit = 0f;
    [Tooltip("지능 1당 마법 공격력")]                  public float apPerInt;
    [Tooltip("신앙 1당 마법 공격력")]                  public float apPerFth;
    [Tooltip("신앙 1당 초당 마나 재생")]               public float mpRegenPerFth = 0f;

    [Header("치명타")]
    [Tooltip("기본 치명타 확률 (0.05 = 5%). 최종 치명타 확률은 100%를 넘지 않음")]
    public float baseCritRate   = 0.05f;
    [Tooltip("치명타 데미지 배율 (1.5 = 150%)")]
    public float baseCritDamage = 1.5f;
    [Header("마법 저항력")]
    public float baseMagicRes = 0f;

    [Header("이동속도")]
    public float baseMoveSpeed = 3f;

    [Header("HP / MP 재생")]
    public float baseHpRegen    = 0f;  // 초당 기본 HP 재생
    public float baseMpRegen    = 0f;  // 초당 기본 MP 재생

    // ─────────────────────────────────────────────────────────────────
    // 무기(클래스)별 외형·평타 — 파티원 평타 설정은 전부 여기서 관리 (씬의 공격 컴포넌트에는 없음)
    // ─────────────────────────────────────────────────────────────────

    // 에셋에 정수로 저장되므로 값 고정 (0은 예전 "프리팹 기본값" 자리라 비워둠)
    public enum BasicAttackDamage
    {
        Physical = 1,  // 물리 공격력 기준 물리 데미지 (탱커, 워리어, 건슬링어)
        Magic    = 2,  // 마법 공격력 기준 마법 데미지
    }

    [Header("외형")]
    [Tooltip("이 클래스를 고르면 캐릭터 Animator에 적용할 컨트롤러 (비우면 씬에 배치된 그대로).\n" +
             "무기 오브젝트는 캐릭터의 ClassWeaponSwitcher에서 지정")]
    public RuntimeAnimatorController animatorController;

    [Header("기본 공격")]
    public BasicAttackDamage basicAttackDamage = BasicAttackDamage.Physical;
    [Tooltip("평타 효과음 키 (AudioManager, 비우면 소리 없음)")]
    public string normalAttackSfxKey;
    [Tooltip("근접 평타 히트 이펙트 풀 키 (MeleeAttack 전용, 비우면 없음)")]
    public string meleeHitEffectKey;
    [Tooltip("원거리 평타 투사체 풀 키 (RangedAttack 전용)")]
    public string projectilePoolKey;
    [Tooltip("평타 사거리")]
    public float attackRange = 2f;
    [Tooltip("공격속도 배율. 공격 간격 = Attack Duration ÷ Attack Speed (1이면 Attack Duration 그대로)")]
    public float attackSpeed = 1f;

    [Header("기본 공격 타이밍 (애니메이터 모션 길이에 맞춤)")]
    [Tooltip("공격 간격 (초) — 보통 평타 모션 길이")]
    public float attackDuration = 1f;
    [Tooltip("모션 시작 후 타격(근접)·발사(원거리)까지 걸리는 시간 (초)")]
    public float damageDelay = 0.33f;
    [Tooltip("타격·발사 뒤 후딜레이 (초) — 끝나야 이동을 재개. 보통 모션 길이 - Damage Delay")]
    public float recoveryDuration = 0.3f;

    [Header("근접 판정 (MeleeAttack 전용)")]
    [Tooltip("판정 구 반지름")]
    public float meleeHitRadius = 1.5f;
    [Tooltip("캐릭터 앞쪽으로 판정 구를 얼마나 떨어뜨릴지")]
    public float meleeHitOffset = 1f;
}

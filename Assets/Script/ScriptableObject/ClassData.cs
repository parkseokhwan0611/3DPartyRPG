using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Class", menuName = "Scriptable Object/ClassData")]
public class ClassData : ScriptableObject
{
    [Tooltip("대화창 등에 표시할 캐릭터 이름. 비워두면 이 에셋의 파일명을 그대로 사용")]
    public string displayName;

    // 캐릭터 3명 × 무기 2종. 에셋에는 정수로 저장되므로 순서를 바꾸지 말 것
    // 아그니우스: Tanker(검방) / Warrior(딜러), 솔라리스: Mage / GunSlinger, 루나리스: Healer / UtilMage
    public enum ClassType { Tanker, Warrior, Mage, GunSlinger, Healer, UtilMage }
    public ClassType classType;
    public int level;
    public float hp;
    public float mp;
    public List<int> learnedSkillIds; // 배운 스킬 ID 리스트
    public List<int> inventoryItemIds; // 소지 아이템 ID 리스트
    public float baseStr;    // 기본 힘
    public float baseVit;    // 기본 체력
    public float baseInt;    // 기본 체력
    public float baseFht;    // 기본 체력
    public float atkPerStr;  // 힘 1당 증가할 ATK 계수
    public float hpPerVit;  // 체력 1당 증가할 HP 계수
    public float defPerVit;  // 체력 1당 증가할 HP 계수
    public float apPerInt;  // 지능 1당 증가할 AP 계수
    public float apPerFth;  // 신앙 1당 증가할 AP 계수
    [Header("치명타")]
    public float baseCritRate   = 0.05f;
    public float baseCritDamage = 1.5f;
    [Header("마법 저항력")]
    public float baseMagicRes = 0f;

    [Header("이동속도")]
    public float baseMoveSpeed = 3f;

    [Header("HP / MP 재생")]
    public float baseHpRegen    = 0f;  // 초당 기본 HP 재생
    public float hpRegenPerVit  = 0f;  // VIT 1당 초당 HP 재생
    public float baseMpRegen    = 0f;  // 초당 기본 MP 재생
    public float mpRegenPerFth  = 0f;  // FTH 1당 초당 MP 재생

    // ─────────────────────────────────────────────────────────────────
    // 무기(클래스)별 외형·평타 — 비워두거나 0이면 캐릭터 프리팹에 설정된 값을 그대로 사용
    // ─────────────────────────────────────────────────────────────────

    public enum BasicAttackDamage
    {
        PrefabDefault, // 공격 컴포넌트 기본값 (근접 = 물리, 원거리 = 마법)
        Physical,      // 물리 공격력 기준 물리 데미지 (예: 건슬링어)
        Magic,         // 마법 공격력 기준 마법 데미지
    }

    [Header("외형 (비우면 프리팹 그대로)")]
    [Tooltip("이 클래스를 고르면 캐릭터 Animator에 적용할 컨트롤러. 무기 오브젝트는 캐릭터 프리팹의 ClassWeaponSwitcher에서 지정")]
    public RuntimeAnimatorController animatorController;

    [Header("기본 공격 (비우거나 0이면 프리팹 값)")]
    public BasicAttackDamage basicAttackDamage = BasicAttackDamage.PrefabDefault;
    [Tooltip("평타 효과음 키 (AudioManager)")]
    public string normalAttackSfxKey;
    [Tooltip("근접 평타 히트 이펙트 풀 키 (MeleeAttack 전용)")]
    public string meleeHitEffectKey;
    [Tooltip("원거리 평타 투사체 풀 키 (RangedAttack/HealerAttack 전용)")]
    public string projectilePoolKey;
    [Tooltip("평타 사거리")]
    public float attackRange = 0f;
    [Tooltip("공격속도 (공격 간격 = attackDuration ÷ 공격속도)")]
    public float attackSpeed = 0f;
}

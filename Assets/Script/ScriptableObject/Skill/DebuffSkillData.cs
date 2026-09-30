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
    public Vector3 effectSpawnOffset;   // 스폰 위치 오프셋 (로컬 스페이스)
    public Vector3 effectSpawnRotation; // 스폰 회전 오프셋 (오일러각, 로컬 스페이스)

    [Header("디버프 효과 목록 (최대 3개)")]
    public List<DebuffEffect> debuffEffects = new List<DebuffEffect>();

    [System.Serializable]
    public class DebuffEffect
    {
        public StatusEffectType effectType;
        [Tooltip("AtkDown/DefDown에서만 선택 가능 (그 외: Slow·MoveSpeedDown은 항상 퍼센트, Poison은 항상 고정값)\n" +
                 "Percent: 현재 수치 기준 감소 (0.2 = -20%, 최대 99%)\n" +
                 "Flat: 수치 그대로 감소 (20 = -20, 0 미만으로는 안 내려감)")]
        public ModifierMode valueMode = ModifierMode.Percent; // 기존 에셋이 전부 퍼센트 기준이라 기본값 Percent
        public float baseValue     = 0f;
        public float valuePerLevel = 0f;
        public float baseDuration     = 3f;
        public float durationPerLevel = 0.5f;

        public float GetValue(int level)    => baseValue + (valuePerLevel * (level - 1));
        public float GetDuration(int level) => baseDuration + (durationPerLevel * (level - 1));

        // 실제로 적용되는 방식 — 고정값 선택을 지원하지 않는 타입은 valueMode와 무관하게 원래 방식 유지
        public ModifierMode EffectiveMode => SupportsFlat(effectType) ? valueMode : ModifierMode.Percent;
        public bool IsFlat => EffectiveMode == ModifierMode.Flat;
    }

    public static bool SupportsFlat(StatusEffectType type)
        => type == StatusEffectType.AtkDown || type == StatusEffectType.DefDown;
}
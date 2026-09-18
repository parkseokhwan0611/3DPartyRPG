using UnityEditor;

// 패시브 효과 타입에 따라 Value Mode를 보여주거나 숨긴다.
// Value Mode는 능력치 5종(Atk/Ap/Def/MagicRes/MaxHp)과 받는 데미지 감소 2종에서만 동작하므로,
// 다른 타입에서 Flat을 골라도 무시되는 착각을 막기 위해 해당 타입에선 숨기고 단위 안내를 대신 띄운다.
[CustomEditor(typeof(PassiveSkillData))]
public class PassiveSkillDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var data = (PassiveSkillData)target;
        bool supportsMode       = PassiveSkillData.SupportsValueMode(data.effectType);
        bool secondSupportsMode = PassiveSkillData.SupportsValueMode(data.secondEffectType);

        SerializedProperty prop = serializedObject.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (prop.name == "valueMode" && !supportsMode) continue;

            // 두 번째 효과 세부 항목은 토글이 켜져 있을 때만
            bool isSecondDetail = prop.name == "secondEffectType" || prop.name == "secondValueMode"
                               || prop.name == "secondBaseValue"  || prop.name == "secondValuePerLevel";
            if (isSecondDetail && !data.hasSecondEffect) continue;
            if (prop.name == "secondValueMode" && !secondSupportsMode) continue;

            using (new EditorGUI.DisabledScope(prop.name == "m_Script"))
                EditorGUILayout.PropertyField(prop, true);

            if (prop.name == "effectType")
                DrawUnitNote(data.effectType);

            if (prop.name == "secondEffectType")
            {
                if (!PassiveSkillData.IsValueEffect(data.secondEffectType))
                    EditorGUILayout.HelpBox("두 번째 효과에는 수치 증가형 타입만 쓸 수 있습니다. 이 설정은 무시됩니다.",
                                            MessageType.Warning);
                else
                    DrawUnitNote(data.secondEffectType);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawUnitNote(PassiveSkillData.PassiveEffectType type)
    {
        string unitNote = GetUnitNote(type);
        if (!string.IsNullOrEmpty(unitNote))
            EditorGUILayout.HelpBox(unitNote, MessageType.Info);
    }

    // Value Mode가 없는 타입의 수치 단위 안내
    private static string GetUnitNote(PassiveSkillData.PassiveEffectType type)
    {
        switch (type)
        {
            case PassiveSkillData.PassiveEffectType.PhysDmgBonus:
            case PassiveSkillData.PassiveEffectType.MagicDmgBonus:
                return "최종 피해 퍼센트 증가 전용입니다. 0.1 = +10%\n고정 수치로 올리려면 Atk / Ap 타입을 쓰세요.";
            case PassiveSkillData.PassiveEffectType.HealPercent:
                return "힐량 퍼센트 증가 전용입니다. 0.1 = +10%";
            case PassiveSkillData.PassiveEffectType.CritRate:
            case PassiveSkillData.PassiveEffectType.CritDamage:
                return "비율 수치를 그대로 더합니다. 0.04 = +4%p";
            case PassiveSkillData.PassiveEffectType.MaxMpBonus:
                return "고정 수치 증가 전용입니다. 10 = 최대 마나 +10";
            case PassiveSkillData.PassiveEffectType.PhysDmgReduction:
            case PassiveSkillData.PassiveEffectType.MagicDmgReduction:
                return "방어력/마법 저항력 경감 뒤에 적용됩니다.\n" +
                       "Percent: 0.1 = 받는 데미지 10% 감소 (여러 개면 합산)\n" +
                       "Flat: 10 = 한 대마다 10 감소 (최소 1은 들어감)";
            case PassiveSkillData.PassiveEffectType.AtkSpeed:
                return "기본 수치 칸: 0.1 = 공격속도 +10%";
            case PassiveSkillData.PassiveEffectType.FaithToHp:
                return "기본 수치 칸: 신앙 1당 최대 체력 (2 = 신앙 30이면 +60)";
            case PassiveSkillData.PassiveEffectType.OnDebuffExtraDamage:
                return "기본 수치 칸: 0.1 = 디버프가 1개 이상 걸린 적에게 데미지 +10% (확률 없음)";

            // 발동형 — 특수 효과 설정 칸 사용
            case PassiveSkillData.PassiveEffectType.OnHitAtkSpeedUp:
                return "평타 적중 시 발동.\nProc Chance: 1 = 100%\nProc Value: 0.2 = 공격속도 +20%\nProc Duration: 지속시간(초). 다시 발동하면 누적 없이 갱신";
            case PassiveSkillData.PassiveEffectType.OnHitPoison:
                return "평타 적중 시 발동.\nProc Chance: 1 = 100%\nProc Value: 0.1 = 초당 (근접: 물리 공격력 / 원거리: 마법 공격력)의 10% 마법 데미지\nProc Duration: 독 지속시간(초)";
            case PassiveSkillData.PassiveEffectType.OnCritLightning:
                return "평타가 치명타일 때 발동.\nProc Chance: 1 = 100%\nProc Skill: 이 데미지 스킬의 공식으로 데미지 (비우면 마법 공격력 × Proc Value)\nProc Effect Pool Key: 대상 위치 이펙트";
            case PassiveSkillData.PassiveEffectType.OnHitCooldownReset:
                return "평타 적중 시 발동.\nProc Chance: 0.05 = 5%\n퀵슬롯 스킬 쿨타임을 전부 초기화 (쿨 초기화 효과가 있는 스킬은 제외)";
            case PassiveSkillData.PassiveEffectType.OnKillHeal:
                return "적 처치 시 항상 발동.\nProc Value: 0.05 = 최대 체력의 5% 회복";
            case PassiveSkillData.PassiveEffectType.HealCrit:
                return "수치 칸 없음. 힐할 때 시전자의 치명타 확률로 굴려서 치명타 데미지 배율만큼 힐량 증가";
            case PassiveSkillData.PassiveEffectType.OnHealAtkSpeedUp:
                return "힐 스킬로 힐할 때 항상 발동.\nProc Value: 0.2 = 대상 공격속도 +20%\nProc Duration: 지속시간(초)";
            default:
                return null;
        }
    }
}

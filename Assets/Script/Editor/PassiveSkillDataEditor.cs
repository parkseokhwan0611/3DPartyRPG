using UnityEditor;

// 패시브 효과 타입에 따라 Value Mode를 보여주거나 숨긴다.
// Value Mode는 Atk/Ap/Def/MagicRes/MaxHp에서만 동작하므로, 다른 타입에서 Flat을 골라도
// 무시되고 퍼센트로 계산되는 착각을 막기 위해 해당 타입에선 숨기고 단위 안내를 대신 띄운다.
[CustomEditor(typeof(PassiveSkillData))]
public class PassiveSkillDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var data = (PassiveSkillData)target;
        bool supportsMode = data.TryGetModifierStat(out _);

        SerializedProperty prop = serializedObject.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (prop.name == "valueMode" && !supportsMode) continue;

            using (new EditorGUI.DisabledScope(prop.name == "m_Script"))
                EditorGUILayout.PropertyField(prop, true);

            if (prop.name == "effectType")
            {
                string unitNote = GetUnitNote(data.effectType);
                if (!string.IsNullOrEmpty(unitNote))
                    EditorGUILayout.HelpBox(unitNote, MessageType.Info);
            }
        }

        serializedObject.ApplyModifiedProperties();
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
            default:
                return null;
        }
    }
}

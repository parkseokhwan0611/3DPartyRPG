using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// 스킬 데이터 인스펙터 정리 — 칸은 하나도 숨기지 않고, 관련 있는 칸끼리 섹션으로 묶어 순서만 정리한다.
// 공통 칸(기본 정보·레벨·마나·쿨타임 등)은 SkillData 선언 순서 그대로 위에 그리고, 스킬 종류별 칸을 그 아래 섹션으로.
// 리스트 항목(버프 효과·디버프 효과·스탯 배율·사운드 등)은 아래 PropertyDrawer들이 한 줄에 여러 칸씩 가지런히 그린다.
// 새 필드를 추가했는데 Draw에 넣는 걸 잊으면 맨 아래 "기타"에 자동으로 표시된다.
public abstract class GroupedSkillDataEditor : Editor
{
    private readonly HashSet<string> _drawn = new HashSet<string>();

    private static HashSet<string> _baseFields;
    private static HashSet<string> BaseFields
    {
        get
        {
            if (_baseFields != null) return _baseFields;
            _baseFields = new HashSet<string>();
            foreach (var f in typeof(SkillData).GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                         BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                _baseFields.Add(f.Name);
            return _baseFields;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        _drawn.Clear();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        // 공통 칸 — SkillData 선언 순서·헤더 그대로
        var it = serializedObject.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (BaseFields.Contains(it.name)) EditorGUILayout.PropertyField(it, true);
        }

        DrawSpecific();

        // Draw에 빠진 칸 — 숨기지 않고 맨 아래에
        bool headerDrawn = false;
        it = serializedObject.GetIterator();
        enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (it.name == "m_Script" || BaseFields.Contains(it.name) || _drawn.Contains(it.name)) continue;
            if (!headerDrawn) { Section("기타"); headerDrawn = true; }
            EditorGUILayout.PropertyField(it, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    protected abstract void DrawSpecific();

    protected static void Section(string title)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    protected void Props(params string[] names)
    {
        foreach (var n in names)
        {
            _drawn.Add(n);
            var p = serializedObject.FindProperty(n);
            if (p != null) EditorGUILayout.PropertyField(p, true);
        }
    }

    // 기본값 + 레벨당 증가처럼 짝인 두 칸을 한 줄에
    protected void Pair(string label, string a, string aLabel, string b, string bLabel)
    {
        _drawn.Add(a); _drawn.Add(b);
        var pa = serializedObject.FindProperty(a);
        var pb = serializedObject.FindProperty(b);
        if (pa == null || pb == null) return;

        Rect r = EditorGUILayout.GetControlRect();
        Rect field = EditorGUI.PrefixLabel(r, new GUIContent(label, pa.tooltip));
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        InlineRow.Draw(field, (aLabel, pa, 1f), (bLabel, pb, 1f));
        EditorGUI.indentLevel = indent;
    }

    protected bool Bool(string name)
    {
        var p = serializedObject.FindProperty(name);
        return p != null && p.boolValue;
    }

    protected static void Note(string text) => EditorGUILayout.HelpBox(text, MessageType.None);
}

// ─────────────────────────────────────────────────────────────────
// 데미지 스킬
// ─────────────────────────────────────────────────────────────────
[CustomEditor(typeof(DamageSkillData))]
public class DamageSkillDataEditor : GroupedSkillDataEditor
{
    protected override void DrawSpecific()
    {
        Section("데미지");
        Props("useAp");
        Pair("데미지 배율", "baseDamageMultiplier", "기본", "damageMultiplierPerLevel", "레벨당");
        Props("statScalings");

        Section("사거리 / 범위");
        Props("castRange", "isAoe");
        Pair("범위", "baseRange", "기본", "rangePerLevel", "레벨당");

        // 판정 방식 안내 — 우선순위: 수류탄 > 투사체 > 장판 > 즉시(광역/단일)
        bool grenade    = Bool("throwGrenade");
        bool projectile = !grenade && Bool("fireProjectile");
        bool zone       = !grenade && !projectile && Bool("spawnAtTarget");
        string mode = grenade ? "수류탄형" : projectile ? "투사체형" : zone ? "장판형"
                    : Bool("isAoe") ? "즉시 광역" : "즉시 단일";

        Section("판정 방식");
        Note($"현재 판정: {mode}   (우선순위: 수류탄 > 투사체 > 장판 > 즉시 — 위쪽이 켜져 있으면 아래 설정은 무시됨)");

        Section("  장판");
        Props("spawnAtTarget", "zoneHitOnce", "zoneActivationDelay", "zoneDamageInterval", "zoneDuration", "zoneFaceCaster");

        Section("  수류탄");
        Props("throwGrenade", "grenadeFlightDuration", "grenadeArcHeight");

        Section("  투사체");
        Props("fireProjectile", "projectileCount", "projectileInterval", "projectileSpreadAngle",
              "projectileShotSfxKey", "projectileHitDamage");

        Section("적중 효과");
        Props("onHitDebuffs");
        Pair("디버프 1개당 피해", "debuffBonusPerStack", "기본", "debuffBonusPerLevel", "레벨당");
        Props("maxDebuffStacks");

        Section("시전자 효과");
        Props("onCastBuffs");

        Section("  다음 스킬 연계");
        Props("hasNextSkillBuff", "nextSkillDamageBonus", "nextSkillBuffDuration");

        Section("  어그로");
        Props("hasAggroEffect", "aggroAmount", "aggroRange");

        Section("  무적 + 사라지는 연출");
        Props("vanishCaster", "vanishStartTime", "vanishDuration", "reappearPosition", "reappearDistance", "reappearAnimTrigger");

        Section("애니메이션 / 이펙트");
        Props("animTriggerName", "animDuration", "effectSpawnDelay", "effectPoolKey", "effectSpawnOffset", "effectSpawnRotation");
    }
}

// ─────────────────────────────────────────────────────────────────
// 버프 스킬
// ─────────────────────────────────────────────────────────────────
[CustomEditor(typeof(BuffSkillData))]
public class BuffSkillDataEditor : GroupedSkillDataEditor
{
    protected override void DrawSpecific()
    {
        Section("버프 효과");
        Props("isPartyBuff");
        Pair("지속시간", "baseDuration", "기본", "durationPerLevel", "레벨당");
        Props("buffEffects");

        Section("기본 공격 강화 (OnHitDamage 효과 전용)");
        Props("onHitDamageIsMagic", "basicAttackEffectKey", "basicAttackEffectRotation");

        Section("가시 반사 (Thorns 효과 전용)");
        Props("thornsIsMagic", "thornsScalings", "thornsAggroPerHit", "thornsHitEffectPoolKey");

        Section("부활 (ReviveDead 효과 전용)");
        Props("reviveEffectPoolKey");

        Section("애니메이션 / 이펙트");
        Props("effectStyle", "effectPoolKey", "auraIndex",
              "animTriggerName", "animDuration", "effectSpawnDelay", "effectSpawnOffset", "effectSpawnRotation");
    }
}

// ─────────────────────────────────────────────────────────────────
// 힐 스킬
// ─────────────────────────────────────────────────────────────────
[CustomEditor(typeof(HealSkillData))]
public class HealSkillDataEditor : GroupedSkillDataEditor
{
    protected override void DrawSpecific()
    {
        Section("힐");
        Props("targetType", "useApRatio");
        Pair("힐 배율", "baseHealMultiplier", "기본", "healMultiplierPerLevel", "레벨당");
        Props("statScalings");

        Section("도트 힐");
        Props("isDotHeal", "dotInterval");
        Pair("지속시간", "baseDotDuration", "기본", "dotDurationPerLevel", "레벨당");

        Section("애니메이션 / 이펙트");
        Props("animTriggerName", "animDuration", "effectSpawnDelay",
              "effectPoolKey", "effectSpawnOffset", "effectSpawnRotation",
              "targetEffectPoolKey", "targetEffectSpawnOffset", "targetEffectSpawnRotation");
    }
}

// ─────────────────────────────────────────────────────────────────
// 소환 스킬
// ─────────────────────────────────────────────────────────────────
[CustomEditor(typeof(SummonSkillData))]
public class SummonSkillDataEditor : GroupedSkillDataEditor
{
    protected override void DrawSpecific()
    {
        Section("동작");
        Props("action", "useAp");

        Section("소환 (Action = Summon)");
        Props("summonPrefab", "summonCountByLevel");
        Pair("지속시간",          "baseDuration", "기본", "durationPerLevel", "레벨당");
        Pair("체력 비율",         "baseHpRatio",  "기본", "hpRatioPerLevel",  "레벨당");
        Pair("공격력 비율",       "baseAtkRatio", "기본", "atkRatioPerLevel", "레벨당");
        Pair("방어력 비율",       "baseDefRatio", "기본", "defRatioPerLevel", "레벨당");

        Section("도발 (Action = Taunt)");
        Props("tauntKind", "tauntRadius");
        Pair("도발 어그로",       "baseTauntAggro",       "기본", "tauntAggroPerLevel",       "레벨당");
        Pair("도발 지속시간",     "baseTauntDuration",    "기본", "tauntDurationPerLevel",    "레벨당");
        Pair("도발 데미지 비율",  "baseTauntDamageRatio", "기본", "tauntDamageRatioPerLevel", "레벨당");
        Props("tauntStatScalings", "tauntEffectPoolKey");

        Section("애니메이션 / 이펙트 (시전자)");
        Props("animTriggerName", "animDuration", "effectSpawnDelay",
              "effectPoolKey", "effectSpawnOffset", "effectSpawnRotation");
    }
}

// ─────────────────────────────────────────────────────────────────
// 한 줄에 여러 칸 — "라벨 [값]  라벨 [값]" 형식. 칸에 붙은 [Header] 장식이 작은 칸 안에 그려지지 않도록
// PropertyField 대신 타입별 필드를 직접 그린다 (프리팹 오버라이드 표시·되돌리기는 BeginProperty로 유지)
// ─────────────────────────────────────────────────────────────────
public static class InlineRow
{
    public const float Gap = 8f;

    public static float LineHeight => EditorGUIUtility.singleLineHeight;
    public static float Step       => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

    public static void Draw(Rect rect, params (string label, SerializedProperty prop, float weight)[] items)
    {
        float totalWeight = 0f;
        foreach (var item in items) totalWeight += item.weight;
        float usable = rect.width - Gap * (items.Length - 1);

        float x = rect.x;
        foreach (var item in items)
        {
            float w   = usable * item.weight / totalWeight;
            var seg   = new Rect(x, rect.y, w, LineHeight);
            x        += w + Gap;
            if (item.prop == null) continue;

            var content = new GUIContent(item.label, item.prop.tooltip);
            Rect field  = seg;
            if (!string.IsNullOrEmpty(item.label))
            {
                float lw = Mathf.Min(EditorStyles.label.CalcSize(content).x + 4f, w * 0.55f);
                EditorGUI.LabelField(new Rect(seg.x, seg.y, lw, seg.height), content);
                field = new Rect(seg.x + lw, seg.y, w - lw, seg.height);
            }
            Field(field, item.prop, content);
        }
    }

    private static void Field(Rect r, SerializedProperty p, GUIContent content)
    {
        EditorGUI.BeginProperty(r, content, p);
        EditorGUI.BeginChangeCheck();
        switch (p.propertyType)
        {
            case SerializedPropertyType.Float:
                float f = EditorGUI.FloatField(r, p.floatValue);
                if (EditorGUI.EndChangeCheck())
                    p.floatValue = p.name == "repeatInterval" || p.name == "delay" ? Mathf.Max(0f, f) : f; // [Min(0)] 칸
                break;
            case SerializedPropertyType.Integer:
                int i = EditorGUI.IntField(r, p.intValue);
                if (EditorGUI.EndChangeCheck()) p.intValue = Mathf.Max(p.name == "repeatCount" ? 1 : int.MinValue, i);
                break;
            case SerializedPropertyType.Boolean:
                bool b = EditorGUI.Toggle(r, p.boolValue);
                if (EditorGUI.EndChangeCheck()) p.boolValue = b;
                break;
            case SerializedPropertyType.Enum:
                int e = EditorGUI.Popup(r, p.enumValueIndex, p.enumDisplayNames);
                if (EditorGUI.EndChangeCheck()) p.enumValueIndex = e;
                break;
            case SerializedPropertyType.String:
                string s = EditorGUI.TextField(r, p.stringValue);
                if (EditorGUI.EndChangeCheck()) p.stringValue = s;
                break;
            default:
                EditorGUI.EndChangeCheck();
                EditorGUI.PropertyField(r, p, GUIContent.none, true);
                break;
        }
        EditorGUI.EndProperty();
    }
}

// 리스트 항목 공통 — 줄 단위로 그린다 (접기 없이 항상 펼침, 항목 사이에 약간의 여백)
public abstract class RowListItemDrawer : PropertyDrawer
{
    // 줄마다 (라벨, 필드 이름, 너비 비율) 목록
    protected abstract (string, string, float)[][] Rows(SerializedProperty prop);

    public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
        => Rows(prop).Length * InlineRow.Step + 2f;

    public override void OnGUI(Rect rect, SerializedProperty prop, GUIContent label)
    {
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        float y = rect.y + 1f;
        foreach (var row in Rows(prop))
        {
            var items = new (string, SerializedProperty, float)[row.Length];
            for (int i = 0; i < row.Length; i++)
                items[i] = (row[i].Item1, prop.FindPropertyRelative(row[i].Item2), row[i].Item3);
            InlineRow.Draw(new Rect(rect.x, y, rect.width, InlineRow.LineHeight), items);
            y += InlineRow.Step;
        }
        EditorGUI.indentLevel = indent;
    }
}

// 스탯 배율 (데미지·힐·도발) — 한 줄
[CustomPropertyDrawer(typeof(DamageSkillData.StatScaling))]
public class StatScalingDrawer : RowListItemDrawer
{
    protected override (string, string, float)[][] Rows(SerializedProperty p) => new[]
    {
        new[] { ("스탯", "stat", 1f), ("계수", "coeff", 1f), ("레벨당", "coeffPerLevel", 1f) },
    };
}

// 가시 반사 계수 — 한 줄
[CustomPropertyDrawer(typeof(BuffSkillData.ThornsScaling))]
public class ThornsScalingDrawer : RowListItemDrawer
{
    protected override (string, string, float)[][] Rows(SerializedProperty p) => new[]
    {
        new[] { ("능력치", "stat", 1f), ("계수", "coeff", 1f), ("레벨당", "coeffPerLevel", 1f) },
    };
}

// 버프 효과 — 효과·방식 / 수치 / 스탯 비례
[CustomPropertyDrawer(typeof(BuffSkillData.BuffEffect))]
public class BuffEffectDrawer : RowListItemDrawer
{
    protected override (string, string, float)[][] Rows(SerializedProperty p) => new[]
    {
        new[] { ("효과", "effectType", 2f),  ("방식", "valueMode", 1f) },
        new[] { ("수치", "baseValue", 1f),   ("레벨당", "valuePerLevel", 1f) },
        new[] { ("스탯 비례", "scalingStat", 1.2f), ("계수", "scalingCoeff", 1f), ("레벨당", "scalingPerLevel", 1f) },
    };
}

// 디버프 효과 (디버프 스킬·데미지 스킬의 적중 디버프) — 효과·방식 / 수치 / 지속시간
[CustomPropertyDrawer(typeof(DebuffSkillData.DebuffEffect))]
public class DebuffEffectDrawer : RowListItemDrawer
{
    protected override (string, string, float)[][] Rows(SerializedProperty p) => new[]
    {
        new[] { ("효과", "effectType", 2f),     ("방식", "valueMode", 1f) },
        new[] { ("수치", "baseValue", 1f),      ("레벨당", "valuePerLevel", 1f) },
        new[] { ("지속시간", "baseDuration", 1f), ("레벨당", "durationPerLevel", 1f) },
    };
}

// 데미지 스킬의 시전 시 자기 버프 — 효과·방식 / 수치 / 지속시간 / 스탯 비례 / 아우라
[CustomPropertyDrawer(typeof(DamageSkillData.CastBuffEffect))]
public class CastBuffEffectDrawer : RowListItemDrawer
{
    protected override (string, string, float)[][] Rows(SerializedProperty p) => new[]
    {
        new[] { ("효과", "effectType", 2f),       ("방식", "valueMode", 1f) },
        new[] { ("수치", "baseValue", 1f),        ("레벨당", "valuePerLevel", 1f) },
        new[] { ("지속시간", "baseDuration", 1f), ("레벨당", "durationPerLevel", 1f) },
        new[] { ("스탯 비례", "scalingStat", 1.2f), ("계수", "scalingCoeff", 1f), ("레벨당", "scalingPerLevel", 1f) },
        new[] { ("아우라 사용", "useAura", 1f),    ("아우라 번호", "auraIndex", 1f) },
    };
}

// 스킬 사운드 — 키·딜레이 / 반복 횟수·간격
[CustomPropertyDrawer(typeof(SkillSfxEntry))]
public class SkillSfxEntryDrawer : RowListItemDrawer
{
    protected override (string, string, float)[][] Rows(SerializedProperty p) => new[]
    {
        new[] { ("SFX 키", "sfxKey", 2f),      ("딜레이", "delay", 1f) },
        new[] { ("반복 횟수", "repeatCount", 1f), ("반복 간격", "repeatInterval", 1f) },
    };
}

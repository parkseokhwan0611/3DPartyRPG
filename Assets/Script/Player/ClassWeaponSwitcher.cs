using System;
using System.Collections.Generic;
using UnityEngine;

// 무기(클래스)에 따라 캐릭터 프리팹에 미리 달아둔 무기 오브젝트를 켜고 끈다.
// 현재 클래스에 등록된 오브젝트만 켜고, 다른 클래스에 등록된 오브젝트는 끈다.
// 애니메이터 교체는 CharacterStat이 ClassData.animatorController로 처리
[RequireComponent(typeof(CharacterStat))]
public class ClassWeaponSwitcher : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public ClassData.ClassType classType;
        [Tooltip("이 클래스일 때 켤 오브젝트 (무기 모델, 방패, 무기 이펙트 등)")]
        public List<GameObject> objects = new List<GameObject>();
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private CharacterStat _stat;

    void Awake()
    {
        _stat = GetComponent<CharacterStat>();
        _stat.OnClassApplied += Apply;
    }

    // CharacterStat이 먼저 바인딩을 끝냈으면 이벤트를 놓쳤을 수 있으므로 한 번 직접 적용
    void Start()
    {
        if (_stat.CurrentClass != null) Apply(_stat.CurrentClass);
    }

    void OnDestroy()
    {
        if (_stat != null) _stat.OnClassApplied -= Apply;
    }

    private void Apply(ClassData cls)
    {
        if (cls == null) return;
        // 현재 클래스 항목이 없으면 설정 전으로 보고 아무것도 건드리지 않음 (전부 꺼지는 사고 방지)
        if (!entries.Exists(e => e.classType == cls.classType)) return;

        // 같은 오브젝트가 여러 클래스에 등록돼 있을 수 있으므로 "현재 클래스 항목에 있으면 켬"으로 판단하고,
        // 상태가 달라질 때만 SetActive — 전부 껐다 켜면 이미 켜져 있던 무기 이펙트가 처음부터 다시 재생됨
        foreach (var e in entries)
            foreach (var go in e.objects)
            {
                if (go == null) continue;
                bool on = IsInClass(go, cls.classType);
                if (go.activeSelf != on) go.SetActive(on);
            }
    }

    private bool IsInClass(GameObject go, ClassData.ClassType type)
    {
        foreach (var e in entries)
            if (e.classType == type && e.objects.Contains(go)) return true;
        return false;
    }
}

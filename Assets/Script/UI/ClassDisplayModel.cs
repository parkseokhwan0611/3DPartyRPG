using System;
using System.Collections.Generic;
using UnityEngine;

// 인벤토리 전시 공간의 캐릭터 모델을 그 캐릭터의 현재 무기(클래스)에 맞춘다.
// InventoryUI.displayModels의 각 모델 오브젝트에 붙이고, 클래스별로 켤 오브젝트를 등록한다.
// - 무기만 다르면: 무기·방패 오브젝트를 등록
// - 모델 자체가 다르면: 모델 루트 오브젝트를 통째로 등록
// 전시 모델은 전투 캐릭터와 별개 오브젝트라 CharacterStat 대신 DataManager에서 직접 클래스를 읽는다
public class ClassDisplayModel : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public ClassData.ClassType classType;
        [Tooltip("이 클래스일 때 켤 오브젝트 (무기 모델, 방패, 또는 모델 루트 전체)")]
        public List<GameObject> objects = new List<GameObject>();
        [Tooltip("이 클래스일 때 전시 모델 Animator에 적용할 컨트롤러 (비우면 그대로). 전투용과 다른 전시용 대기 모션을 넣는 칸")]
        public RuntimeAnimatorController animatorController;
    }

    [Tooltip("이 전시 모델이 보여주는 파티 인덱스 (0=첫 번째 캐릭터)")]
    [SerializeField] private int partyIndex;
    [Tooltip("애니메이터 교체 대상 (비우면 자식에서 자동 탐색)")]
    [SerializeField] private Animator targetAnimator;
    [SerializeField] private List<Entry> entries = new List<Entry>();

    // InventoryUI가 탭을 바꿀 때 SetActive(true)로 켜므로 켜질 때마다 최신 클래스로 맞춘다
    void OnEnable()
    {
        if (DataManager.instance != null)
            DataManager.instance.OnPartyClassChanged += HandleClassChanged;
        Apply();
    }

    void OnDisable()
    {
        if (DataManager.instance != null)
            DataManager.instance.OnPartyClassChanged -= HandleClassChanged;
    }

    private void HandleClassChanged(int changedIndex)
    {
        if (changedIndex == partyIndex) Apply();
    }

    private void Apply()
    {
        if (DataManager.instance == null) return;
        var statuses = DataManager.instance.partyStatuses;
        if (partyIndex < 0 || partyIndex >= statuses.Count) return;

        ClassData cls = statuses[partyIndex].classData;
        if (cls == null) return;

        Entry current = entries.Find(e => e.classType == cls.classType);
        if (current == null) return; // 설정 전인 클래스는 건드리지 않음 (전부 꺼지는 사고 방지)

        // 같은 오브젝트가 여러 클래스에 등록돼 있을 수 있으므로 먼저 전부 끄고 해당 클래스 것만 켠다
        foreach (var e in entries)
            foreach (var go in e.objects)
                if (go != null) go.SetActive(false);
        foreach (var go in current.objects)
            if (go != null) go.SetActive(true);

        if (current.animatorController != null)
        {
            // 모델 루트를 통째로 바꾸는 경우를 위해, 켜진 뒤에 애니메이터를 찾는다
            Animator anim = targetAnimator != null ? targetAnimator : GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != current.animatorController)
                anim.runtimeAnimatorController = current.animatorController;
        }
    }
}

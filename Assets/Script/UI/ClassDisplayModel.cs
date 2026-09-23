using System;
using System.Collections;
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

    private bool _subscribed;

    // InventoryUI가 탭을 바꿀 때 SetActive(true)로 켜므로 켜질 때마다 최신 클래스로 맞춘다.
    // 전시 스테이지가 씬 시작부터 켜져 있으면 이 OnEnable이 DataManager.Awake보다 먼저 실행될 수 있어
    // (오브젝트 간 실행 순서 미보장) 그때는 파티 데이터가 아직 없다 — 준비될 때까지 다시 시도한다
    void OnEnable()
    {
        TrySubscribe();
        if (!Apply()) StartCoroutine(ApplyWhenReady());
    }

    void OnDisable()
    {
        if (_subscribed && DataManager.instance != null)
        {
            DataManager.instance.OnPartyClassChanged -= HandleClassChanged;
            DataManager.instance.OnDataInitialized   -= ApplyFromEvent;
        }
        _subscribed = false;
    }

    // 새 게임·불러오기로 파티 데이터가 새로 만들어질 때도 다시 맞춘다
    private void TrySubscribe()
    {
        if (_subscribed || DataManager.instance == null) return;
        DataManager.instance.OnPartyClassChanged += HandleClassChanged;
        DataManager.instance.OnDataInitialized   += ApplyFromEvent;
        _subscribed = true;
    }

    private IEnumerator ApplyWhenReady()
    {
        // DataManager가 살아나고 파티 데이터가 채워질 때까지 매 프레임 재시도 (최대 5초)
        float timeout = 5f;
        while (timeout > 0f)
        {
            yield return null;
            timeout -= Time.unscaledDeltaTime;
            TrySubscribe();
            if (Apply()) yield break;
        }
        Debug.LogWarning($"[ClassDisplayModel] {gameObject.name}: 파티 인덱스 {partyIndex}의 클래스를 찾지 못해 전시 모델을 맞추지 못했습니다.");
    }

    private void ApplyFromEvent() => Apply();

    private void HandleClassChanged(int changedIndex)
    {
        if (changedIndex == partyIndex) Apply();
    }

    // 반환값: 실제로 적용했는지 (false면 아직 데이터가 준비되지 않음)
    private bool Apply()
    {
        if (DataManager.instance == null) return false;
        var statuses = DataManager.instance.partyStatuses;
        if (partyIndex < 0 || partyIndex >= statuses.Count) return false;

        ClassData cls = statuses[partyIndex].classData;
        if (cls == null) return false;

        Entry current = entries.Find(e => e.classType == cls.classType);
        if (current == null)
        {
            // 현재 클래스 항목이 등록돼 있지 않음 — 전부 꺼지는 사고를 막기 위해 아무것도 건드리지 않는다
            Debug.LogWarning($"[ClassDisplayModel] {gameObject.name}: {cls.classType} 항목이 Entries에 없어 전시 무기를 바꾸지 못했습니다.");
            return true;
        }

        // 같은 오브젝트가 여러 클래스에 등록돼 있을 수 있으므로 "현재 클래스 항목에 있으면 켬"으로 판단하고,
        // 상태가 달라질 때만 SetActive — 탭을 열 때마다 모델을 껐다 켜서 애니메이션이 처음으로 돌아가는 것 방지
        foreach (var e in entries)
            foreach (var go in e.objects)
            {
                if (go == null) continue;
                bool on = current.objects.Contains(go);
                if (go.activeSelf != on) go.SetActive(on);
            }

        if (current.animatorController != null)
        {
            // 모델 루트를 통째로 바꾸는 경우를 위해, 켜진 뒤에 애니메이터를 찾는다
            Animator anim = targetAnimator != null ? targetAnimator : GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != current.animatorController)
                anim.runtimeAnimatorController = current.animatorController;
        }

        return true;
    }
}

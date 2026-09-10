using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 버프/디버프가 걸릴 때 캐릭터 위에 대응하는 VFX를 한 번 재생한다.
// PartyStatusEffectHandler.OnBuffChanged 이벤트만 구독하므로 기존 버프 로직은 건드리지 않는다.
[RequireComponent(typeof(PartyStatusEffectHandler))]
public class PartyBuffVfx : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public StatusEffectType effectType;
        [Tooltip("ObjectPoolManager에 등록해둔 이 버프용 VFX 풀 키")]
        public string vfxPoolKey;
    }

    [Tooltip("VFX가 재생될 위치 — 비워두면 이 오브젝트 위치 + Offset을 사용")]
    [SerializeField] private Transform vfxAnchor;
    [Tooltip("Vfx Anchor가 비어있을 때, 캐릭터 위치 기준으로 얼마나 띄워서 재생할지 (머리 위)")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 2f, 0f);

    [Tooltip("StatusEffectType별로 재생할 VFX 풀 키. 비워둔 타입은 그냥 무시된다")]
    [SerializeField] private List<Entry> entries = new List<Entry>();

    private PartyStatusEffectHandler _handler;
    private Dictionary<StatusEffectType, string> _poolKeyMap;

    void Awake()
    {
        _handler = GetComponent<PartyStatusEffectHandler>();

        _poolKeyMap = new Dictionary<StatusEffectType, string>();
        foreach (var entry in entries)
            if (!string.IsNullOrEmpty(entry.vfxPoolKey))
                _poolKeyMap[entry.effectType] = entry.vfxPoolKey;
    }

    void OnEnable()  => _handler.OnBuffChanged += HandleBuffChanged;
    void OnDisable() => _handler.OnBuffChanged -= HandleBuffChanged;

    // 걸릴 때만 한 번 재생 — 스택/지속시간 관리를 이펙트 쪽에서 따로 추적하지 않아도 되도록,
    // 버프가 풀릴 때는 반응하지 않는다 (짧게 반짝이는 연출용 VFX 기준)
    private void HandleBuffChanged(StatusEffectType type, bool applied)
    {
        if (!applied) return;
        if (!_poolKeyMap.TryGetValue(type, out var poolKey)) return;
        if (ObjectPoolManager.instance == null) return;

        GameObject instance = ObjectPoolManager.instance.GetGo(poolKey);
        if (instance == null) return;

        Transform anchor = vfxAnchor != null ? vfxAnchor : transform;
        Vector3 pos = vfxAnchor != null ? anchor.position : anchor.position + offset;

        // 매번 현재 버프를 건 캐릭터의 앵커로 다시 부모를 맞춰준다 — 풀에서 재사용되면 이전에
        // 빌려갔던 다른 캐릭터에 여전히 매달려 있는 상태일 수 있음
        instance.transform.SetParent(anchor, false);
        instance.transform.SetPositionAndRotation(pos, Quaternion.identity);

        // 풀에서 재사용된 파티클에 이전 위치의 잔류 파티클이 남아있지 않도록 초기화 후 재생
        var ps = instance.GetComponent<ParticleSystem>();
        float lifetime = 3f;
        if (ps != null)
        {
            ps.Clear(true);
            ps.Play(true);
            lifetime = ps.main.duration + ps.main.startLifetime.constantMax;
        }

        var poolAble = instance.GetComponent<PoolAble>();
        StartCoroutine(ReleaseAfter(poolAble, lifetime));
    }

    private IEnumerator ReleaseAfter(PoolAble poolAble, float delay)
    {
        yield return new WaitForSeconds(delay);
        // 이미 다른 경로(예: 프리팹에 함께 붙은 PoolableObject의 자체 타이머)로 반납되어
        // 비활성화됐다면 중복 반납하지 않는다
        if (poolAble != null && poolAble.gameObject.activeSelf)
            poolAble.ReleaseObject();
    }
}

using UnityEngine;

// MonsterSkillBase 구현 — 시전자(보스/정예) 자기 자신에게만 보호막을 거는 자버프 스킬.
// 대상(target)은 다른 스킬처럼 플레이어 쪽 타겟이 넘어오지만 이 스킬은 사용하지 않고 무시한다.
// 보호막 자체의 중첩 규칙(수치 합연산, 지속시간은 최신 스킬 기준 갱신)은 StatusEffectHandler.ApplyShield가 전담.
public class MonsterShieldSkill : MonsterSkillBase
{
    [Header("# 보호막 설정")]
    [Tooltip("이번 시전으로 추가되는 보호막 수치 (기존 보호막에 합산됨)")]
    public float shieldAmount = 50f;
    [Tooltip("보호막 지속시간(초). 중첩 시전 시 전체 보호막의 만료 시점이 이 값으로 갱신된다")]
    public float shieldDuration = 5f;

    [Header("# 보호막 VFX (선택 — 비워두면 표시 안 함)")]
    [Tooltip("ObjectPoolManager에 등록한 보호막 VFX 풀 키. 시전자 자신 위치에 생성된다 " +
             "(MonsterAoeSkill.effectPoolKey와 동일한 패턴 — 자체 수명은 프리팹의 PoolableObject.destroyTime에 맡김. " +
             "보호막 지속시간 내내 보이게 하려면 destroyTime을 shieldDuration에 맞춰주면 된다)")]
    public string shieldVfxPoolKey;
    [Tooltip("VFX 스폰 위치의 Y축 보정(m). 프리팹 자체의 Y 위치는 스폰 시 덮어써지므로 무시됨")]
    public float shieldVfxHeightOffset = 0f;

    private StatusEffectHandler _statusHandler;

    void Awake()
    {
        _statusHandler = GetComponent<StatusEffectHandler>();
    }

    public override void ExecuteSkill(Transform target)
    {
        if (_statusHandler == null) return;
        _statusHandler.ApplyShield(shieldAmount, shieldDuration, gameObject);
        SpawnShieldVfx();
    }

    private void SpawnShieldVfx()
    {
        if (string.IsNullOrEmpty(shieldVfxPoolKey) || ObjectPoolManager.instance == null) return;

        var vfx = ObjectPoolManager.instance.GetGo(shieldVfxPoolKey);
        if (vfx == null) return;

        vfx.transform.SetPositionAndRotation(transform.position + Vector3.up * shieldVfxHeightOffset, Quaternion.identity);
    }
}

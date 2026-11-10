using System.Collections;
using UnityEngine;

// 보호막 수치 풀 — 중첩 시 수치는 합연산, 만료 시점은 (남은 시간, 새 지속시간) 중 더 늦은 쪽으로 맞추는
// 단일 풀 + 단일 만료 타이머 방식 (짧은 보호막이 걸려도 이미 걸린 긴 보호막이 일찍 사라지지 않음). PartyStatusEffectHandler/StatusEffectHandler(Enemy)가 공용으로 사용.
// MonoBehaviour가 아니므로 코루틴 실행을 위해 호스트 컴포넌트를 필요로 한다.
public class ShieldPool
{
    private readonly MonoBehaviour _host;
    private Coroutine _routine;
    private float     _expireAt; // 만료 예정 시각 (Time.time 기준)

    public float Current { get; private set; } = 0f;
    public System.Action OnChanged;

    public ShieldPool(MonoBehaviour host)
    {
        _host = host;
    }

    public void Apply(float amount, float duration)
    {
        Current += amount;
        OnChanged?.Invoke();

        // 이미 더 늦게 끝나는 보호막이 있으면 그 만료 시점을 유지
        float expireAt = Time.time + duration;
        if (_routine != null && _expireAt >= expireAt) return;

        if (_routine != null) _host.StopCoroutine(_routine);
        _expireAt = expireAt;
        _routine  = _host.StartCoroutine(ExpireRoutine(duration));
    }

    public float Absorb(float damage)
    {
        if (Current <= 0f) return damage;

        if (Current >= damage)
        {
            Current -= damage;
            OnChanged?.Invoke();
            return 0f;
        }

        damage  -= Current;
        Current  = 0f;
        OnChanged?.Invoke();
        return damage;
    }

    // 사망 등으로 즉시 초기화할 때 사용 — 진행 중이던 만료 코루틴은 호스트 비활성화로
    // 이미 죽으므로 핸들만 정리한다
    public void Clear()
    {
        _routine  = null;
        _expireAt = 0f;
        Current  = 0f;
        OnChanged?.Invoke();
    }

    private IEnumerator ExpireRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        _routine = null;
        Current  = 0f;
        OnChanged?.Invoke();
    }
}

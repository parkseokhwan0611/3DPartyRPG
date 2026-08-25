using System.Collections;
using UnityEngine;

// 쉴드 수치 풀 — 중첩 시 수치는 합연산, 지속시간은 가장 최근에 건 스킬 기준으로 갱신되는
// 단일 풀 + 단일 만료 타이머 방식. PartyStatusEffectHandler/StatusEffectHandler(Enemy)가 공용으로 사용.
// MonoBehaviour가 아니므로 코루틴 실행을 위해 호스트 컴포넌트를 필요로 한다.
public class ShieldPool
{
    private readonly MonoBehaviour _host;
    private Coroutine _routine;

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

        if (_routine != null) _host.StopCoroutine(_routine);
        _routine = _host.StartCoroutine(ExpireRoutine(duration));
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
        _routine = null;
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

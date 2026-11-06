using System.Collections;
using UnityEngine;

// 보스가 죽으면 지정한 오브젝트(다음 맵 포탈 등)를 활성화한다. 보스 몬스터 오브젝트에 붙인다.
// 대상 오브젝트는 씬에 미리 원하는 위치에 배치해 두면 되고, 시작할 때 자동으로 꺼두므로
// 씬에서 활성 상태로 둬도 된다.
[RequireComponent(typeof(EnemyHp))]
public class BossClearActivator : MonoBehaviour
{
    [Tooltip("보스 처치 시 활성화할 오브젝트들 (다음 맵으로 가는 포탈 등)")]
    public GameObject[] activateOnClear;
    [Tooltip("보스가 죽은 뒤 활성화까지 대기 시간(초) — 사망 모션이 끝난 뒤 나타나게 할 때 사용")]
    public float activateDelay = 0f;

    private EnemyHp _enemyHp;
    private bool _pending; // 사망했지만 아직 활성화 대기 중

    void Awake()
    {
        _enemyHp = GetComponent<EnemyHp>();
        SetTargetsActive(false);
    }

    void OnEnable()  { if (_enemyHp != null) _enemyHp.OnDied += HandleDeath; }
    void OnDisable() { if (_enemyHp != null) _enemyHp.OnDied -= HandleDeath; }

    private void HandleDeath()
    {
        if (activateDelay <= 0f)
        {
            SetTargetsActive(true);
            return;
        }
        _pending = true;
        StartCoroutine(ActivateAfterDelay());
    }

    private IEnumerator ActivateAfterDelay()
    {
        yield return new WaitForSeconds(activateDelay);
        Activate();
    }

    // 보스는 사망 후 EnemyHp.destroyDelay가 지나면 Destroy되므로, 대기 시간이 그보다 길면
    // 코루틴이 끊긴다 — 그 경우 파괴되는 순간에 바로 활성화해서 포탈이 안 나타나는 일이 없게 함
    // (씬 전환으로 파괴될 때는 활성화하지 않음)
    void OnDestroy()
    {
        if (_pending && gameObject.scene.isLoaded) Activate();
    }

    private void Activate()
    {
        if (!_pending) return;
        _pending = false;
        SetTargetsActive(true);
    }

    private void SetTargetsActive(bool active)
    {
        if (activateOnClear == null) return;
        foreach (var go in activateOnClear)
            if (go != null) go.SetActive(active);
    }
}

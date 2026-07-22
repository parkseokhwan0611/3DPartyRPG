using System.Collections;
using UnityEngine;

// 일정 주기로 지정된 위치에 지정된 몬스터를 리스폰.
// 스폰한 몬스터를 처치하기 전까지는 새로 리스폰되지 않고, 처치된 뒤 respawnDelay가
// 지나야 다시 스폰된다 (씬에 하나씩 배치, 몬스터 종류/위치별로 스포너를 여러 개 둘 것).
public class MonsterSpawner : MonoBehaviour
{
    [Header("# 스폰 설정")]
    [Tooltip("스폰할 몬스터 프리팹 (EnemyHp 컴포넌트 필수)")]
    [SerializeField] GameObject monsterPrefab;
    [Tooltip("비워두면 이 오브젝트 자신의 위치/회전을 사용")]
    [SerializeField] Transform  spawnPoint;
    [Tooltip("처치 후 다시 스폰되기까지 대기 시간 (초)")]
    [SerializeField] float      respawnDelay = 30f;
    [Tooltip("게임 시작 시 곧바로 스폰할지 여부")]
    [SerializeField] bool       spawnOnStart = true;

    private GameObject _currentMonster;
    private Coroutine  _respawnRoutine;

    void Start()
    {
        if (spawnOnStart) SpawnMonster();
    }

    void OnDestroy()
    {
        if (_respawnRoutine != null) StopCoroutine(_respawnRoutine);
    }

    private void SpawnMonster()
    {
        if (monsterPrefab == null)
        {
            Debug.LogWarning($"[MonsterSpawner] {gameObject.name}: Monster Prefab이 설정되지 않았습니다.");
            return;
        }

        Vector3    pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        _currentMonster = Instantiate(monsterPrefab, pos, rot);

        EnemyHp hp = _currentMonster.GetComponent<EnemyHp>();
        if (hp != null)
            hp.OnDied += HandleMonsterDied;
        else
            Debug.LogWarning($"[MonsterSpawner] {monsterPrefab.name}에 EnemyHp 컴포넌트가 없습니다.");
    }

    // 스폰된 몬스터가 죽었을 때 호출 — 죽은 몬스터 오브젝트는 EnemyHp가 알아서 지연 파괴하므로
    // 여기서는 참조만 비우고 리스폰 타이머만 시작
    private void HandleMonsterDied()
    {
        _currentMonster = null;
        _respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        _respawnRoutine = null;
        SpawnMonster();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos, 0.5f);
    }
}

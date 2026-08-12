using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// MonsterSkillBase 구현 — 보스 주변에 잔몹을 소환. 소환 대상은 기존 일반 몬스터 프리팹을
// MonsterSpawner와 동일하게 직접 Instantiate (일반 몬스터는 오브젝트 풀 대상이 아님 — 죽을 때
// EnemyHp가 알아서 지연 파괴함).
public class MonsterSummonSkill : MonsterSkillBase
{
    [Header("# 소환 설정")]
    [Tooltip("소환할 몬스터 프리팹 목록 (EnemyHp 컴포넌트 필수) — 매번 이 중 하나를 랜덤으로 골라 소환")]
    public GameObject[] summonPrefabs;
    [Tooltip("한 번 시전 시 소환할 마릿수")]
    public int summonCount = 3;
    [Tooltip("보스 위치를 중심으로 소환 지점을 흩뿌리는 반경")]
    public float spawnRadius = 4f;
    [Tooltip("체크하면, 이 스킬로 소환한 잔몹 중 살아있는 개체가 하나라도 있는 동안은 재사용 불가 " +
             "(중첩 소환으로 화면이 잔몹으로 가득 차는 것 방지)")]
    public bool blockIfAliveSummons = true;

    private readonly List<EnemyHp> _liveSummons = new List<EnemyHp>();

    public override bool IsReady(Vector3 selfPos, Vector3 targetPos)
    {
        if (blockIfAliveSummons)
        {
            _liveSummons.RemoveAll(e => e == null);
            if (_liveSummons.Count > 0) return false;
        }
        return base.IsReady(selfPos, targetPos);
    }

    public override void ExecuteSkill(Transform target)
    {
        if (summonPrefabs == null || summonPrefabs.Length == 0) return;

        for (int i = 0; i < summonCount; i++)
        {
            GameObject prefab = summonPrefabs[Random.Range(0, summonPrefabs.Length)];
            if (prefab == null) continue;

            Vector2 offset2D = Random.insideUnitCircle * spawnRadius;
            Vector3 pos      = transform.position + new Vector3(offset2D.x, 0f, offset2D.y);

            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, spawnRadius + 1f, NavMesh.AllAreas))
                pos = hit.position;

            GameObject go = Instantiate(prefab, pos, Quaternion.identity);

            EnemyHp hp = go.GetComponent<EnemyHp>();
            if (hp == null) continue;

            _liveSummons.Add(hp);
            hp.OnDied += () => _liveSummons.Remove(hp);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}

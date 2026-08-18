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

    [Header("# 소환 장판 VFX (선택 — 비워두면 표시 안 함)")]
    [Tooltip("ObjectPoolManager에 등록한 소환 장판 VFX 풀 키. 보스가 아니라 소환된 몬스터 각각의 " +
             "스폰 위치에만 나타난다 (자식으로 붙이지 않음 — 몬스터가 죽어 Destroy될 때 풀링된 VFX가 " +
             "함께 파괴되면서 오브젝트 풀이 깨지는 문제가 있어 월드 좌표만 맞춰서 독립적으로 스폰한다. " +
             "자체 수명은 프리팹의 PoolableObject.destroyTime에 맡김)")]
    public string summonVfxPoolKey;
    [Tooltip("VFX 스폰 위치의 Y축 보정(m) — 소환된 몬스터 발 위치(지면 높이) 기준으로 위/아래 조정. " +
             "프리팹 자체의 Y 위치는 스폰 시 덮어써지므로 무시됨")]
    public float summonVfxHeightOffset = 0f;

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

            SpawnSummonVfx(pos);

            _liveSummons.Add(hp);
            hp.OnDied += () => _liveSummons.Remove(hp);
        }
    }

    // 보스(transform.position)가 아니라 소환에 성공한 몬스터의 스폰 위치(pos)에만 생성 —
    // 자식으로 붙이지 않고 월드 좌표만 맞춰서 몬스터의 생명주기(Destroy)와 완전히 분리한다
    private void SpawnSummonVfx(Vector3 pos)
    {
        if (string.IsNullOrEmpty(summonVfxPoolKey) || ObjectPoolManager.instance == null) return;

        var vfx = ObjectPoolManager.instance.GetGo(summonVfxPoolKey);
        if (vfx == null) return;

        vfx.transform.SetPositionAndRotation(pos + Vector3.up * summonVfxHeightOffset, Quaternion.identity);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}

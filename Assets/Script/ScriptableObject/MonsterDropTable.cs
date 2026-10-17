using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DropEntry
{
    [Tooltip("DataManager.itemRegistry에 등록된 아이템 ID")]
    public string itemId;

    [Range(0f, 1f)]
    [Tooltip("드랍 확률 (0 = 절대 안 드랍, 1 = 항상 드랍)")]
    public float dropRate = 0.1f;

    [Min(1)] public int minCount = 1;
    [Min(1)] public int maxCount = 1;
}

[CreateAssetMenu(fileName = "DropTable_New", menuName = "RPG/Monster Drop Table")]
public class MonsterDropTable : ScriptableObject
{
    [Header("드랍 연출")]
    [Tooltip("Normal / Advanced / Elite / Legendary / Mythic 순서로 5개")]
    public GameObject[] gradePrefabs = new GameObject[5];
    [Tooltip("산개된 스폰 위치의 실제 지면 높이를 찾기 위한 레이캐스트 대상 레이어. " +
             "비워두면(Nothing) 지면 보정 없이 기존 방식으로 동작")]
    public LayerMask groundLayer;
    [Tooltip("ObjectPoolManager에 등록한 풀 키 (gradePrefabs와 같은 순서, 5개). " +
             "비워두면 해당 등급은 풀링 없이 기존처럼 Instantiate/Destroy로 동작")]
    public string[] gradePoolKeys = new string[5];
    [Tooltip("지면(또는 사망 위치) 기준 드랍 오브젝트를 띄우는 높이. 빛기둥/아이템 위치가 " +
             "지형에 파묻히거나 붕 떠 보이면 이 값을 조절")]
    public float spawnHeightOffset = 0.5f;

    [Header("드랍 목록")]
    public List<DropEntry> entries = new List<DropEntry>();

    // 확률 판정 후 worldItemPrefab을 월드에 스폰
    public void SpawnDrops(Vector3 position)
    {
        if (DataManager.instance == null) return;
        bool didDrop = false;
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.itemId)) continue;
            if (Random.value > entry.dropRate) continue;

            ItemData data = DataManager.instance.FindItemById(entry.itemId);
            if (data == null)
            {
                Debug.LogWarning($"[DropTable] itemId '{entry.itemId}'를 itemRegistry에서 찾을 수 없습니다.");
                continue;
            }

            int gradeIndex = (int)data.grade;
            if (gradeIndex >= gradePrefabs.Length || gradePrefabs[gradeIndex] == null)
            {
                Debug.LogWarning($"[DropTable] {data.grade} 등급 프리팹이 할당되지 않았습니다.");
                continue;
            }

            int count = Random.Range(entry.minCount, entry.maxCount + 1);
            var inst = new ItemInstance(data, count);

            Vector3 spawnPos = GetDropPosition(position);

            // 풀 키가 설정돼 있으면 풀에서 꺼내고, 아니면 기존처럼 Instantiate (설정 전에도 정상 동작)
            string poolKey = gradeIndex < gradePoolKeys.Length ? gradePoolKeys[gradeIndex] : null;
            GameObject go = null;
            if (!string.IsNullOrEmpty(poolKey) && ObjectPoolManager.instance != null)
                go = ObjectPoolManager.instance.GetGo(poolKey);

            if (go == null)
                go = Instantiate(gradePrefabs[gradeIndex]);

            go.transform.SetPositionAndRotation(spawnPos, Quaternion.identity);
            go.GetComponent<WorldItem>()?.Setup(inst);
            didDrop = true;
        }

        // 여러 아이템이 동시에 드랍돼도 사운드는 한 번만
        if (didDrop)
            AudioManager.instance?.PlaySFX("ItemDrop");
    }

    // 몬스터 발 위치 주변에 랜덤 산개한 뒤, 걸어가서 주울 수 있는 자리로 보정한다.
    // 예전엔 산개 지점 10m 위에서 아래로 지면을 찾아서, 동굴 구조물·위층 바닥 위에 올라가거나 벽 너머로 넘어가거나
    // 바닥 레이어가 아닌 바위를 통과해 바위 속에 묻히는 경우가 있었음 → NavMesh(걸을 수 있는 바닥) 위로 먼저 보정
    private Vector3 GetDropPosition(Vector3 deathPos)
    {
        Vector2 scatter = Random.insideUnitCircle * 1.5f;
        Vector3 groundPos = deathPos + new Vector3(scatter.x, 0f, scatter.y);

        if (UnityEngine.AI.NavMesh.SamplePosition(groundPos, out var navHit, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
            groundPos = navHit.position;
        else if (UnityEngine.AI.NavMesh.SamplePosition(deathPos, out navHit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            groundPos = navHit.position;   // 산개 지점이 벽 너머 등이면 사망 위치로
        else
            groundPos = deathPos;

        // NavMesh 높이는 실제 바닥과 수십 cm 어긋날 수 있음 — 바로 위(2m)에서 실제 바닥 면에 맞춘다 (천장까지 올라가지 않게 짧게)
        if (groundLayer.value != 0
            && Physics.Raycast(groundPos + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 4f, groundLayer, QueryTriggerInteraction.Ignore))
            groundPos = groundHit.point;

        return groundPos + Vector3.up * spawnHeightOffset;
    }
}

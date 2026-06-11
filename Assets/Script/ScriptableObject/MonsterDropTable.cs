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

    [Header("드랍 목록")]
    public List<DropEntry> entries = new List<DropEntry>();

    // 확률 판정 후 worldItemPrefab을 월드에 스폰
    public void SpawnDrops(Vector3 position)
    {
        if (DataManager.instance == null) return;
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

            // 몬스터 발 위치 주변에 랜덤 산개
            Vector2 scatter = Random.insideUnitCircle * 1.5f;
            Vector3 spawnPos = position + new Vector3(scatter.x, 0.5f, scatter.y);

            GameObject go = Instantiate(gradePrefabs[gradeIndex], spawnPos, Quaternion.identity);
            go.GetComponent<WorldItem>()?.Setup(inst);
        }
    }
}

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

// DataManager.itemRegistry를 수동으로 드래그해 채우는 대신, 프로젝트 내 모든 ItemData(및 파생 SO:
// EquipItemData 등)를 스캔해서 자동으로 채워주는 인스펙터 버튼.
[CustomEditor(typeof(DataManager))]
public class DataManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("아이템 레지스트리 도구", EditorStyles.boldLabel);
        if (GUILayout.Button("프로젝트 내 모든 아이템 SO로 자동 채우기", GUILayout.Height(28)))
            RefreshItemRegistry();
    }

    private void RefreshItemRegistry()
    {
        var dataManager = (DataManager)target;

        // "t:ItemData"는 ItemData를 상속한 EquipItemData 등 파생 SO도 함께 찾아온다
        string[] guids = AssetDatabase.FindAssets("t:ItemData");
        var items = new List<ItemData>();
        var seenIds = new HashSet<string>();
        int duplicateCount = 0;
        int emptyIdCount   = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null) continue;

            if (string.IsNullOrEmpty(item.itemId))
            {
                emptyIdCount++;
                Debug.LogWarning($"[DataManagerEditor] '{path}'의 itemId가 비어있어 레지스트리에서 제외했습니다.");
                continue;
            }

            // FindItemById는 중복 itemId 중 먼저 등록된 것만 쓰므로, 여기서도 동일하게
            // 먼저 발견된 것만 남기고 나머지는 경고와 함께 건너뛴다
            if (!seenIds.Add(item.itemId))
            {
                duplicateCount++;
                Debug.LogWarning($"[DataManagerEditor] itemId '{item.itemId}' 중복 — '{path}'는 건너뛰었습니다.");
                continue;
            }

            items.Add(item);
        }

        items.Sort((a, b) => string.Compare(a.itemId, b.itemId, System.StringComparison.Ordinal));

        Undo.RecordObject(dataManager, "아이템 레지스트리 자동 채우기");
        dataManager.itemRegistry = items;
        EditorUtility.SetDirty(dataManager);

        string summary = $"[DataManagerEditor] 아이템 레지스트리를 {items.Count}개로 갱신했습니다.";
        if (duplicateCount > 0) summary += $" (중복 itemId {duplicateCount}개 제외)";
        if (emptyIdCount   > 0) summary += $" (itemId 없음 {emptyIdCount}개 제외)";
        Debug.Log(summary);
    }
}

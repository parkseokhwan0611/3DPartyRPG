using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

// 씬마다 따로 놓여 있던 루트 오브젝트(CoreSystem·SceneLoader·PoolManager 등)를 프리팹 하나로 묶는 도구.
// 지금 열린 씬의 오브젝트를 Assets/Prefab/System/{이름}.prefab으로 저장하고, Assets/Scenes의 모든 씬에서
// 같은 이름의 루트 오브젝트를 그 프리팹으로 교체한다 (없는 씬에는 추가).
// 이후에는 아무 씬에서나 고치고 Overrides → Apply All 하면 모든 씬에 반영된다.
//
// 안전장치 — 프리팹은 씬 오브젝트를 참조할 수 없으므로:
//  - 고른 오브젝트가 자기 바깥의 씬 오브젝트를 참조하면 진행하지 않는다 (예: UI Canvas가 씬의 캐릭터를 참조)
//  - 어떤 씬에서 다른 오브젝트가 교체 대상을 참조하고 있으면 그 씬은 건너뛰고 알려준다
public static class SharedPrefabTool
{
    private const string PrefabFolder = "Assets/Prefab/System";
    private const string ScenesFolder = "Assets/Scenes";

    [MenuItem("Tools/공유 프리팹/선택한 루트 오브젝트를 프리팹으로 만들고 모든 씬에 적용")]
    private static void ConvertSelected()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || !selected.scene.IsValid() || selected.transform.parent != null)
        {
            EditorUtility.DisplayDialog("공유 프리팹", "Hierarchy에서 씬의 루트 오브젝트 하나를 선택하세요.", "확인");
            return;
        }
        // PoolManager처럼 게임 씬에만 필요한 오브젝트는 타이틀·프롤로그에 넣지 않도록 선택하게 한다
        int choice = EditorUtility.DisplayDialogComplex("공유 프리팹",
            $"'{selected.name}'이(가) 없는 씬에도 새로 추가할까요?\n\n" +
            "• 교체만: 이미 '" + selected.name + "'이(가) 있는 씬만 프리팹으로 교체\n" +
            "• 없는 씬에도 추가: 모든 씬에 놓이게 함",
            "교체만", "취소", "없는 씬에도 추가");
        if (choice == 1) return;
        Convert(selected.scene, selected.name, addMissing: choice == 2);
    }

    [MenuItem("Tools/CoreSystem/현재 씬 CoreSystem을 프리팹으로 만들고 모든 씬에 적용")]
    private static void ConvertCoreSystem() => Convert(SceneManager.GetActiveScene(), "CoreSystem", addMissing: true);

    private static void Convert(Scene sourceScene, string rootName, bool addMissing)
    {
        GameObject source = FindRoot(sourceScene, rootName);
        if (source == null)
        {
            EditorUtility.DisplayDialog("공유 프리팹", $"'{sourceScene.name}' 씬의 루트에 '{rootName}' 오브젝트가 없습니다.", "확인");
            return;
        }

        // 1. 자기 바깥 씬 오브젝트를 참조하면 프리팹으로 만들 수 없다
        List<string> outward = FindOutwardSceneReferences(source);
        if (outward.Count > 0)
        {
            EditorUtility.DisplayDialog("공유 프리팹 불가",
                $"'{rootName}'이(가) 자기 바깥의 씬 오브젝트를 참조하고 있어 프리팹으로 만들 수 없습니다.\n\n" +
                string.Join("\n", outward.GetRange(0, Mathf.Min(10, outward.Count))) +
                (outward.Count > 10 ? $"\n... 외 {outward.Count - 10}개" : ""), "확인");
            return;
        }

        string prefabPath = $"{PrefabFolder}/{rootName}.prefab";
        List<string> scenePaths = GetScenePaths();
        if (!EditorUtility.DisplayDialog("공유 프리팹 적용",
                $"'{sourceScene.name}' 씬의 '{rootName}'을(를) 프리팹({prefabPath})으로 저장하고,\n" +
                $"{ScenesFolder}의 씬 {scenePaths.Count}개에 있는 같은 이름의 루트 오브젝트를 전부 이 프리팹으로 교체합니다" +
                (addMissing ? " (없는 씬에는 추가).\n\n" : ".\n\n") +
                "각 씬의 기존 설정값은 사라집니다. 진행할까요?", "진행", "취소"))
            return;

        // 열려 있는 씬의 저장 안 된 변경이 씬 전환으로 날아가지 않게 먼저 물어본다
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 2. 프리팹 저장 + 원본 씬의 오브젝트를 프리팹 인스턴스로 연결
        EnsureFolder(PrefabFolder);
        GameObject prefab = PrefabUtility.IsPartOfPrefabInstance(source)
            ? PrefabUtility.SaveAsPrefabAsset(PrefabUtility.GetOutermostPrefabInstanceRoot(source), prefabPath)
            : PrefabUtility.SaveAsPrefabAssetAndConnect(source, prefabPath, InteractionMode.UserAction);
        if (prefab == null)
        {
            Debug.LogError($"[SharedPrefabTool] {rootName} 프리팹 저장에 실패했습니다.");
            return;
        }
        EditorSceneManager.MarkSceneDirty(sourceScene);
        EditorSceneManager.SaveScene(sourceScene);

        // 3. 나머지 씬을 하나씩 열어 교체
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        var replaced = new List<string>();
        var added    = new List<string>();
        var skipped  = new List<string>();
        try
        {
            for (int i = 0; i < scenePaths.Count; i++)
            {
                string path = scenePaths[i];
                if (path == sourceScene.path) continue;

                EditorUtility.DisplayProgressBar("공유 프리팹 적용", path, (float)i / scenePaths.Count);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                GameObject old = FindRoot(scene, rootName);
                if (old == null && !addMissing) continue;
                if (old != null && PrefabUtility.GetCorrespondingObjectFromSource(old) == prefab
                    && !PrefabUtility.HasPrefabInstanceAnyOverrides(old, false))
                    continue; // 이미 프리팹 그대로

                // 다른 오브젝트가 교체 대상을 참조하면 교체 시 끊어지므로 건너뜀
                if (old != null && HasInwardReferences(scene, old))
                {
                    skipped.Add(scene.name);
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = rootName;
                if (old != null)
                {
                    instance.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
                    instance.SetActive(old.activeSelf);
                    Object.DestroyImmediate(old);
                    replaced.Add(scene.name);
                }
                else
                {
                    added.Add(scene.name);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            // 원래 열려 있던 씬 구성으로 복귀
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        string msg = $"프리팹: {prefabPath}\n교체: {replaced.Count}개 씬"
                   + (added.Count   > 0 ? $"\n새로 추가: {string.Join(", ", added)}" : "")
                   + (skipped.Count > 0 ? $"\n건너뜀(다른 오브젝트가 참조 중): {string.Join(", ", skipped)}" : "");
        Debug.Log($"[SharedPrefabTool] {msg.Replace('\n', ' ')}");
        EditorUtility.DisplayDialog("공유 프리팹 적용 완료", msg, "확인");
    }

    // root 계층의 컴포넌트가 계층 바깥의 씬 오브젝트를 참조하는 곳 (프리팹은 씬 오브젝트를 참조할 수 없음)
    private static List<string> FindOutwardSceneReferences(GameObject root)
    {
        var result = new List<string>();
        foreach (var comp in root.GetComponentsInChildren<Component>(true))
        {
            if (comp == null) continue;
            var so = new SerializedObject(comp);
            var it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                Object target = it.objectReferenceValue;
                if (target == null || EditorUtility.IsPersistent(target)) continue;

                GameObject targetGo = target as GameObject ?? (target as Component)?.gameObject;
                if (targetGo == null || targetGo.transform.IsChildOf(root.transform)) continue;

                result.Add($"{comp.gameObject.name}.{comp.GetType().Name}.{it.propertyPath} → {targetGo.name}");
            }
        }
        return result;
    }

    // 씬의 다른 오브젝트가 target 계층 안의 무언가를 참조하는지
    private static bool HasInwardReferences(Scene scene, GameObject target)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == target) continue;
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                var so = new SerializedObject(comp);
                var it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object refObj = it.objectReferenceValue;
                    if (refObj == null) continue;
                    GameObject refGo = refObj as GameObject ?? (refObj as Component)?.gameObject;
                    if (refGo != null && refGo.scene == scene && refGo.transform.IsChildOf(target.transform))
                    {
                        Debug.LogWarning($"[SharedPrefabTool] {scene.name}: {comp.gameObject.name}.{comp.GetType().Name}.{it.propertyPath}이(가) {target.name}을(를) 참조해서 교체하지 않았습니다.");
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    private static List<string> GetScenePaths()
    {
        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder }))
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort();
        return paths;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
    }
}

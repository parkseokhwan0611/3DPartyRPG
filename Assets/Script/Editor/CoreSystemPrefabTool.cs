using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

// 씬마다 따로 놓여 있던 CoreSystem(DataManager·SaveManager·AudioManager 등)을 프리팹 하나로 묶는 도구.
// 지금 열린 씬의 CoreSystem을 프리팹으로 저장하고, Assets/Scenes의 모든 씬에 있는 CoreSystem을 그 프리팹으로 교체한다.
// 이후에는 아무 씬에서나 CoreSystem을 고치고 Overrides → Apply All 하면 모든 씬에 반영된다
public static class CoreSystemPrefabTool
{
    private const string CoreName    = "CoreSystem";
    private const string PrefabPath  = "Assets/Prefab/System/CoreSystem.prefab";
    private const string ScenesFolder = "Assets/Scenes";

    [MenuItem("Tools/CoreSystem/현재 씬 CoreSystem을 프리팹으로 만들고 모든 씬에 적용")]
    private static void ConvertAllScenes()
    {
        Scene active = SceneManager.GetActiveScene();
        GameObject source = FindCoreSystem(active);
        if (source == null)
        {
            EditorUtility.DisplayDialog("CoreSystem", $"현재 씬({active.name})의 루트에 '{CoreName}' 오브젝트가 없습니다.", "확인");
            return;
        }

        List<string> scenePaths = GetScenePaths();
        if (!EditorUtility.DisplayDialog("CoreSystem 프리팹 적용",
                $"'{active.name}' 씬의 CoreSystem을 프리팹({PrefabPath})으로 저장하고,\n" +
                $"{ScenesFolder}의 씬 {scenePaths.Count}개에 있는 CoreSystem을 전부 이 프리팹으로 교체합니다.\n\n" +
                "각 씬의 기존 CoreSystem 설정값은 사라집니다. 진행할까요?", "진행", "취소"))
            return;

        // 열려 있는 씬의 저장 안 된 변경이 씬 전환으로 날아가지 않게 먼저 물어본다
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 1. 프리팹 저장 + 현재 씬의 CoreSystem을 프리팹 인스턴스로 연결
        EnsureFolder(System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
        GameObject prefab = PrefabUtility.IsPartOfPrefabInstance(source)
            ? PrefabUtility.SaveAsPrefabAsset(PrefabUtility.GetOutermostPrefabInstanceRoot(source), PrefabPath)
            : PrefabUtility.SaveAsPrefabAssetAndConnect(source, PrefabPath, InteractionMode.UserAction);
        if (prefab == null)
        {
            Debug.LogError("[CoreSystemPrefabTool] 프리팹 저장에 실패했습니다.");
            return;
        }
        EditorSceneManager.MarkSceneDirty(active);
        EditorSceneManager.SaveScene(active);

        // 2. 나머지 씬을 하나씩 열어 교체
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        var replaced = new List<string>();
        var added    = new List<string>();
        try
        {
            for (int i = 0; i < scenePaths.Count; i++)
            {
                string path = scenePaths[i];
                if (path == active.path) continue;

                EditorUtility.DisplayProgressBar("CoreSystem 프리팹 적용", path, (float)i / scenePaths.Count);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                GameObject old = FindCoreSystem(scene);
                if (old != null && PrefabUtility.GetCorrespondingObjectFromSource(old) == prefab
                    && !PrefabUtility.HasPrefabInstanceAnyOverrides(old, false))
                    continue; // 이미 프리팹 그대로

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = CoreName;
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

        string msg = $"프리팹: {PrefabPath}\n교체: {replaced.Count}개 씬" + (added.Count > 0 ? $"\n새로 추가: {string.Join(", ", added)}" : "");
        Debug.Log($"[CoreSystemPrefabTool] {msg.Replace('\n', ' ')}\n교체한 씬: {string.Join(", ", replaced)}");
        EditorUtility.DisplayDialog("CoreSystem 프리팹 적용 완료", msg, "확인");
    }

    private static GameObject FindCoreSystem(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == CoreName) return root;
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

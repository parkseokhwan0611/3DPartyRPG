using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 던전 씬들을 훑어 벽·장애물(가림 판정 레이어)에 쓰인 머티리얼마다 반투명 버전을 에셋으로 만들고,
// Resources/OcclusionFadeLibrary에 원본→반투명 목록을 채운다.
// 실행 중에도 반투명 머티리얼을 즉석에서 만들 수 있지만, 에셋으로 있어야 빌드에 반투명 셰이더 변형이 포함된다.
// 맵을 고쳐 새 머티리얼이 들어오면 다시 실행할 것.
public static class OcclusionFadeLibraryBuilder
{
    private const string LibraryPath    = "Assets/Resources/OcclusionFadeLibrary.asset";
    private const string MaterialFolder = "Assets/Materials/OcclusionFade";
    private const string ScenesFolder   = "Assets/Scenes";

    [MenuItem("Tools/Occlusion/던전 씬 반투명 머티리얼 만들기")]
    private static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 씬을 차례로 열면 쓰지 않는 에셋이 메모리에서 내려가 앞서 잡아둔 에셋 참조가 파괴된다 —
        // 순회 중에는 값과 경로(문자열)만 모으고, 에셋은 순회가 끝난 뒤 다시 불러온다
        int occluderMask = LoadOrCreateLibrary().occluderMask.value;
        var scenes = GetDungeonScenes();
        var materialPaths = new HashSet<string>();
        var unsupported = new HashSet<string>();

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            for (int i = 0; i < scenes.Count; i++)
            {
                EditorUtility.DisplayProgressBar("반투명 머티리얼", scenes[i], (float)i / scenes.Count);
                EditorSceneManager.OpenScene(scenes[i], OpenSceneMode.Single);
                foreach (var r in Object.FindObjectsOfType<Renderer>(true))
                {
                    if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                    if ((occluderMask & (1 << r.gameObject.layer)) == 0) continue;
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        string p = AssetDatabase.GetAssetPath(m);
                        if (!OcclusionFader.SupportsFade(m)) unsupported.Add($"{m.name} ({m.shader.name})");
                        else if (!string.IsNullOrEmpty(p) && p.EndsWith(".mat")) materialPaths.Add(p);
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        EnsureFolder(MaterialFolder);

        // 원본 .mat 파일을 그대로 복사해서 만든다 — new Material()로 만든 에셋에는 URP 버전 정보(AssetVersion)가 없어,
        // 저장 순간 URP MaterialPostprocessor가 새 머티리얼 초기화(InitializeLatest)를 돌리다 에디터가 크래시함.
        // 복사본은 원본의 버전 정보를 그대로 가져가므로 그 초기화를 건너뛴다. 매번 원본에서 새로 복사(원본 변경 반영)
        var copies = new List<(string srcPath, string path)>();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string srcPath in materialPaths)
            {
                string guid = AssetDatabase.AssetPathToGUID(srcPath);
                string path = $"{MaterialFolder}/{Path.GetFileNameWithoutExtension(srcPath)}_{guid.Substring(0, 8)}_Fade.mat";
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path))) AssetDatabase.DeleteAsset(path);
                if (AssetDatabase.CopyAsset(srcPath, path)) copies.Add((srcPath, path));
                else Debug.LogWarning($"[OcclusionFade] 복사 실패: {srcPath}");
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        var lib = LoadOrCreateLibrary();
        var pairs = new List<OcclusionFadeLibrary.Pair>();
        foreach (var (srcPath, path) in copies)
        {
            var src  = AssetDatabase.LoadAssetAtPath<Material>(srcPath);
            var fade = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (src == null || fade == null) continue;
            fade.name = System.IO.Path.GetFileNameWithoutExtension(path);
            OcclusionFader.SetupTransparent(fade);
            EditorUtility.SetDirty(fade);
            pairs.Add(new OcclusionFadeLibrary.Pair { original = src, transparent = fade });
        }
        int created = pairs.Count;
        pairs.Sort((a, b) => string.Compare(a.original.name, b.original.name, System.StringComparison.Ordinal));

        lib.pairs = pairs;
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();

        string msg = $"씬 {scenes.Count}개에서 반투명 머티리얼 {created}개 생성";
        if (unsupported.Count > 0)
        {
            msg += $"\n반투명 미지원 셰이더라 제외: {unsupported.Count}개 (콘솔 참고)";
            Debug.Log("[OcclusionFade] 제외된 머티리얼:\n" + string.Join("\n", unsupported));
        }
        Debug.Log("[OcclusionFade] " + msg.Replace('\n', ' '));
        EditorUtility.DisplayDialog("반투명 머티리얼", msg, "확인");
        Selection.activeObject = lib;
    }

    private static OcclusionFadeLibrary LoadOrCreateLibrary()
    {
        var lib = AssetDatabase.LoadAssetAtPath<OcclusionFadeLibrary>(LibraryPath);
        if (lib != null) return lib;
        EnsureFolder(Path.GetDirectoryName(LibraryPath).Replace('\\', '/'));
        lib = ScriptableObject.CreateInstance<OcclusionFadeLibrary>();
        // 벽·장애물 레이어만 — 던전 씬은 바닥=Plane, 나머지 맵 조각=Obstacle로 정리돼 있음
        lib.occluderMask = LayerMask.GetMask("Obstacle", "Tree");
        AssetDatabase.CreateAsset(lib, LibraryPath);
        return lib;
    }

    // 실제 게임에 쓰는 던전 씬 — Dungeon_Full(데모 보관용)은 제외
    private static List<string> GetDungeonScenes()
    {
        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.StartsWith("Dungeon_") && name != "Dungeon_Full") paths.Add(path);
        }
        paths.Sort();
        return paths;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}

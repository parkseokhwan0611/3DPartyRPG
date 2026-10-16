using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// 미니맵 배경 이미지를 뽑기 위한 에디터 툴 — 씬을 위에서 내려다보는 직교 카메라로 한 번 렌더링해서
/// PNG로 저장한다. 지형이 바뀌면 언제든 다시 눌러서 재생성할 수 있다.
/// 캡처된 월드 좌표 범위(중심/크기)는 런타임 미니맵 스크립트의 월드→UV 변환 공식에 그대로
/// 재사용해야 하므로, 캡처 후 콘솔과 창에 함께 표시한다.
/// </summary>
public class MinimapCaptureTool : EditorWindow
{
    [Tooltip("비워두면 씬의 모든 Renderer를 합친 범위를 사용. 지정하면 그 하위 Renderer만 기준으로 범위를 계산")]
    private Transform boundsRoot;
    private LayerMask cullingMask = ~0;
    private int resolution = 2048;
    private float paddingMultiplier = 1.05f;
    private float cameraHeightMargin = 50f;
    private string outputFolder = "Assets/Textures/Minimap";

    private Bounds _lastCapturedBounds;
    private bool _hasCaptured;

    [MenuItem("Tools/Minimap/Capture Top-Down Map")]
    private static void Open()
    {
        GetWindow<MinimapCaptureTool>("Minimap Capture").minSize = new Vector2(360, 320);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("캡처 범위", EditorStyles.boldLabel);
        boundsRoot = (Transform)EditorGUILayout.ObjectField(
            new GUIContent("범위 기준 오브젝트 (선택)", "비워두면 씬 전체 Renderer 기준"),
            boundsRoot, typeof(Transform), true);
        cullingMask = LayerMaskField("촬영에 포함할 레이어", cullingMask);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("캡처 설정", EditorStyles.boldLabel);
        resolution = EditorGUILayout.IntField("해상도 (정사각형, px)", resolution);
        paddingMultiplier = EditorGUILayout.FloatField("여백 배율", paddingMultiplier);
        cameraHeightMargin = EditorGUILayout.FloatField("카메라 높이 여유(m)", cameraHeightMargin);
        outputFolder = EditorGUILayout.TextField("저장 폴더", outputFolder);

        EditorGUILayout.Space();
        if (GUILayout.Button("캡처", GUILayout.Height(32)))
            Capture();

        if (_hasCaptured)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("마지막 캡처 월드 범위 (런타임 스크립트에 그대로 사용)", EditorStyles.boldLabel);
            EditorGUILayout.Vector2Field("중심 (X, Z)", new Vector2(_lastCapturedBounds.center.x, _lastCapturedBounds.center.z));
            EditorGUILayout.Vector2Field("크기 (Width, Depth)", new Vector2(_lastCapturedBounds.size.x, _lastCapturedBounds.size.z));
        }
    }

    private static LayerMask LayerMaskField(string label, LayerMask mask)
    {
        var layers = UnityEditorInternal.InternalEditorUtility.layers;
        int layerNumbers = 0;
        for (int i = 0; i < layers.Length; i++)
            layerNumbers |= 1 << LayerMask.NameToLayer(layers[i]);

        int maskWithoutEmpty = InternalMaskToField(mask, layers);
        int newMaskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers);
        return FieldToInternalMask(newMaskWithoutEmpty, layers);
    }

    private static int InternalMaskToField(LayerMask mask, string[] layers)
    {
        int result = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            int layerIndex = LayerMask.NameToLayer(layers[i]);
            if ((mask.value & (1 << layerIndex)) != 0) result |= 1 << i;
        }
        return result;
    }

    private static LayerMask FieldToInternalMask(int field, string[] layers)
    {
        int mask = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            if ((field & (1 << i)) != 0)
                mask |= 1 << LayerMask.NameToLayer(layers[i]);
        }
        return mask;
    }

    private void Capture()
    {
        Bounds bounds = ComputeBounds();
        if (bounds.size.x <= 0f || bounds.size.z <= 0f)
        {
            Debug.LogError("[MinimapCaptureTool] 캡처 범위를 계산하지 못했습니다 (Renderer가 없습니다).");
            return;
        }

        // 카메라는 자기 Culling Mask에 없는 레이어의 라이트도 빼고 렌더링한다 — 촬영 레이어를 Plane/Tree만 고르면
        // Default 레이어의 Directional Light가 빠져 환경광(하늘색)만 받아 파랗게 찍힘.
        // 촬영 중에만 라이트를 촬영 레이어로 옮겨 포함시키고 끝나면 원래 레이어로 되돌린다 (라이트가 비추는 대상은 그대로)
        int captureLayer = FirstLayerInMask(cullingMask);
        var movedLights = new System.Collections.Generic.List<(GameObject go, int layer)>();
        if (captureLayer >= 0)
        {
            foreach (var light in Object.FindObjectsOfType<Light>())
            {
                if (!light.isActiveAndEnabled) continue;
                var go = light.gameObject;
                if ((cullingMask.value & (1 << go.layer)) != 0) continue;
                movedLights.Add((go, go.layer));
                go.layer = captureLayer;
            }
        }

        // 카메라가 지형 위 높이 여유(50m 등)만큼 떨어져 있어 씬 포그의 시작/끝 거리를 넘기면 화면 전체가 포그 색으로 덮임
        // (Dungeon_Tutorial: 파란 포그, 30~95m) — 촬영 중에만 포그를 끄고 끝나면 되돌린다
        bool prevFog = RenderSettings.fog;
        RenderSettings.fog = false;

        float centerX = bounds.center.x, centerZ = bounds.center.z;
        float half = Mathf.Max(bounds.extents.x, bounds.extents.z);
        try
        {
            // 1차: 작은 해상도로 찍어서 실제로 픽셀이 찍힌 영역만 찾아 범위를 줄인다 (자동 크롭) —
            // Renderer 경계에는 위에서 안 보이는 벽 뒷면 등도 들어가서, 그대로 쓰면 빈 공간까지 캡처됨
            const int probeRes = 512;
            Texture2D probe = RenderTopDown(bounds, centerX, centerZ, half, probeRes);
            if (TryGetOpaqueRect(probe, out int minPx, out int minPy, out int maxPx, out int maxPy))
            {
                float pixelSize = half * 2f / probeRes;
                float minX = centerX - half + minPx * pixelSize;
                float maxX = centerX - half + (maxPx + 1) * pixelSize;
                float minZ = centerZ - half + minPy * pixelSize;
                float maxZ = centerZ - half + (maxPy + 1) * pixelSize;
                centerX = (minX + maxX) * 0.5f;
                centerZ = (minZ + maxZ) * 0.5f;
                half    = Mathf.Max(maxX - minX, maxZ - minZ) * 0.5f;
            }
            Object.DestroyImmediate(probe);

            // 2차: 줄인 범위 + 여백으로 최종 해상도 캡처
            half *= paddingMultiplier;
            Texture2D tex = RenderTopDown(bounds, centerX, centerZ, half, resolution);
            SaveTexture(tex);
            Object.DestroyImmediate(tex);
        }
        finally
        {
            foreach (var (go, layer) in movedLights)
                if (go != null) go.layer = layer;
            RenderSettings.fog = prevFog;
        }

        _lastCapturedBounds = new Bounds(new Vector3(centerX, bounds.center.y, centerZ), new Vector3(half * 2f, bounds.size.y, half * 2f));
        _hasCaptured = true;

        // 씬의 MinimapUI에 범위를 바로 입력 — 손으로 옮겨 적다가 재캡처 후 갱신을 빼먹으면 아이콘 위치가 어긋남
        var minimap = Object.FindObjectOfType<MinimapUI>(true);
        if (minimap != null)
        {
            Undo.RecordObject(minimap, "Minimap Capture Bounds");
            minimap.mapWorldCenter = new Vector2(centerX, centerZ);
            minimap.mapWorldSize   = new Vector2(half * 2f, half * 2f);
            EditorUtility.SetDirty(minimap);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(minimap.gameObject.scene);
            Debug.Log($"[MinimapCaptureTool] '{minimap.name}'의 Map World Center/Size에 자동 입력했습니다 — 씬을 저장하세요.", minimap);
        }
        else
        {
            Debug.LogWarning("[MinimapCaptureTool] 씬에서 MinimapUI를 찾지 못해 범위를 자동 입력하지 못했습니다. 아래 값을 직접 입력하세요.");
        }

        Debug.Log($"[MinimapCaptureTool] 캡처 완료 — 중심: ({_lastCapturedBounds.center.x:F2}, {_lastCapturedBounds.center.z:F2}), " +
                  $"크기: ({_lastCapturedBounds.size.x:F2} x {_lastCapturedBounds.size.z:F2}). " +
                  "런타임 미니맵 스크립트의 월드→UV 변환에 이 값을 사용하세요.");
    }

    // 위에서 내려다보는 직교 카메라로 (centerX, centerZ) 중심, 한 변 half*2 크기의 정사각형 영역을 찍는다
    private Texture2D RenderTopDown(Bounds bounds, float centerX, float centerZ, float half, int res)
    {
        var camGo = new GameObject("__MinimapCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = half;
        cam.cullingMask = cullingMask;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = bounds.size.y + cameraHeightMargin * 2f + 10f;

        camGo.transform.position = new Vector3(centerX, bounds.max.y + cameraHeightMargin, centerZ);
        camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var rt = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
        cam.targetTexture = rt;

        RenderTexture prevActive = RenderTexture.active;
        try
        {
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
            tex.Apply();
            return tex;
        }
        finally
        {
            RenderTexture.active = prevActive;
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
        }
    }

    // 알파가 있는(무언가 찍힌) 픽셀들의 최소/최대 좌표. 하나도 없으면 false
    private static bool TryGetOpaqueRect(Texture2D tex, out int minX, out int minY, out int maxX, out int maxY)
    {
        Color32[] pixels = tex.GetPixels32();
        int w = tex.width, h = tex.height;
        minX = w; minY = h; maxX = -1; maxY = -1;
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                if (pixels[row + x].a < 8) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
        return maxX >= 0;
    }

    private static int FirstLayerInMask(LayerMask mask)
    {
        for (int i = 0; i < 32; i++)
            if ((mask.value & (1 << i)) != 0) return i;
        return -1;
    }

    private Bounds ComputeBounds()
    {
        Bounds bounds = default;
        bool initialized = false;

        // 일반 메쉬 오브젝트
        Renderer[] renderers = boundsRoot != null
            ? boundsRoot.GetComponentsInChildren<Renderer>()
            : Object.FindObjectsOfType<Renderer>();

        foreach (var r in renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            // 실제로 찍히는 것만 범위에 포함 — 촬영 레이어 밖 오브젝트, 경계가 실제보다 훨씬 크게 잡히는
            // 파티클·트레일·라인 렌더러가 섞이면 아무것도 없는 빈 공간까지 캡처 범위가 넓어짐
            if ((cullingMask.value & (1 << r.gameObject.layer)) == 0) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!initialized) { bounds = r.bounds; initialized = true; }
            else bounds.Encapsulate(r.bounds);
        }

        // Unity Terrain은 Renderer 컴포넌트가 없어 위 루프에서 잡히지 않으므로 별도로 합산
        Terrain[] terrains = boundsRoot != null
            ? boundsRoot.GetComponentsInChildren<Terrain>()
            : Terrain.activeTerrains;

        foreach (var t in terrains)
        {
            if (t == null || t.terrainData == null) continue;
            Vector3 size = t.terrainData.size;
            var terrainBounds = new Bounds(t.transform.position + size * 0.5f, size);
            if (!initialized) { bounds = terrainBounds; initialized = true; }
            else bounds.Encapsulate(terrainBounds);
        }

        return initialized ? bounds : default;
    }

    private void SaveTexture(Texture2D tex)
    {
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string fileName = $"{sceneName}_Minimap.png";
        string fullPath = Path.Combine(outputFolder, fileName);

        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        AssetDatabase.Refresh();

        var importer = AssetImporter.GetAtPath(fullPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        var asset = AssetDatabase.LoadAssetAtPath<Object>(fullPath);
        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;
    }
}

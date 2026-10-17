using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
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

    // 베이크된 NavMesh로 갈 수 있는 영역을 구해, 그 밖을 어둡게 칠하고 경계에 외곽선을 넣는다
    private bool highlightWalkable = true;
    private float outsideBrightness = 0.35f;
    private float edgeExpand = 0.7f;      // NavMesh는 벽에서 에이전트 반경만큼 안쪽으로 구워지므로 그만큼 넓혀 실제 바닥 가장자리에 맞춤
    private bool drawOutline = true;
    private Color outlineColor = new Color(1f, 0.9f, 0.65f, 0.9f);
    private float outlineWidthPx = 2f;
    private float minIslandArea = 10f;    // 이보다 작은 NavMesh 조각(바위 위 등 따로 떨어진 곳)은 강조하지 않음

    private Bounds _lastCapturedBounds;
    private bool _hasCaptured;

    [MenuItem("Tools/Minimap/Capture Top-Down Map")]
    private static void Open()
    {
        GetWindow<MinimapCaptureTool>("Minimap Capture").minSize = new Vector2(360, 480);
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
        EditorGUILayout.LabelField("길 강조 (베이크된 NavMesh 기준)", EditorStyles.boldLabel);
        highlightWalkable = EditorGUILayout.Toggle("갈 수 없는 곳 어둡게", highlightWalkable);
        using (new EditorGUI.DisabledScope(!highlightWalkable))
        {
            outsideBrightness = EditorGUILayout.Slider("바깥 밝기", outsideBrightness, 0f, 1f);
            edgeExpand = EditorGUILayout.FloatField(new GUIContent("가장자리 확장(m)", "NavMesh는 벽에서 에이전트 반경만큼 떨어져 구워짐 — 보통 에이전트 반경과 같게"), edgeExpand);
            minIslandArea = EditorGUILayout.FloatField(new GUIContent("최소 영역 넓이(m²)", "이보다 작은 따로 떨어진 NavMesh 조각은 강조하지 않음"), minIslandArea);
            drawOutline = EditorGUILayout.Toggle("외곽선", drawOutline);
            using (new EditorGUI.DisabledScope(!drawOutline))
            {
                outlineColor = EditorGUILayout.ColorField("외곽선 색", outlineColor);
                outlineWidthPx = EditorGUILayout.Slider("외곽선 두께(px)", outlineWidthPx, 0.5f, 8f);
            }
        }

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
            if (highlightWalkable) ApplyWalkableHighlight(tex, centerX, centerZ, half);
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

    // ─────────────────────────────────────────────────────────────────
    // 길 강조
    // ─────────────────────────────────────────────────────────────────

    // 캡처 이미지에 NavMesh 영역을 겹쳐서, 갈 수 있는 곳은 그대로 두고 바깥은 어둡게, 경계에는 외곽선을 칠한다.
    // 텍스처 좌표: 왼쪽 아래(0,0) = 월드 (centerX - half, centerZ - half), 오른쪽 = +X, 위쪽 = +Z
    private void ApplyWalkableHighlight(Texture2D tex, float centerX, float centerZ, float half)
    {
        int res = tex.width;
        float pixelSize = half * 2f / res;
        bool[] mask = RasterizeNavMesh(res, centerX - half, centerZ - half, pixelSize);
        if (mask == null) return;

        float[] dist = DistanceFromMask(mask, res);
        float expandPx = Mathf.Max(0f, edgeExpand) / pixelSize;
        float outlinePx = drawOutline ? outlineWidthPx : 0f;

        Color32[] pixels = tex.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            // 0 = 갈 수 있는 곳, 1 = 경계 밖 1px 이상 — 사이는 계단 현상 없이 부드럽게 섞음
            float outside = Mathf.Clamp01(dist[i] - expandPx);
            if (outside <= 0f) continue;

            Color c = pixels[i];
            float k = Mathf.Lerp(1f, outsideBrightness, outside);
            c.r *= k; c.g *= k; c.b *= k;

            if (outlinePx > 0f)
            {
                float o = outside * Mathf.Clamp01(expandPx + outlinePx + 1f - dist[i]) * outlineColor.a;
                if (o > 0f)
                {
                    c.r = Mathf.Lerp(c.r, outlineColor.r, o);
                    c.g = Mathf.Lerp(c.g, outlineColor.g, o);
                    c.b = Mathf.Lerp(c.b, outlineColor.b, o);
                    c.a = Mathf.Max(c.a, o); // 아무것도 안 찍힌(투명) 자리에서도 외곽선은 보이게
                }
            }
            pixels[i] = c;
        }
        tex.SetPixels32(pixels);
        tex.Apply();
    }

    // 베이크된 NavMesh 삼각형을 위에서 본 모양 그대로 픽셀 마스크로 칠한다. 작은 조각(minIslandArea 미만)은 제외
    private bool[] RasterizeNavMesh(int res, float originX, float originZ, float pixelSize)
    {
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        Vector3[] v = tri.vertices;
        int[] idx = tri.indices;
        if (v.Length == 0 || idx.Length == 0)
        {
            Debug.LogWarning("[MinimapCaptureTool] 베이크된 NavMesh가 없어 길 강조를 건너뜁니다. NavMesh를 먼저 Bake하세요.");
            return null;
        }

        // 타일 경계에서 같은 위치의 정점이 따로 나오므로 위치로 합친 뒤, 정점을 공유하는 삼각형끼리 한 조각으로 묶음
        var weld = new Dictionary<Vector3Int, int>();
        int[] vid = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var key = new Vector3Int(Mathf.RoundToInt(v[i].x * 100f), Mathf.RoundToInt(v[i].y * 100f), Mathf.RoundToInt(v[i].z * 100f));
            if (!weld.TryGetValue(key, out int id)) { id = weld.Count; weld[key] = id; }
            vid[i] = id;
        }
        int[] parent = new int[weld.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        for (int t = 0; t < idx.Length; t += 3)
        {
            int a = Find(vid[idx[t]]), b = Find(vid[idx[t + 1]]), c = Find(vid[idx[t + 2]]);
            parent[b] = a; parent[Find(c)] = a;
        }
        var islandArea = new Dictionary<int, float>();
        for (int t = 0; t < idx.Length; t += 3)
        {
            int root = Find(vid[idx[t]]);
            islandArea.TryGetValue(root, out float area);
            islandArea[root] = area + TriangleAreaXZ(v[idx[t]], v[idx[t + 1]], v[idx[t + 2]]);
        }

        bool[] mask = new bool[res * res];
        int skipped = 0;
        for (int t = 0; t < idx.Length; t += 3)
        {
            if (islandArea[Find(vid[idx[t]])] < minIslandArea) { skipped++; continue; }
            Vector2 p0 = ToPixel(v[idx[t]]), p1 = ToPixel(v[idx[t + 1]]), p2 = ToPixel(v[idx[t + 2]]);
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x))));
            int maxX = Mathf.Min(res - 1, Mathf.CeilToInt(Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y))));
            int maxY = Mathf.Min(res - 1, Mathf.CeilToInt(Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y))));
            float area = Edge(p0, p1, p2);
            if (Mathf.Abs(area) < 1e-6f) continue;
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float w0 = Edge(p1, p2, p), w1 = Edge(p2, p0, p), w2 = Edge(p0, p1, p);
                // 감기 방향과 상관없이 세 값의 부호가 모두 같으면 삼각형 안
                if ((w0 >= 0f && w1 >= 0f && w2 >= 0f) || (w0 <= 0f && w1 <= 0f && w2 <= 0f))
                    mask[y * res + x] = true;
            }
        }
        if (skipped > 0)
            Debug.Log($"[MinimapCaptureTool] {minIslandArea}m² 미만의 작은 NavMesh 조각(삼각형 {skipped}개)은 강조에서 제외했습니다.");
        return mask;

        Vector2 ToPixel(Vector3 w) => new Vector2((w.x - originX) / pixelSize, (w.z - originZ) / pixelSize);
    }

    private static float Edge(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

    private static float TriangleAreaXZ(Vector3 a, Vector3 b, Vector3 c) =>
        Mathf.Abs((b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z)) * 0.5f;

    // 각 픽셀에서 가장 가까운 마스크 픽셀까지의 거리(px). 2패스 챔퍼 근사 — 확장·외곽선 폭 계산용으로 충분히 정확함
    private static float[] DistanceFromMask(bool[] mask, int res)
    {
        const float D1 = 1f, D2 = 1.41421356f;
        float[] d = new float[mask.Length];
        for (int i = 0; i < d.Length; i++) d[i] = mask[i] ? 0f : float.MaxValue;

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            int i = y * res + x;
            float v = d[i];
            if (x > 0) v = Mathf.Min(v, d[i - 1] + D1);
            if (y > 0)
            {
                v = Mathf.Min(v, d[i - res] + D1);
                if (x > 0)       v = Mathf.Min(v, d[i - res - 1] + D2);
                if (x < res - 1) v = Mathf.Min(v, d[i - res + 1] + D2);
            }
            d[i] = v;
        }
        for (int y = res - 1; y >= 0; y--)
        for (int x = res - 1; x >= 0; x--)
        {
            int i = y * res + x;
            float v = d[i];
            if (x < res - 1) v = Mathf.Min(v, d[i + 1] + D1);
            if (y < res - 1)
            {
                v = Mathf.Min(v, d[i + res] + D1);
                if (x < res - 1) v = Mathf.Min(v, d[i + res + 1] + D2);
                if (x > 0)       v = Mathf.Min(v, d[i + res - 1] + D2);
            }
            d[i] = v;
        }
        return d;
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

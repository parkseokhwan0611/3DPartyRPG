using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

// Terrain 컴포넌트에 붙여서 인스펙터 우클릭(컨텍스트 메뉴)으로 실행하는 지형 자동 생성 툴.
// 실행 시 기존 높이맵/텍스처/나무를 덮어쓰므로, 실행 전 커밋해두는 걸 권장.
[RequireComponent(typeof(Terrain))]
public class TerrainProceduralGenerator : MonoBehaviour
{
    [Header("높이 생성 — 탑다운 RPG용, 고저차를 작게 유지")]
    [Tooltip("랜덤 시드. 같은 값이면 항상 같은 지형이 나옴")]
    public int seed = 0;
    [Tooltip("언덕의 높낮이 폭 (월드 단위, 미터). 탑다운이면 2~4 정도 권장")]
    public float heightVariation = 3f;
    [Tooltip("지형 전체를 띄우는 기본 높이 (월드 단위, 미터)")]
    public float baseHeight = 0f;
    [Tooltip("노이즈 스케일 — 작을수록 언덕이 완만하고 넓게 퍼짐")]
    public float noiseScale = 2.5f;
    [Range(1, 6)] public int octaves = 3;
    [Range(0f, 1f)] public float persistence = 0.4f;
    public float lacunarity = 2f;
    [Tooltip("생성 후 스무딩 반복 횟수 — 클수록 더 완만해짐")]
    [Range(0, 8)] public int smoothIterations = 3;

    [Header("외곽 경계(산) — 지형 가장자리를 높여서 자연스러운 벽으로 사용 (선택, 기본 꺼짐)")]
    [Tooltip("전체 생성(GenerateAll) 실행 시 이 단계를 포함할지 여부")]
    public bool  raiseEdges         = false;
    [Tooltip("경계 산의 최대 높이 (월드 단위, 미터) — 안쪽 언덕보다 훨씬 높게, NavMesh Max Slope를 넘길 만큼 충분히 가파르게")]
    public float edgeMountainHeight = 25f;
    [Tooltip("가장자리에서 이 비율만큼 안쪽까지 산이 퍼짐 (0.5 = 지형 절반까지). 진짜 끝만 올리고 싶으면 0.02~0.05처럼 작게")]
    [Range(0.01f, 0.4f)] public float edgeBorderWidth = 0.04f;
    [Tooltip("경계 산의 울퉁불퉁함 — 값이 클수록 더 거칠고 험준해짐")]
    [Range(1f, 20f)] public float edgeRoughness = 8f;

    [Header("텍스처 페인팅 (선택 — 비워두면 건너뜀)")]
    [Tooltip("완만한 지역에 칠할 레이어 (예: 잔디)")]
    public TerrainLayer flatLayer;
    [Tooltip("경사가 급한 지역에 칠할 레이어 (예: 바위/절벽)")]
    public TerrainLayer steepLayer;
    [Tooltip("이 경사도(도) 이상이면 steepLayer로 칠함")]
    public float steepAngleThreshold = 25f;

    [Header("외곽 이동 차단 (NavMesh 통행불가 구역 — 실제로 못 나가게 막는 투명 벽)")]
    [Tooltip("전체 생성(GenerateAll) 실행 시 이 단계를 포함할지 여부")]
    public bool  addBoundaryBlockers = true;
    [Tooltip("가장자리에서 안쪽으로 이 두께(월드 단위, 미터)만큼 NavMesh 생성을 막음")]
    public float boundaryThickness   = 5f;
    [Tooltip("차단 구역의 높이 (월드 단위, 미터) — 지형 최고점보다 넉넉하게")]
    public float boundaryHeight      = 50f;

    [Header("나무 배치 (선택 — 비워두면 건너뜀)")]
    public GameObject[] treePrefabs;
    [Tooltip("1제곱미터당 나무가 생성될 확률")]
    [Range(0f, 0.05f)] public float treeDensity = 0.005f;
    [Tooltip("이 경사도(도) 이상이면 나무를 심지 않음")]
    public float treeMaxSlope = 20f;

    private Terrain     _terrain;
    private TerrainData _data;

    private void Cache()
    {
        if (_terrain == null) _terrain = GetComponent<Terrain>();
        _data = _terrain != null ? _terrain.terrainData : null;
    }

    [ContextMenu("1. 지형 높이 생성")]
    public void GenerateHeights()
    {
        Cache();
        if (_data == null) { Debug.LogWarning("[TerrainProceduralGenerator] TerrainData가 없습니다."); return; }

        int res = _data.heightmapResolution;
        float[,] heights = new float[res, res];

        var   rng     = new System.Random(seed);
        float offsetX = rng.Next(-100000, 100000) * 0.01f;
        float offsetY = rng.Next(-100000, 100000) * 0.01f;

        float normVariation = _data.size.y > 0f ? heightVariation / _data.size.y : 0f;
        float normBase      = _data.size.y > 0f ? baseHeight      / _data.size.y : 0f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx = (float)x / res;
                float ny = (float)y / res;

                float h = FractalNoise(nx, ny, offsetX, offsetY, octaves, persistence, lacunarity, noiseScale);
                heights[y, x] = Mathf.Clamp01(normBase + h * normVariation);
            }
        }

        Smooth(heights, res, smoothIterations);

        _data.SetHeights(0, 0, heights);
        Debug.Log("[TerrainProceduralGenerator] 지형 높이 생성 완료");
    }

    // 프랙탈(다중 옥타브) 펄린 노이즈 — 0~1로 정규화된 값을 반환
    private static float FractalNoise(float nx, float ny, float offsetX, float offsetY,
                                       int octaves, float persistence, float lacunarity, float frequency)
    {
        float amplitude    = 1f;
        float amplitudeSum = 0f;
        float h            = 0f;

        for (int o = 0; o < octaves; o++)
        {
            h += Mathf.PerlinNoise(nx * frequency + offsetX, ny * frequency + offsetY) * amplitude;
            amplitudeSum += amplitude;
            amplitude    *= persistence;
            frequency    *= lacunarity;
        }

        return amplitudeSum > 0f ? h / amplitudeSum : 0f;
    }

    // 3x3 박스 블러 — 노이즈 특유의 각진 경계를 완만하게 다듬음
    private static void Smooth(float[,] heights, int res, int iterations)
    {
        for (int it = 0; it < iterations; it++)
        {
            float[,] copy = (float[,])heights.Clone();
            for (int y = 1; y < res - 1; y++)
            {
                for (int x = 1; x < res - 1; x++)
                {
                    float sum = 0f;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            sum += copy[y + dy, x + dx];
                    heights[y, x] = sum / 9f;
                }
            }
        }
    }

    [ContextMenu("2. 외곽 경계(산) 생성")]
    public void RaiseEdges()
    {
        Cache();
        if (_data == null) { Debug.LogWarning("[TerrainProceduralGenerator] TerrainData가 없습니다."); return; }

        int res = _data.heightmapResolution;
        float[,] heights = _data.GetHeights(0, 0, res, res);

        var   rng     = new System.Random(seed + 2);
        float offsetX = rng.Next(-100000, 100000) * 0.01f;
        float offsetY = rng.Next(-100000, 100000) * 0.01f;

        float normEdgeHeight = _data.size.y > 0f ? edgeMountainHeight / _data.size.y : 0f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx = (float)x / res;
                float ny = (float)y / res;

                // 가장자리까지의 거리 (0 = 가장자리, 0.5 = 지형 정중앙) → edgeBorderWidth 기준으로 0~1 정규화
                float distToEdge = Mathf.Min(Mathf.Min(nx, 1f - nx), Mathf.Min(ny, 1f - ny));
                float t          = edgeBorderWidth > 0f ? Mathf.Clamp01(distToEdge / edgeBorderWidth) : 1f;
                float edgeFactor = Mathf.SmoothStep(1f, 0f, t); // t=0(가장자리)->1, t=1(경계 밖)->0
                if (edgeFactor <= 0f) continue;

                float mountainNoise = FractalNoise(nx, ny, offsetX, offsetY, 4, 0.5f, 2f, edgeRoughness);
                heights[y, x] = Mathf.Clamp01(heights[y, x] + edgeFactor * mountainNoise * normEdgeHeight);
            }
        }

        _data.SetHeights(0, 0, heights);
        Debug.Log("[TerrainProceduralGenerator] 외곽 경계(산) 생성 완료");
    }

    [ContextMenu("3. 텍스처 페인팅 (경사 기반)")]
    public void PaintTextures()
    {
        Cache();
        if (_data == null) { Debug.LogWarning("[TerrainProceduralGenerator] TerrainData가 없습니다."); return; }
        if (flatLayer == null || steepLayer == null)
        {
            Debug.LogWarning("[TerrainProceduralGenerator] flatLayer/steepLayer가 비어있어 텍스처 페인팅을 건너뜁니다.");
            return;
        }

        int flatIndex  = EnsureLayer(flatLayer);
        int steepIndex = EnsureLayer(steepLayer);

        int   w          = _data.alphamapWidth;
        int   h          = _data.alphamapHeight;
        int   layerCount = _data.terrainLayers.Length;
        float[,,] alphas = new float[h, w, layerCount];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx        = (float)x / w;
                float ny        = (float)y / h;
                float steepness = _data.GetSteepness(nx, ny);

                int chosen = steepness >= steepAngleThreshold ? steepIndex : flatIndex;
                for (int l = 0; l < layerCount; l++)
                    alphas[y, x, l] = (l == chosen) ? 1f : 0f;
            }
        }

        _data.SetAlphamaps(0, 0, alphas);
        Debug.Log("[TerrainProceduralGenerator] 텍스처 페인팅 완료");
    }

    // layer가 이미 terrainLayers에 있으면 그 인덱스를, 없으면 뒤에 추가하고 새 인덱스를 반환
    private int EnsureLayer(TerrainLayer layer)
    {
        var layers = _data.terrainLayers;
        for (int i = 0; i < layers.Length; i++)
            if (layers[i] == layer) return i;

        var newLayers = new TerrainLayer[layers.Length + 1];
        layers.CopyTo(newLayers, 0);
        newLayers[layers.Length] = layer;
        _data.terrainLayers      = newLayers;
        return newLayers.Length - 1;
    }

    [ContextMenu("4. 나무 배치 (경사 회피)")]
    public void ScatterTrees()
    {
        Cache();
        if (_data == null) { Debug.LogWarning("[TerrainProceduralGenerator] TerrainData가 없습니다."); return; }
        if (treePrefabs == null || treePrefabs.Length == 0)
        {
            Debug.LogWarning("[TerrainProceduralGenerator] treePrefabs가 비어있어 나무 배치를 건너뜁니다.");
            return;
        }

        var prototypes = new List<TreePrototype>();
        foreach (var prefab in treePrefabs)
        {
            if (prefab == null) continue;
            prototypes.Add(new TreePrototype { prefab = prefab });
        }
        if (prototypes.Count == 0)
        {
            Debug.LogWarning("[TerrainProceduralGenerator] 유효한 treePrefabs가 없어 나무 배치를 건너뜁니다.");
            return;
        }
        _data.treePrototypes = prototypes.ToArray();

        var   rng   = new System.Random(seed + 1);
        float area  = _data.size.x * _data.size.z;
        int   count = Mathf.RoundToInt(area * treeDensity);

        var instances = new List<TreeInstance>(count);
        for (int i = 0; i < count; i++)
        {
            float nx = (float)rng.NextDouble();
            float nz = (float)rng.NextDouble();

            if (_data.GetSteepness(nx, nz) > treeMaxSlope) continue;

            instances.Add(new TreeInstance
            {
                position       = new Vector3(nx, 0f, nz),
                prototypeIndex = rng.Next(0, prototypes.Count),
                widthScale     = 0.85f + (float)rng.NextDouble() * 0.3f,
                heightScale    = 0.85f + (float)rng.NextDouble() * 0.3f,
                rotation       = (float)(rng.NextDouble() * Mathf.PI * 2),
                color          = Color.white,
                lightmapColor  = Color.white,
            });
        }

        _data.SetTreeInstances(instances.ToArray(), true);
        Debug.Log($"[TerrainProceduralGenerator] 나무 {instances.Count}그루 배치 완료");
    }

    [ContextMenu("5. 외곽 이동 차단 (NavMesh 통행불가 구역)")]
    public void GenerateBoundaryBlockers()
    {
        Cache();
        if (_data == null) { Debug.LogWarning("[TerrainProceduralGenerator] TerrainData가 없습니다."); return; }

        const string rootName = "BoundaryBlockers";
        Transform existing = transform.Find(rootName);
        if (existing != null) DestroyImmediate(existing.gameObject);

        var root = new GameObject(rootName);
        root.transform.SetParent(transform, false);

        Vector3 size = _data.size; // x=가로, y=높이 범위, z=세로
        float   t    = boundaryThickness;
        float   hY   = boundaryHeight / 2f;

        CreateBoundaryVolume(root.transform, "North", new Vector3(size.x / 2f, hY, size.z - t / 2f), new Vector3(size.x, boundaryHeight, t));
        CreateBoundaryVolume(root.transform, "South", new Vector3(size.x / 2f, hY, t / 2f),           new Vector3(size.x, boundaryHeight, t));
        CreateBoundaryVolume(root.transform, "East",  new Vector3(size.x - t / 2f, hY, size.z / 2f),  new Vector3(t, boundaryHeight, size.z));
        CreateBoundaryVolume(root.transform, "West",  new Vector3(t / 2f, hY, size.z / 2f),           new Vector3(t, boundaryHeight, size.z));

        Debug.Log("[TerrainProceduralGenerator] 외곽 이동 차단 구역 생성 완료 — NavMesh를 다시 베이크해야 실제로 적용됩니다.");
    }

    // area=1은 NavMesh 빌드 시 "Not Walkable" — 렌더러 없이 순수하게 NavMesh만 뚫어서 못 지나가게 함
    private static void CreateBoundaryVolume(Transform parent, string name, Vector3 localPos, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        var modifier = go.AddComponent<NavMeshModifierVolume>();
        modifier.size   = size;
        modifier.center = Vector3.zero;
        modifier.area   = 1; // Not Walkable
    }

    [ContextMenu("전체 생성 (1 -> 2 -> 3 -> 4 -> 5)")]
    public void GenerateAll()
    {
        GenerateHeights();
        if (raiseEdges) RaiseEdges();
        PaintTextures();
        ScatterTrees();
        if (addBoundaryBlockers) GenerateBoundaryBlockers();
    }
}

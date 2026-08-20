using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

// Terrain 컴포넌트에 붙여서 인스펙터 우클릭(컨텍스트 메뉴)으로 실행하는 지형 자동 생성 툴.
// 실행 시 기존 높이맵/텍스처/나무를 덮어쓰므로, 실행 전 커밋해두는 걸 권장.
[RequireComponent(typeof(Terrain))]
public class TerrainProceduralGenerator : MonoBehaviour
{
    [Header("높이 — 탑다운 게임으로 결정하면서 고저차 없이 항상 평탄(고도 0)으로 고정")]
    [Tooltip("랜덤 시드. 같은 값이면 항상 같은 배치가 나옴")]
    public int seed = 0;
    [Tooltip("지형 전체 고도 (월드 단위, 미터). 탑다운이라 기본 0 고정 — 필요 시에만 조정")]
    public float baseHeight = 0f;

    [Header("텍스처 페인팅 (선택 — 비워두면 건너뜀)")]
    [Tooltip("기본 레이어 (예: 잔디)")]
    public TerrainLayer flatLayer;
    [Tooltip("flatLayer와 함께 섞을 추가 레이어들 (선택 — 밝은/마른/이끼 낀 잔디 등). " +
             "비워두면 flatLayer 하나만 칠해짐")]
    public TerrainLayer[] additionalFlatLayers;
    [Tooltip("여러 레이어를 섞는 패치 노이즈 스케일 — 작을수록 패치가 넓어짐")]
    public float flatBlendScale = 4f;

    [Header("디테일(잔디 등) 페인팅 (선택 — 비워두면 건너뜀)")]
    [Tooltip("작은 디테일 프리팹 (잔디/잡초 등) — GPU 인스턴싱으로 저렴하게 흩뿌려짐")]
    public GameObject[] detailPrefabs;
    [Tooltip("각 칸에 디테일이 배치될 확률 (0~1)")]
    [Range(0f, 1f)] public float detailCoverage = 0.5f;
    [Tooltip("배치될 때의 밀도 (칸당 개수 느낌, 1~16)")]
    [Range(1, 16)] public int detailDensity = 6;

    [Header("외곽 이동 차단 (NavMesh 통행불가 구역 — 실제로 못 나가게 막는 투명 벽)")]
    [Tooltip("전체 생성(GenerateAll) 실행 시 이 단계를 포함할지 여부")]
    public bool  addBoundaryBlockers = true;
    [Tooltip("가장자리에서 안쪽으로 이 두께(월드 단위, 미터)만큼 NavMesh 생성을 막음")]
    public float boundaryThickness   = 5f;
    [Tooltip("차단 구역의 높이 (월드 단위, 미터) — 지형 최고점보다 넉넉하게")]
    public float boundaryHeight      = 50f;

    [Header("나무 배치 (선택 — 비워두면 건너뜀) — Terrain Tree 시스템(GPU 인스턴싱)")]
    public GameObject[] treePrefabs;
    [Tooltip("1제곱미터당 나무가 생성될 확률")]
    [Range(0f, 0.05f)] public float treeDensity = 0.005f;

    [Header("바위 배치 (선택 — 비워두면 건너뜀) — 나무와 동일한 Terrain Tree 시스템 공유")]
    public GameObject[] rockPrefabs;
    [Tooltip("1제곱미터당 바위가 생성될 확률")]
    [Range(0f, 0.05f)] public float rockDensity = 0.01f;

    [Header("특수 오브젝트 배치 (동상 등, 선택 — 비워두면 건너뜀) — 실제 GameObject로 인스턴스화")]
    public GameObject[] specialObjectPrefabs;
    [Tooltip("배치할 개수 (밀도가 아니라 정확한 개수)")]
    public int specialObjectCount = 3;
    [Tooltip("특수 오브젝트끼리 서로 이 거리(월드 단위, 미터) 이상 떨어지도록 배치")]
    public float specialObjectMinSpacing = 15f;
    [Tooltip("개체 하나당 최소 간격을 만족하는 자리를 찾기 위한 최대 시도 횟수 — 다 써도 못 찾으면 그 개체는 건너뜀")]
    public int specialObjectMaxAttempts = 30;

    private Terrain     _terrain;
    private TerrainData _data;

    private void Cache()
    {
        if (_terrain == null) _terrain = GetComponent<Terrain>();
        _data = _terrain != null ? _terrain.terrainData : null;
    }

    [ContextMenu("1. 지형 높이 고정 (평탄, 고도 0)")]
    public void GenerateHeights()
    {
        Cache();
        if (_data == null) { Debug.LogWarning("[TerrainProceduralGenerator] TerrainData가 없습니다."); return; }

        int res = _data.heightmapResolution;
        float[,] heights = new float[res, res];

        float normBase = _data.size.y > 0f ? baseHeight / _data.size.y : 0f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
                heights[y, x] = normBase;

        _data.SetHeights(0, 0, heights);
        Debug.Log("[TerrainProceduralGenerator] 지형 높이 고정 완료 (평탄)");
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

    [ContextMenu("2. 텍스처 페인팅 (잔디 블렌드)")]
    public void PaintTextures()
    {
        Cache();
        if (_data == null) { Debug.LogWarning("[TerrainProceduralGenerator] TerrainData가 없습니다."); return; }
        if (flatLayer == null)
        {
            Debug.LogWarning("[TerrainProceduralGenerator] flatLayer가 비어있어 텍스처 페인팅을 건너뜁니다.");
            return;
        }

        var flatPool = new List<TerrainLayer> { flatLayer };
        if (additionalFlatLayers != null)
            foreach (var l in additionalFlatLayers)
                if (l != null) flatPool.Add(l);

        int[] flatIndices = new int[flatPool.Count];
        for (int i = 0; i < flatPool.Count; i++)
            flatIndices[i] = EnsureLayer(flatPool[i]);

        var   rng     = new System.Random(seed + 4);
        float offsetX = rng.Next(-100000, 100000) * 0.01f;
        float offsetY = rng.Next(-100000, 100000) * 0.01f;

        int   w          = _data.alphamapWidth;
        int   h          = _data.alphamapHeight;
        int   layerCount = _data.terrainLayers.Length;
        float[,,] alphas = new float[h, w, layerCount];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = (float)x / w;
                float ny = (float)y / h;

                int chosen;
                if (flatIndices.Length > 1)
                {
                    // 여러 flat 레이어를 저주파 노이즈로 패치 단위로 나눠 섞음
                    float patch  = FractalNoise(nx, ny, offsetX, offsetY, 2, 0.5f, 2f, flatBlendScale);
                    int   pIndex = Mathf.Clamp(Mathf.FloorToInt(patch * flatIndices.Length), 0, flatIndices.Length - 1);
                    chosen = flatIndices[pIndex];
                }
                else
                {
                    chosen = flatIndices[0];
                }

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

    [ContextMenu("3. 디테일(잔디) 페인팅")]
    public void PaintDetails()
    {
        Cache();
        if (_data == null) { Debug.LogWarning("[TerrainProceduralGenerator] TerrainData가 없습니다."); return; }

        var validPrefabs = new List<GameObject>();
        if (detailPrefabs != null)
            foreach (var p in detailPrefabs)
                if (p != null) validPrefabs.Add(p);

        if (validPrefabs.Count == 0)
        {
            Debug.LogWarning("[TerrainProceduralGenerator] detailPrefabs가 비어있어 디테일 페인팅을 건너뜁니다.");
            return;
        }

        int dw = _data.detailWidth;
        int dh = _data.detailHeight;
        if (dw <= 0 || dh <= 0)
        {
            Debug.LogWarning("[TerrainProceduralGenerator] Terrain의 Detail Resolution이 0입니다. Terrain 설정에서 먼저 지정해주세요.");
            return;
        }

        var prototypes = new List<DetailPrototype>();
        foreach (var prefab in validPrefabs)
        {
            prototypes.Add(new DetailPrototype
            {
                prototype        = prefab,
                usePrototypeMesh = true,
                renderMode       = DetailRenderMode.VertexLit,
                useInstancing    = true,
                healthyColor     = Color.white,
                dryColor         = Color.white,
                minWidth  = 0.8f, maxWidth  = 1.2f,
                minHeight = 0.8f, maxHeight = 1.2f,
                noiseSeed = seed,
            });
        }
        _data.detailPrototypes = prototypes.ToArray();

        for (int p = 0; p < prototypes.Count; p++)
        {
            var rng      = new System.Random(seed + 10 + p);
            var layerMap = new int[dh, dw];

            for (int y = 0; y < dh; y++)
            {
                for (int x = 0; x < dw; x++)
                {
                    layerMap[y, x] = rng.NextDouble() < detailCoverage ? detailDensity : 0;
                }
            }

            _data.SetDetailLayer(0, 0, p, layerMap);
        }

        Debug.Log($"[TerrainProceduralGenerator] 디테일 페인팅 완료 ({prototypes.Count}종류)");
    }

    // 나무/바위 둘 다 Terrain의 Tree 시스템(GPU 인스턴싱)을 공유한다. treePrototypes/SetTreeInstances는
    // 호출할 때마다 전체를 통째로 덮어쓰기 때문에, 따로따로 호출하면 서로를 지워버린다 —
    // 그래서 두 카테고리를 한 프로토타입 배열 + 한 인스턴스 목록으로 합쳐서 한 번에 기록한다
    [ContextMenu("4. 나무 + 바위 배치")]
    public void ScatterTreesAndRocks()
    {
        Cache();
        if (_data == null) { Debug.LogWarning("[TerrainProceduralGenerator] TerrainData가 없습니다."); return; }

        var validTrees = new List<GameObject>();
        if (treePrefabs != null)
            foreach (var p in treePrefabs) if (p != null) validTrees.Add(p);

        var validRocks = new List<GameObject>();
        if (rockPrefabs != null)
            foreach (var p in rockPrefabs) if (p != null) validRocks.Add(p);

        if (validTrees.Count == 0 && validRocks.Count == 0)
        {
            Debug.LogWarning("[TerrainProceduralGenerator] treePrefabs/rockPrefabs가 모두 비어있어 배치를 건너뜁니다.");
            return;
        }

        var prototypes = new List<TreePrototype>();
        int treeStart = prototypes.Count;
        foreach (var p in validTrees) prototypes.Add(new TreePrototype { prefab = p });
        int rockStart = prototypes.Count;
        foreach (var p in validRocks) prototypes.Add(new TreePrototype { prefab = p });

        _data.treePrototypes = prototypes.ToArray();

        var instances = new List<TreeInstance>();
        if (validTrees.Count > 0)
            ScatterTreeCategory(instances, treeStart, validTrees.Count, treeDensity, seed + 1);
        if (validRocks.Count > 0)
            ScatterTreeCategory(instances, rockStart, validRocks.Count, rockDensity, seed + 2);

        _data.SetTreeInstances(instances.ToArray(), true);
        Debug.Log($"[TerrainProceduralGenerator] 나무 {validTrees.Count}종 / 바위 {validRocks.Count}종, 총 {instances.Count}개 배치 완료");
    }

    // prototypeStart..prototypeStart+prototypeCount-1 범위 안에서 랜덤으로 골라 area*density개 흩뿌림
    private void ScatterTreeCategory(List<TreeInstance> instances, int prototypeStart, int prototypeCount,
                                      float density, int categorySeed)
    {
        var   rng   = new System.Random(categorySeed);
        float area  = _data.size.x * _data.size.z;
        int   count = Mathf.RoundToInt(area * density);

        for (int i = 0; i < count; i++)
        {
            float nx = (float)rng.NextDouble();
            float nz = (float)rng.NextDouble();

            instances.Add(new TreeInstance
            {
                position       = new Vector3(nx, 0f, nz), // y=0(정규화) — 평탄한 지형 표면에 그대로 놓임
                prototypeIndex = prototypeStart + rng.Next(0, prototypeCount),
                widthScale     = 0.85f + (float)rng.NextDouble() * 0.3f,
                heightScale    = 0.85f + (float)rng.NextDouble() * 0.3f,
                rotation       = (float)(rng.NextDouble() * Mathf.PI * 2),
                color          = Color.white,
                lightmapColor  = Color.white,
            });
        }
    }

    // 동상 등 랜드마크형 오브젝트 — Terrain Tree와 달리 개별 콜라이더/스크립트가 필요할 수 있어
    // 실제 GameObject로 인스턴스화한다. 서로 너무 가까이 겹치지 않도록 최소 간격을 두고 배치
    [ContextMenu("5. 특수 오브젝트 배치 (동상 등)")]
    public void ScatterSpecialObjects()
    {
        Cache();
        if (_data == null || _terrain == null) { Debug.LogWarning("[TerrainProceduralGenerator] TerrainData가 없습니다."); return; }

        var validPrefabs = new List<GameObject>();
        if (specialObjectPrefabs != null)
            foreach (var p in specialObjectPrefabs) if (p != null) validPrefabs.Add(p);

        if (validPrefabs.Count == 0)
        {
            Debug.LogWarning("[TerrainProceduralGenerator] specialObjectPrefabs가 비어있어 배치를 건너뜁니다.");
            return;
        }

        const string rootName = "SpecialObjects";
        Transform existingRoot = transform.Find(rootName);
        if (existingRoot != null) DestroyImmediate(existingRoot.gameObject);

        var root = new GameObject(rootName);
        root.transform.SetParent(transform, false);

        var rng     = new System.Random(seed + 20);
        var placed  = new List<Vector3>();
        Vector3 origin = _terrain.transform.position;
        Vector3 size   = _data.size;
        float minSqr   = specialObjectMinSpacing * specialObjectMinSpacing;

        int placedCount = 0;
        for (int i = 0; i < specialObjectCount; i++)
        {
            Vector3 candidate = Vector3.zero;
            bool    found     = false;

            for (int attempt = 0; attempt < specialObjectMaxAttempts; attempt++)
            {
                float wx = (float)rng.NextDouble() * size.x;
                float wz = (float)rng.NextDouble() * size.z;
                candidate = origin + new Vector3(wx, 0f, wz);

                bool tooClose = false;
                foreach (var p in placed)
                {
                    if ((p - candidate).sqrMagnitude < minSqr) { tooClose = true; break; }
                }

                if (!tooClose) { found = true; break; }
            }

            if (!found) continue; // 최대 시도 안에 자리를 못 찾음 — 이 개체는 건너뜀

            placed.Add(candidate);

            GameObject prefab   = validPrefabs[rng.Next(0, validPrefabs.Count)];
            GameObject instance = Instantiate(prefab, candidate, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), root.transform);
            instance.name = prefab.name;
            placedCount++;
        }

        Debug.Log($"[TerrainProceduralGenerator] 특수 오브젝트 {placedCount}/{specialObjectCount}개 배치 완료");
    }

    [ContextMenu("6. 외곽 이동 차단 (NavMesh 통행불가 구역)")]
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

    [ContextMenu("전체 생성 (1 -> 2 -> 3 -> 4 -> 5 -> 6)")]
    public void GenerateAll()
    {
        GenerateHeights();
        PaintTextures();
        PaintDetails();
        ScatterTreesAndRocks();
        ScatterSpecialObjects();
        if (addBoundaryBlockers) GenerateBoundaryBlockers();
    }
}

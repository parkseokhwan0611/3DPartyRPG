using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 고립된 NavMesh 막기 — 베이크된 NavMesh에서 파티 시작 위치와 이어지지 않은 조각(발코니·벽 윗면·바위 위 등
/// 실제로는 갈 수 없는 곳)을 찾아, 그 자리에 Not Walkable 볼륨(NavMeshModifierVolume)을 만든다.
/// 사용: Bake → 분석(씬 뷰에 빨갛게 표시) → 볼륨 만들기 → 다시 Bake.
/// 만든 볼륨은 씬 루트의 "NavMesh_NotWalkable_Auto" 아래에 모이고, 지우면 원래대로 돌아간다.
/// </summary>
public class NavMeshIslandTool : EditorWindow
{
    private const string AutoRootName = "NavMesh_NotWalkable_Auto";

    private float cellSize = 2.5f;
    private float minIslandArea = 0f;

    private class Island
    {
        public readonly List<int> tris = new List<int>(); // 삼각형 시작 인덱스 (indices 배열 기준)
        public float area;
        public Bounds bounds;
    }

    private Vector3[] _verts;
    private int[] _indices;
    private readonly List<Island> _islands = new List<Island>();
    private readonly List<int> _mainTris = new List<int>();
    private float _mainArea;
    private string _status;
    private Vector2 _scroll;

    [MenuItem("Tools/NavMesh/고립된 NavMesh 막기")]
    private static void Open() => GetWindow<NavMeshIslandTool>("NavMesh Islands").minSize = new Vector2(380, 360);

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorSceneManager.activeSceneChangedInEditMode += HandleSceneChanged;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorSceneManager.activeSceneChangedInEditMode -= HandleSceneChanged;
    }

    // 다른 씬을 열면 이전 씬의 분석 결과는 의미가 없으므로 비운다
    private void HandleSceneChanged(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to) => Clear();

    private void Clear()
    {
        _islands.Clear(); _mainTris.Clear(); _verts = null; _indices = null; _status = null;
        Repaint(); SceneView.RepaintAll();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("1) NavMesh Bake  2) 분석  3) 볼륨 만들기  4) 다시 Bake\n" +
                                "파티 시작 위치와 이어진 NavMesh만 남기고, 이어지지 않은 조각을 막습니다.", MessageType.None);
        cellSize = EditorGUILayout.FloatField(new GUIContent("볼륨 칸 크기(m)", "고립 조각을 이 크기 격자로 나눠 덮음. 작을수록 정확하지만 볼륨이 많아짐"), cellSize);
        minIslandArea = EditorGUILayout.FloatField(new GUIContent("무시할 최대 넓이(m²)", "이보다 작은 조각은 막지 않음 (0 = 전부 막기)"), minIslandArea);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("분석", GUILayout.Height(28))) Analyze();
            using (new EditorGUI.DisabledScope(_islands.Count == 0))
                if (GUILayout.Button("볼륨 만들기", GUILayout.Height(28))) CreateVolumes();
        }
        if (GUILayout.Button("자동 볼륨 전부 지우기")) DeleteAutoVolumes();

        if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.Info);

        if (_islands.Count > 0)
        {
            EditorGUILayout.LabelField($"고립된 조각 {_islands.Count}개 (클릭하면 씬 뷰 이동)", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _islands.Count; i++)
            {
                var isl = _islands[i];
                string label = $"#{i + 1}  {isl.area:F1}m²  중심 ({isl.bounds.center.x:F1}, {isl.bounds.center.y:F1}, {isl.bounds.center.z:F1})";
                if (GUILayout.Button(label, EditorStyles.miniButton))
                    SceneView.lastActiveSceneView?.Frame(isl.bounds, false);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 분석 — 정점을 위치로 합쳐 서로 맞닿은 삼각형끼리 묶고, 시작 위치가 속한 묶음 외에는 전부 고립 조각
    // ─────────────────────────────────────────────────────────────────

    private void Analyze()
    {
        Clear();
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        _verts = tri.vertices; _indices = tri.indices;
        if (_indices.Length == 0) { _status = "베이크된 NavMesh가 없습니다. 먼저 Bake하세요."; return; }

        var weld = new Dictionary<Vector3Int, int>();
        int[] vid = new int[_verts.Length];
        for (int i = 0; i < _verts.Length; i++)
        {
            var key = new Vector3Int(Mathf.RoundToInt(_verts[i].x * 100f), Mathf.RoundToInt(_verts[i].y * 100f), Mathf.RoundToInt(_verts[i].z * 100f));
            if (!weld.TryGetValue(key, out int id)) { id = weld.Count; weld[key] = id; }
            vid[i] = id;
        }
        int[] parent = new int[weld.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        for (int t = 0; t < _indices.Length; t += 3)
        {
            int a = Find(vid[_indices[t]]);
            parent[Find(vid[_indices[t + 1]])] = a;
            parent[Find(vid[_indices[t + 2]])] = a;
        }

        // 시작 위치 = 파티원 평균 위치에서 가장 가까운 NavMesh 정점의 묶음
        var members = Object.FindObjectsOfType<PartyMemberScript>(true);
        if (members.Length == 0) { _status = "씬에서 파티원을 찾지 못해 시작 위치를 알 수 없습니다."; return; }
        Vector3 start = Vector3.zero;
        foreach (var m in members) start += m.transform.position;
        start /= members.Length;
        int mainRoot = Find(vid[NearestVertex(start)]);

        var byRoot = new Dictionary<int, Island>();
        for (int t = 0; t < _indices.Length; t += 3)
        {
            int root = Find(vid[_indices[t]]);
            Vector3 a = _verts[_indices[t]], b = _verts[_indices[t + 1]], c = _verts[_indices[t + 2]];
            float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            if (root == mainRoot) { _mainTris.Add(t); _mainArea += area; continue; }
            if (!byRoot.TryGetValue(root, out var isl)) { isl = new Island { bounds = new Bounds(a, Vector3.zero) }; byRoot[root] = isl; }
            isl.tris.Add(t); isl.area += area;
            isl.bounds.Encapsulate(a); isl.bounds.Encapsulate(b); isl.bounds.Encapsulate(c);
        }
        foreach (var isl in byRoot.Values) if (isl.area > minIslandArea) _islands.Add(isl);
        _islands.Sort((x, y) => y.area.CompareTo(x.area));

        float islandArea = 0f; foreach (var isl in _islands) islandArea += isl.area;
        _status = $"걸어갈 수 있는 NavMesh {_mainArea:F0}m² / 고립된 조각 {_islands.Count}개, 합계 {islandArea:F0}m² (씬 뷰에 빨갛게 표시)";

        // 포탈이 고립 조각에 있으면 시작 위치 판단이 틀렸거나 맵이 끊긴 것 — 경고
        foreach (var portal in Object.FindObjectsOfType<Portal>(true))
        {
            int r = Find(vid[NearestVertex(portal.transform.position)]);
            if (r != mainRoot) _status += $"\n⚠ 포탈 '{portal.name}'이(가) 시작 위치와 이어지지 않은 곳에 있습니다 — 길이 끊겼는지 확인하세요.";
        }
        SceneView.RepaintAll();
    }

    private int NearestVertex(Vector3 p)
    {
        int best = 0; float bd = float.MaxValue;
        for (int i = 0; i < _verts.Length; i++) { float d = (_verts[i] - p).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
        return best;
    }

    // ─────────────────────────────────────────────────────────────────
    // 볼륨 만들기 — 고립 조각을 격자 칸으로 나눠 칸마다 높이 범위를 구하고, 같은 줄의 이웃 칸은 합쳐서 상자 하나로
    // ─────────────────────────────────────────────────────────────────

    private void CreateVolumes()
    {
        GameObject root = GameObject.Find(AutoRootName);
        if (root == null)
        {
            root = new GameObject(AutoRootName);
            Undo.RegisterCreatedObjectUndo(root, "NavMesh Island Volumes");
        }
        int notWalkable = NavMesh.GetAreaFromName("Not Walkable");
        int created = 0, skipped = 0;

        for (int n = 0; n < _islands.Count; n++)
        {
            // 칸 (x,z) → 높이 범위
            var cells = new Dictionary<Vector2Int, Vector2>();
            foreach (int t in _islands[n].tris)
            {
                Vector3 a = _verts[_indices[t]], b = _verts[_indices[t + 1]], c = _verts[_indices[t + 2]];
                float yMin = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), yMax = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                // 삼각형 안을 칸 크기의 절반 간격으로 훑어 걸치는 칸을 모두 표시
                float step = cellSize * 0.5f;
                float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                float minZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), maxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                for (float x = minX; x <= maxX + step * 0.5f; x += step)
                for (float z = minZ; z <= maxZ + step * 0.5f; z += step)
                {
                    var p = new Vector2(Mathf.Min(x, maxX), Mathf.Min(z, maxZ));
                    if (!InTriangleXZ(p, a, b, c)) continue;
                    Mark(cells, p, yMin, yMax);
                }
                Mark(cells, new Vector2(a.x, a.z), yMin, yMax);
                Mark(cells, new Vector2(b.x, b.z), yMin, yMax);
                Mark(cells, new Vector2(c.x, c.z), yMin, yMax);
            }

            // 같은 z줄에서 x로 이어진 칸 합치기
            var rows = new Dictionary<int, List<int>>();
            foreach (var k in cells.Keys) { if (!rows.ContainsKey(k.y)) rows[k.y] = new List<int>(); rows[k.y].Add(k.x); }
            foreach (var row in rows)
            {
                row.Value.Sort();
                int i = 0;
                while (i < row.Value.Count)
                {
                    int x0 = row.Value[i], x1 = x0;
                    Vector2 yr = cells[new Vector2Int(x0, row.Key)];
                    while (i + 1 < row.Value.Count && row.Value[i + 1] == x1 + 1)
                    {
                        Vector2 next = cells[new Vector2Int(x1 + 1, row.Key)];
                        if (next.x > yr.y + 1f || next.y < yr.x - 1f) break; // 높이가 크게 다르면 따로
                        yr = new Vector2(Mathf.Min(yr.x, next.x), Mathf.Max(yr.y, next.y));
                        x1++; i++;
                    }
                    i++;

                    // 상자: 칸 범위 + 약간의 여유, 높이는 조각 높이 아래 0.4m ~ 위 1m
                    var center = new Vector3((x0 + x1 + 1) * 0.5f * cellSize, (yr.x - 0.4f + yr.y + 1f) * 0.5f, (row.Key + 0.5f) * cellSize);
                    var size   = new Vector3((x1 - x0 + 1) * cellSize + 0.2f, (yr.y + 1f) - (yr.x - 0.4f), cellSize + 0.2f);
                    var box = new Bounds(center, size);
                    if (OverlapsMain(box)) { skipped++; continue; } // 걸어갈 수 있는 바닥까지 막게 되면 만들지 않음

                    var go = new GameObject($"Island{n + 1}_{created + 1}");
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = center;
                    var vol = go.AddComponent<NavMeshModifierVolume>();
                    vol.size = size;
                    vol.center = Vector3.zero;
                    vol.area = notWalkable;
                    Undo.RegisterCreatedObjectUndo(go, "NavMesh Island Volumes");
                    created++;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        _status = $"볼륨 {created}개를 만들었습니다 ('{AutoRootName}' 아래). 이제 NavMesh를 다시 Bake하세요." +
                  (skipped > 0 ? $"\n걸어갈 수 있는 바닥과 겹쳐서 만들지 않은 칸 {skipped}개 — 다시 Bake 후 분석해서 남은 조각을 확인하세요." : "");
        _islands.Clear();
        SceneView.RepaintAll();
    }

    private void Mark(Dictionary<Vector2Int, Vector2> cells, Vector2 p, float yMin, float yMax)
    {
        var k = new Vector2Int(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.y / cellSize));
        cells[k] = cells.TryGetValue(k, out var r) ? new Vector2(Mathf.Min(r.x, yMin), Mathf.Max(r.y, yMax)) : new Vector2(yMin, yMax);
    }

    private static bool InTriangleXZ(Vector2 p, Vector3 a, Vector3 b, Vector3 c)
    {
        float d1 = (p.x - b.x) * (a.z - b.z) - (a.x - b.x) * (p.y - b.z);
        float d2 = (p.x - c.x) * (b.z - c.z) - (b.x - c.x) * (p.y - c.z);
        float d3 = (p.x - a.x) * (c.z - a.z) - (c.x - a.x) * (p.y - a.z);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    // 상자가 걸어갈 수 있는 NavMesh 삼각형과 겹치는지 (삼각형의 경계 상자로 보수적으로 판단)
    private bool OverlapsMain(Bounds box)
    {
        foreach (int t in _mainTris)
        {
            Vector3 a = _verts[_indices[t]], b = _verts[_indices[t + 1]], c = _verts[_indices[t + 2]];
            var tb = new Bounds(a, Vector3.zero); tb.Encapsulate(b); tb.Encapsulate(c);
            if (!tb.Intersects(box)) continue;
            // 경계 상자만 겹치고 실제 삼각형은 상자 밖일 수 있어, 상자 안에 들어오는 점이 있는지 한 번 더 확인
            if (box.Contains(a) || box.Contains(b) || box.Contains(c) || box.Contains((a + b + c) / 3f)) return true;
            Vector3 cc = box.center; cc.y = (a.y + b.y + c.y) / 3f;
            if (cc.y >= box.min.y && cc.y <= box.max.y && InTriangleXZ(new Vector2(cc.x, cc.z), a, b, c)) return true;
        }
        return false;
    }

    private void DeleteAutoVolumes()
    {
        GameObject root = GameObject.Find(AutoRootName);
        if (root == null) { _status = "자동 볼륨이 없습니다."; return; }
        var scene = root.scene;
        Undo.DestroyObjectImmediate(root);
        EditorSceneManager.MarkSceneDirty(scene);
        _status = "자동 볼륨을 지웠습니다. NavMesh를 다시 Bake하세요.";
    }

    private void OnSceneGUI(SceneView view)
    {
        if (_islands.Count == 0 || _verts == null) return;
        Handles.color = new Color(1f, 0.15f, 0.1f, 0.55f);
        var poly = new Vector3[3];
        foreach (var isl in _islands)
            foreach (int t in isl.tris)
            {
                poly[0] = _verts[_indices[t]] + Vector3.up * 0.05f;
                poly[1] = _verts[_indices[t + 1]] + Vector3.up * 0.05f;
                poly[2] = _verts[_indices[t + 2]] + Vector3.up * 0.05f;
                Handles.DrawAAConvexPolygon(poly);
            }
    }
}

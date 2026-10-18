using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터 스포너 자동 배치 툴 — 베이크된 NavMesh 위에서 파티 시작 위치부터 실제로 걸어갈 수 있는 곳만 골라
/// 스포너를 고르게 흩어 놓는다. 몬스터 종류는 건드리지 않음 (새 스포너는 템플릿 스포너를 복제).
/// 1) 미리보기로 씬 뷰에 위치를 확인 → 2) 적용 (Undo 가능)
/// </summary>
public class MonsterSpawnerPlacementTool : EditorWindow
{
    private enum Kind { Keep, Move, New }
    private struct Slot { public Vector3 pos; public Kind kind; public MonsterSpawner spawner; }

    private Transform spawnerRoot;
    private MonsterSpawner template;
    private int targetCount = 28;
    private float minSpacing = 10f;
    private float edgeClearance = 2.5f;     // 벽·낭떠러지에서 이만큼 떨어진 넓은 곳만 (다리 위·좁은 통로 제외)
    private float startSafeRadius = 20f;    // 파티 시작 위치 주변은 비워둠
    private float portalSafeRadius = 10f;   // 포탈 주변도 비워둠 (도착하자마자 전투 방지)
    private bool keepValidExisting = true;
    private int seed = 1;

    private readonly List<Slot> _preview = new List<Slot>();
    private int _unplacedExisting;
    private string _status;

    [MenuItem("Tools/Monster/Spawner Placement")]
    private static void Open() => GetWindow<MonsterSpawnerPlacementTool>("Spawner Placement").minSize = new Vector2(360, 360);

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        if (spawnerRoot == null)
        {
            var go = GameObject.Find("MonsterSpawner");
            if (go != null) spawnerRoot = go.transform;
        }
    }

    private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

    private void OnGUI()
    {
        spawnerRoot = (Transform)EditorGUILayout.ObjectField("스포너 부모", spawnerRoot, typeof(Transform), true);
        template = (MonsterSpawner)EditorGUILayout.ObjectField(new GUIContent("복제할 템플릿", "비우면 부모의 첫 번째 스포너"), template, typeof(MonsterSpawner), true);

        EditorGUILayout.Space();
        targetCount = EditorGUILayout.IntField("목표 총 개수", targetCount);
        minSpacing = EditorGUILayout.FloatField("스포너 간 최소 거리(m)", minSpacing);
        edgeClearance = EditorGUILayout.FloatField(new GUIContent("벽에서 최소 거리(m)", "NavMesh 가장자리에서 이만큼 떨어진 곳만 — 다리·좁은 통로 제외"), edgeClearance);
        startSafeRadius = EditorGUILayout.FloatField("시작 위치 안전 반경(m)", startSafeRadius);
        portalSafeRadius = EditorGUILayout.FloatField("포탈 안전 반경(m)", portalSafeRadius);
        keepValidExisting = EditorGUILayout.Toggle(new GUIContent("유효한 기존 위치 유지", "이미 갈 수 있는 곳에 있는 스포너는 그대로 두고, 맵 밖·갈 수 없는 곳의 스포너만 옮김"), keepValidExisting);
        seed = EditorGUILayout.IntField("랜덤 시드", seed);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("미리보기", GUILayout.Height(28))) Compute();
            using (new EditorGUI.DisabledScope(_preview.Count == 0))
                if (GUILayout.Button("적용", GUILayout.Height(28))) Apply();
        }
        if (_preview.Count > 0 && GUILayout.Button("미리보기 지우기")) { _preview.Clear(); _status = null; SceneView.RepaintAll(); }

        if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.Info);
        EditorGUILayout.HelpBox("씬 뷰: 초록 = 유지, 노랑 = 기존 스포너 이동, 하늘색 = 새로 추가", MessageType.None);
    }

    private void Compute()
    {
        _preview.Clear();
        if (spawnerRoot == null) { _status = "스포너 부모를 지정하세요."; return; }

        var members = Object.FindObjectsOfType<PartyMemberScript>(true);
        if (members.Length == 0) { _status = "씬에서 파티원을 찾지 못해 시작 위치를 알 수 없습니다."; return; }
        Vector3 start = Vector3.zero;
        foreach (var member in members) start += member.transform.position;
        start /= members.Length;
        if (!NavMesh.SamplePosition(start, out NavMeshHit startHit, 5f, NavMesh.AllAreas))
        { _status = "파티 시작 위치 근처에 NavMesh가 없습니다. NavMesh를 먼저 Bake하세요."; return; }
        start = startHit.position;

        var portals = new List<Vector3>();
        foreach (var p in Object.FindObjectsOfType<Portal>(true)) portals.Add(p.transform.position);

        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        if (tri.indices.Length == 0) { _status = "베이크된 NavMesh가 없습니다."; return; }

        var path = new NavMeshPath();
        bool IsGoodSpot(Vector3 pos)
        {
            if (Vector3.Distance(pos, start) < startSafeRadius) return false;
            foreach (var pp in portals) if (Vector3.Distance(pos, pp) < portalSafeRadius) return false;
            if (NavMesh.FindClosestEdge(pos, out NavMeshHit edge, NavMesh.AllAreas) && edge.distance < edgeClearance) return false;
            return true;
        }
        bool IsReachable(Vector3 pos) => NavMesh.CalculatePath(start, pos, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;

        // 기존 스포너 분류
        var spawners = spawnerRoot.GetComponentsInChildren<MonsterSpawner>(true);
        var fixedPos = new List<Vector3>();
        var movable = new List<MonsterSpawner>();
        foreach (var s in spawners)
        {
            Vector3 p = s.transform.position;
            bool valid = keepValidExisting
                && NavMesh.SamplePosition(p, out NavMeshHit h, 1.5f, NavMesh.AllAreas)
                && IsGoodSpot(h.position) && IsReachable(h.position);
            if (valid) { fixedPos.Add(p); _preview.Add(new Slot { pos = p, kind = Kind.Keep, spawner = s }); }
            else movable.Add(s);
        }

        // NavMesh 위에 면적 비례로 후보점 뿌리기
        var rng = new System.Random(seed);
        Vector3[] v = tri.vertices; int[] idx = tri.indices;
        int triCount = idx.Length / 3;
        float[] cum = new float[triCount];
        float total = 0f;
        for (int t = 0; t < triCount; t++)
        {
            Vector3 a = v[idx[t * 3]], b = v[idx[t * 3 + 1]], c = v[idx[t * 3 + 2]];
            total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            cum[t] = total;
        }
        int sampleCount = Mathf.Clamp((int)(total / 4f), 2000, 20000);
        var candidates = new List<Vector3>(sampleCount);
        for (int i = 0; i < sampleCount; i++)
        {
            int t = System.Array.BinarySearch(cum, (float)rng.NextDouble() * total);
            if (t < 0) t = ~t;
            t = Mathf.Min(t, triCount - 1);
            float r1 = Mathf.Sqrt((float)rng.NextDouble()), r2 = (float)rng.NextDouble();
            Vector3 a = v[idx[t * 3]], b = v[idx[t * 3 + 1]], c = v[idx[t * 3 + 2]];
            Vector3 p = (1 - r1) * a + r1 * (1 - r2) * b + r1 * r2 * c;
            if (IsGoodSpot(p)) candidates.Add(p);
        }

        // 가장 먼 점 우선 선택 — 이미 있는 스포너와 서로 최대한 떨어지도록 고르게 분포
        float[] minD = new float[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            float d = float.MaxValue;
            foreach (var f in fixedPos) d = Mathf.Min(d, Vector3.Distance(candidates[i], f));
            minD[i] = d;
        }
        var chosen = new List<Vector3>();
        while (fixedPos.Count + chosen.Count < targetCount)
        {
            int best = -1; float bestD = -1f;
            for (int i = 0; i < minD.Length; i++) if (minD[i] > bestD) { bestD = minD[i]; best = i; }
            if (best < 0 || bestD < minSpacing) break;
            Vector3 p = candidates[best];
            if (!IsReachable(p)) { minD[best] = -1f; continue; } // 따로 떨어진 섬(바위 위 등)
            chosen.Add(p);
            for (int i = 0; i < minD.Length; i++) minD[i] = Mathf.Min(minD[i], Vector3.Distance(candidates[i], p));
        }

        int m = 0;
        foreach (var p in chosen)
        {
            if (m < movable.Count) _preview.Add(new Slot { pos = p, kind = Kind.Move, spawner = movable[m++] });
            else _preview.Add(new Slot { pos = p, kind = Kind.New });
        }
        _unplacedExisting = movable.Count - m;

        int keep = 0, move = 0, add = 0;
        foreach (var s in _preview) { if (s.kind == Kind.Keep) keep++; else if (s.kind == Kind.Move) move++; else add++; }
        _status = $"유지 {keep} / 이동 {move} / 추가 {add} — 총 {_preview.Count}개 (걸어갈 수 있는 NavMesh {total:F0}m²)";
        if (fixedPos.Count + chosen.Count < targetCount)
            _status += $"\n최소 거리 {minSpacing}m 조건으로는 {targetCount}개를 다 놓을 수 없어 여기서 멈췄습니다.";
        if (_unplacedExisting > 0)
            _status += $"\n자리를 못 찾은 기존 스포너 {_unplacedExisting}개는 그대로 남습니다 (갈 수 없는 위치).";
        SceneView.RepaintAll();
    }

    private void Apply()
    {
        MonsterSpawner tpl = template;
        if (tpl == null && spawnerRoot != null) tpl = spawnerRoot.GetComponentInChildren<MonsterSpawner>(true);
        var rng = new System.Random(seed);
        int created = 0, moved = 0;

        foreach (var s in _preview)
        {
            Quaternion rot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            if (s.kind == Kind.Move && s.spawner != null)
            {
                Undo.RecordObject(s.spawner.transform, "Place Monster Spawners");
                s.spawner.transform.SetPositionAndRotation(s.pos, rot);
                moved++;
            }
            else if (s.kind == Kind.New)
            {
                if (tpl == null) { Debug.LogWarning("[SpawnerPlacement] 복제할 템플릿 스포너가 없어 새 스포너를 만들지 못했습니다."); break; }
                var go = Object.Instantiate(tpl.gameObject, spawnerRoot);
                go.name = GameObjectUtility.GetUniqueNameForSibling(spawnerRoot, tpl.gameObject.name);
                go.transform.SetPositionAndRotation(s.pos, rot);
                Undo.RegisterCreatedObjectUndo(go, "Place Monster Spawners");
                created++;
            }
        }
        EditorSceneManager.MarkSceneDirty(spawnerRoot.gameObject.scene);
        _status = $"적용 완료 — 이동 {moved}, 추가 {created}. 새 스포너는 템플릿의 몬스터 종류를 그대로 복사했습니다. 씬을 저장하세요.";
        _preview.Clear();
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView view)
    {
        foreach (var s in _preview)
        {
            Handles.color = s.kind == Kind.Keep ? Color.green : s.kind == Kind.Move ? Color.yellow : Color.cyan;
            Handles.DrawSolidDisc(s.pos + Vector3.up * 0.05f, Vector3.up, 0.8f);
            Handles.DrawWireDisc(s.pos + Vector3.up * 0.05f, Vector3.up, minSpacing * 0.5f);
            Handles.DrawLine(s.pos, s.pos + Vector3.up * 4f);
        }
    }
}

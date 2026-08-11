using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// UI 좌표 투영 방식 미니맵. MinimapCaptureTool로 뽑은 배경 이미지의 월드 좌표 범위(중심/크기)를
// mapWorldCenter/mapWorldSize에 그대로 입력해서 사용한다 (툴 캡처 완료 시 콘솔/창에 표시되는 값).
// 카메라 없이 월드 좌표 → mapRect 내부 좌표로 직접 변환해 아이콘을 배치하므로 렌더링 비용이 없다.
public class MinimapUI : MonoBehaviour
{
    [Header("# 배경 / 좌표")]
    [Tooltip("배경 이미지가 채워진 RectTransform. 이 사각형의 크기를 기준으로 아이콘 좌표를 계산한다")]
    public RectTransform mapRect;
    [Tooltip("아이콘을 붙일 부모 (비워두면 mapRect 사용)")]
    public RectTransform iconLayer;
    [Tooltip("MinimapCaptureTool 캡처 결과의 '중심 (X, Z)' 값을 그대로 입력")]
    public Vector2 mapWorldCenter;
    [Tooltip("MinimapCaptureTool 캡처 결과의 '크기 (Width, Depth)' 값을 그대로 입력")]
    public Vector2 mapWorldSize = new Vector2(100f, 100f);

    [Header("# 아이콘 프리팹")]
    public RectTransform playerIconPrefab;
    [Tooltip("일반 등급 몬스터 아이콘. Elite/Boss용 아이콘을 비워두면 이 아이콘으로 대체됨")]
    public RectTransform monsterIconPrefab;
    [Tooltip("정예(Elite) 등급 몬스터 아이콘 (비워두면 monsterIconPrefab 사용)")]
    public RectTransform eliteMonsterIconPrefab;
    [Tooltip("보스(Boss) 등급 몬스터 아이콘 (비워두면 monsterIconPrefab 사용)")]
    public RectTransform bossMonsterIconPrefab;
    public RectTransform npcIconPrefab;
    public RectTransform questIconPrefab;
    public RectTransform portalIconPrefab;

    [Header("# 씬 이름 표시")]
    public TextMeshProUGUI sceneNameText;

    private RectTransform _leaderIcon;
    private RectTransform _questMarkerIcon;
    private readonly Dictionary<EnemyHp, RectTransform> _monsterIcons = new Dictionary<EnemyHp, RectTransform>();
    private readonly List<EnemyHp> _monsterRemoveScratch = new List<EnemyHp>();

    void Start()
    {
        if (iconLayer == null) iconLayer = mapRect;
        SpawnNpcIcons();
        SpawnPortalIcons();
        UpdateSceneNameText();
    }

    void Update()
    {
        UpdateLeaderIcon();
        UpdateMonsterIcons();
        UpdateQuestMarker();
    }

    // ─────────────────────────────────────────────────────────────────
    // 씬 이름 표시
    // ─────────────────────────────────────────────────────────────────

    private void UpdateSceneNameText()
    {
        if (sceneNameText == null || DataManager.instance == null) return;

        string currentScene = SceneManager.GetActiveScene().name;
        sceneNameText.text  = DataManager.instance.GetSceneDisplayName(currentScene);
    }

    // ─────────────────────────────────────────────────────────────────
    // 좌표 변환
    // ─────────────────────────────────────────────────────────────────

    // MinimapCaptureTool의 캡처 카메라가 Euler(90,0,0)로 아래를 내려다보므로 이미지 위쪽 = 월드 +Z,
    // 이미지 오른쪽 = 월드 +X. mapRect 범위를 벗어나면 화면 밖으로 튀지 않도록 가장자리에 고정한다
    private Vector2 WorldToMapPos(Vector3 worldPos)
    {
        float nx = mapWorldSize.x > 0f ? (worldPos.x - mapWorldCenter.x) / mapWorldSize.x : 0f;
        float nz = mapWorldSize.y > 0f ? (worldPos.z - mapWorldCenter.y) / mapWorldSize.y : 0f;
        nx = Mathf.Clamp(nx, -0.5f, 0.5f);
        nz = Mathf.Clamp(nz, -0.5f, 0.5f);

        Rect rect = mapRect.rect;
        return new Vector2(nx * rect.width, nz * rect.height);
    }

    // ─────────────────────────────────────────────────────────────────
    // 플레이어 (리더)
    // ─────────────────────────────────────────────────────────────────

    private void UpdateLeaderIcon()
    {
        var leader = PartyManager.instance != null ? PartyManager.instance.currentLeader : null;
        if (leader == null)
        {
            if (_leaderIcon != null) _leaderIcon.gameObject.SetActive(false);
            return;
        }

        if (_leaderIcon == null)
        {
            if (playerIconPrefab == null) return;
            _leaderIcon = Instantiate(playerIconPrefab, iconLayer);
        }

        _leaderIcon.gameObject.SetActive(true);
        _leaderIcon.anchoredPosition = WorldToMapPos(leader.transform.position);

        // 캡처 카메라 기준 월드 +Z=화면 위, +X=화면 오른쪽이라 Y축 회전을 그대로 -Z 회전으로 매핑
        float yaw = leader.transform.eulerAngles.y;
        _leaderIcon.localRotation = Quaternion.Euler(0f, 0f, -yaw);
    }

    // ─────────────────────────────────────────────────────────────────
    // 몬스터 — 스폰/사망에 맞춰 아이콘을 동적으로 추가/제거
    // ─────────────────────────────────────────────────────────────────

    private void UpdateMonsterIcons()
    {
        if (monsterIconPrefab == null) return;

        foreach (var enemy in EnemyHp.AllInstances)
        {
            if (enemy == null || enemy.isDead) continue;

            if (!_monsterIcons.TryGetValue(enemy, out var icon) || icon == null)
            {
                RectTransform prefab = GetMonsterIconPrefab(enemy.grade);
                if (prefab == null) continue;

                icon = Instantiate(prefab, iconLayer);
                _monsterIcons[enemy] = icon;
            }

            icon.anchoredPosition = WorldToMapPos(enemy.transform.position);
        }

        // 죽었거나 파괴된 몬스터의 아이콘 정리
        _monsterRemoveScratch.Clear();
        foreach (var kvp in _monsterIcons)
        {
            if (kvp.Key == null || kvp.Key.isDead)
                _monsterRemoveScratch.Add(kvp.Key);
        }
        foreach (var key in _monsterRemoveScratch)
        {
            if (_monsterIcons.TryGetValue(key, out var icon) && icon != null)
                Destroy(icon.gameObject);
            _monsterIcons.Remove(key);
        }
    }

    private RectTransform GetMonsterIconPrefab(EnemyHp.MonsterGrade grade)
    {
        switch (grade)
        {
            case EnemyHp.MonsterGrade.Elite:
                return eliteMonsterIconPrefab != null ? eliteMonsterIconPrefab : monsterIconPrefab;
            case EnemyHp.MonsterGrade.Boss:
                return bossMonsterIconPrefab != null ? bossMonsterIconPrefab : monsterIconPrefab;
            default:
                return monsterIconPrefab;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // NPC — 고정 위치라 최초 1회만 생성
    // ─────────────────────────────────────────────────────────────────

    private void SpawnNpcIcons()
    {
        if (npcIconPrefab == null) return;

        foreach (var npc in NpcInteractable.AllInstances)
        {
            if (npc == null) continue;
            // 퀘스트(Story) NPC는 NPC 아이콘 대신 UpdateQuestMarker()의 퀘스트 아이콘만 표시
            if (npc.Type == NpcInteractable.NpcType.Story) continue;

            var icon = Instantiate(npcIconPrefab, iconLayer);
            icon.anchoredPosition = WorldToMapPos(npc.transform.position);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 포탈 — 고정 위치라 최초 1회만 생성
    // ─────────────────────────────────────────────────────────────────

    private void SpawnPortalIcons()
    {
        if (portalIconPrefab == null) return;

        foreach (var portal in Portal.AllInstances)
        {
            if (portal == null) continue;
            var icon = Instantiate(portalIconPrefab, iconLayer);
            icon.anchoredPosition = WorldToMapPos(portal.transform.position);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 퀘스트 목표 마커 (NPC 대화형 퀘스트만 — QuestHudUI와 동일 기준)
    // ─────────────────────────────────────────────────────────────────

    private void UpdateQuestMarker()
    {
        var quest = QuestManager.instance != null ? QuestManager.instance.CurrentQuest : null;

        if (quest == null || quest.objectiveType != QuestObjectiveType.NpcDialogue)
        {
            if (_questMarkerIcon != null) _questMarkerIcon.gameObject.SetActive(false);
            return;
        }

        NpcInteractable target = null;
        foreach (var npc in NpcInteractable.AllInstances)
        {
            if (npc.NpcId == quest.targetNpcId) { target = npc; break; }
        }

        if (target == null)
        {
            if (_questMarkerIcon != null) _questMarkerIcon.gameObject.SetActive(false);
            return;
        }

        if (_questMarkerIcon == null)
        {
            if (questIconPrefab == null) return;
            _questMarkerIcon = Instantiate(questIconPrefab, iconLayer);
        }

        _questMarkerIcon.gameObject.SetActive(true);
        _questMarkerIcon.anchoredPosition = WorldToMapPos(target.transform.position);
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 이전/다음 맵으로 이동하는 포탈. F키로 상호작용.
// 범위 감지는 WorldItem(아이템 루팅)과 동일하게 트리거 콜라이더 기반 — 이 오브젝트의
// Collider를 Is Trigger로 켜두면 됨.
// 트리거 이벤트는 두 콜라이더 중 최소 하나가 Rigidbody를 가져야 발생하므로,
// 파티원 쪽을 건드리지 않고 포탈 쪽에 자동으로 (움직이지 않는) Rigidbody를 붙여둠.
[RequireComponent(typeof(Rigidbody))]
public class Portal : MonoBehaviour
{
    // 씬 내 모든 포탈 인스턴스 — 미니맵 등이 매번 FindObjectsOfType으로 스캔하지 않도록 자체 등록
    // (NpcInteractable.AllInstances / EnemyHp.AllInstances와 동일한 패턴)
    public static readonly List<Portal> AllInstances = new List<Portal>();

    void OnEnable()  => AllInstances.Add(this);
    void OnDisable() => AllInstances.Remove(this);

    [Header("이동 설정")]
    [Tooltip("이 포탈을 사용하면 로드할 씬 이름")]
    [SerializeField] string destinationSceneName;
    [Tooltip("이동할 곳이 보스전 스테이지인지 여부 — 사용 시 SaveManager.isInBossRoom에 그대로 반영됨 " +
             "(보스전으로 가는 포탈=true, 그 외=false로 설정)")]
    public bool isBossStage = false;

    [Header("스폰 연동 (선택 — 비워두면 도착 씬의 기본 위치에 스폰)")]
    [Tooltip("이 포탈의 고유 ID. 다른 씬의 포탈이 destinationSpawnId로 이 값을 가리키면, " +
             "그 포탈을 타고 이 씬에 들어왔을 때 파티가 이 포탈의 위치에 스폰된다")]
    [SerializeField] string portalId;
    [Tooltip("이 포탈을 사용해서 도착한 씬에서, 파티를 스폰시킬 포탈의 portalId " +
             "(그 씬에 있는 '돌아오는 포탈'의 ID). 예: 마을→숲 포탈의 destinationSpawnId에 " +
             "숲에 있는 '마을로' 포탈의 portalId를 적어두면, 숲에 도착했을 때 그 포탈 위치에 스폰됨")]
    [SerializeField] string destinationSpawnId;

    // 씬 로드를 넘어 유지되는 정적 상태 — 방금 사용한 포탈이 도착 씬에서 스폰시켜야 할 포탈의 ID
    private static string _pendingSpawnPortalId;

    // 도착한 씬에서 호출 — 대기 중인 스폰 요청이 있으면 그 ID를 가진 포탈을 찾아 위치를 반환하고,
    // 요청은 한 번 쓰고 즉시 지운다 (세이브 로드 등 포탈을 거치지 않은 다음 씬 이동에 잘못 재적용되지 않도록)
    public static bool TryConsumePendingSpawn(out Vector3 position)
    {
        position = Vector3.zero;
        if (string.IsNullOrEmpty(_pendingSpawnPortalId)) return false;

        string targetId = _pendingSpawnPortalId;
        _pendingSpawnPortalId = null;

        foreach (var portal in AllInstances)
        {
            if (portal != null && portal.portalId == targetId)
            {
                position = portal.transform.position;
                return true;
            }
        }

        Debug.LogWarning($"[Portal] 도착 씬에서 portalId '{targetId}'를 가진 포탈을 찾지 못했습니다.");
        return false;
    }

    [Header("UI")]
    [Tooltip("범위 안에 들어왔을 때 표시할 [F] 프롬프트 오브젝트")]
    [SerializeField] GameObject promptObject;
    [Tooltip("프롬프트에 표시할 텍스트 — \"[F] 씬이름\" 형식으로 자동 세팅됨")]
    [SerializeField] TextMeshPro promptText;

    // 파티원이 여러 명이라 트리거 범위에 동시에 걸칠 수 있음 — bool 하나로 추적하면
    // 한 명이 먼저 나갈 때 다른 파티원이 아직 안에 있어도 false로 덮어써버리는 문제가 생김.
    // 안에 들어와 있는 파티원 수를 세어서, 0이 될 때만 실제로 프롬프트를 끔.
    private int _insideCount;

    void Awake()
    {
        // 트리거만 감지하면 되고 실제로 움직이거나 물리 영향을 받으면 안 되므로 Kinematic + 중력 끔
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;

        // Collider가 없거나 Is Trigger가 꺼져 있으면 OnTriggerEnter가 아예 호출되지 않는데
        // 콘솔에 아무 단서도 안 남아 원인 파악이 어려우므로 미리 경고
        var col = GetComponent<Collider>();
        if (col == null)
            Debug.LogWarning($"[Portal] {gameObject.name}에 Collider가 없습니다. 상호작용이 동작하지 않습니다.");
        else if (!col.isTrigger)
            Debug.LogWarning($"[Portal] {gameObject.name}의 Collider가 Is Trigger로 설정되지 않았습니다.");
    }

    void Start()
    {
        if (promptText != null)
        {
            string display = DataManager.instance != null
                ? DataManager.instance.GetSceneDisplayName(destinationSceneName)
                : destinationSceneName;
            promptText.text = $"[F] {display}";
        }
        if (promptObject != null) promptObject.SetActive(false);
    }

    void Update()
    {
        bool anyUiOpen = MenuTabUI.IsOpen || ShopUI.IsOpen || DialogueUI.IsOpen || EnhancementUI.IsOpen;
        if (_insideCount > 0 && !anyUiOpen && !SceneLoader.IsLoading && Input.GetKeyDown(KeyCode.F))
            UsePortal();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _insideCount++;
        if (_insideCount == 1 && promptObject != null) promptObject.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _insideCount = Mathf.Max(0, _insideCount - 1);
        if (_insideCount == 0 && promptObject != null) promptObject.SetActive(false);
    }

    private void UsePortal()
    {
        if (string.IsNullOrEmpty(destinationSceneName))
        {
            Debug.LogWarning("[Portal] destinationSceneName이 비어있습니다.");
            return;
        }

        _insideCount = 0;
        if (promptObject != null) promptObject.SetActive(false);

        // 이동 직전 상태를 체크포인트로 자동 저장 — 현재 이미 보스방 안이라면
        // SaveManager.CanSave가 알아서 막아주므로 여기서 따로 분기할 필요 없음.
        // 맵 이동마다 세이브 사운드가 반복되면 거슬리므로 자동 저장은 무음으로 처리
        SaveManager.instance?.Save(playSound: false);

        // 이 포탈의 목적지 기준으로 보스방 여부 갱신 — 보스전 진입/퇴장 양쪽 다 이 한 줄로 처리됨
        if (SaveManager.instance != null)
            SaveManager.instance.isInBossRoom = isBossStage;

        _pendingSpawnPortalId = destinationSpawnId;

        SceneLoader.Load(destinationSceneName);
    }
}

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
    [Header("이동 설정")]
    [Tooltip("이 포탈을 사용하면 로드할 씬 이름")]
    [SerializeField] string destinationSceneName;
    [Tooltip("이동할 곳이 보스전 스테이지인지 여부 — 사용 시 SaveManager.isInBossRoom에 그대로 반영됨 " +
             "(보스전으로 가는 포탈=true, 그 외=false로 설정)")]
    public bool isBossStage = false;

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
    }

    void Start()
    {
        if (promptText != null) promptText.text = $"[F] {destinationSceneName}";
        if (promptObject != null) promptObject.SetActive(false);
    }

    void Update()
    {
        bool anyUiOpen = MenuTabUI.IsOpen || ShopUI.IsOpen || DialogueUI.IsOpen || EnhancementUI.IsOpen;
        if (_insideCount > 0 && !anyUiOpen && Input.GetKeyDown(KeyCode.F))
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

        SceneLoader.Load(destinationSceneName);
    }
}

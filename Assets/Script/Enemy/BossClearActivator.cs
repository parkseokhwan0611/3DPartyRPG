using System.Collections;
using UnityEngine;

// 보스가 죽으면 (선택) 스토리 대사를 출력한 뒤 지정한 오브젝트(다음 맵 포탈 등)를 활성화하고,
// (선택) 확정 지급 아이템을 파티 인벤토리에 바로 넣는다. 보스 몬스터 오브젝트에 붙인다.
// 대상 오브젝트는 씬에 미리 원하는 위치에 배치해 두면 되고, 시작할 때 자동으로 꺼두므로
// 씬에서 활성 상태로 둬도 된다.
[RequireComponent(typeof(EnemyHp))]
public class BossClearActivator : MonoBehaviour
{
    [Tooltip("보스 처치 시 활성화할 오브젝트들 (다음 맵으로 가는 포탈 등)")]
    public GameObject[] activateOnClear;
    [Tooltip("보스가 죽은 뒤 대사 출력(대사가 없으면 활성화)까지 대기 시간(초) — 사망 모션이 끝난 뒤 나타나게 할 때 사용")]
    public float activateDelay = 0f;

    [Header("처치 후 스토리 대사 (비워두면 대사 없이 바로 활성화)")]
    [Tooltip("대사창 이름 칸에 표시할 보스 이름 (Speaker = NPC 줄). 비워두면 EnemyHp.enemyName 사용")]
    public string speakerName;
    [Tooltip("보스 처치 후 출력할 대사 — Speaker NPC = 보스, Character0~2 = 파티원, Custom = 직접 적은 화자(시스템 등). " +
             "대사가 끝나야(또는 스킵) 포탈 등이 활성화된다")]
    public DialogueLine[] clearDialogue;

    [Header("처치 확정 지급 아이템 (비워두면 지급 없음)")]
    [Tooltip("대사가 끝난 뒤(대사가 없으면 활성화 시점) 파티 인벤토리에 바로 들어가는 아이템 — 스토리용 QuestItem 등. " +
             "인벤토리가 가득 차 있으면 보스 자리에 바닥 드랍으로 떨어뜨린다. DataManager.itemRegistry에 등록해야 저장된다")]
    public ItemData rewardItem;
    [Min(1)] public int rewardCount = 1;
    [Tooltip("켜두면 첫 처치 때만 지급 (보스 반복 사냥 시 중복 지급 안 함 — 세이브에 기록됨). 대사는 매번 출력")]
    public bool rewardOnlyOnce = true;


    private EnemyHp _enemyHp;
    private bool _pending; // 사망했지만 아직 활성화 대기 중 (보스 자신에서 코루틴을 돌리는 경우)
    private Vector3 _deathPos;

    void Awake()
    {
        _enemyHp = GetComponent<EnemyHp>();
        SetTargetsActive(activateOnClear, false);
    }

    void OnEnable()  { if (_enemyHp != null) _enemyHp.OnDied += HandleDeath; }
    void OnDisable() { if (_enemyHp != null) _enemyHp.OnDied -= HandleDeath; }

    private void HandleDeath()
    {
        _deathPos = transform.position;
        bool hasDialogue = clearDialogue != null && clearDialogue.Length > 0 && DialogueUI.instance != null;

        if (hasDialogue)
        {
            // 대사가 끝날 때까지 보스 시체를 남겨둔다 (EnemyHp가 destroyDelay 뒤에도 파괴를 미룸)
            if (_enemyHp != null) _enemyHp.holdDestroy = true;
            string bossName = !string.IsNullOrEmpty(speakerName) ? speakerName
                            : (_enemyHp != null && !string.IsNullOrEmpty(_enemyHp.enemyName) ? _enemyHp.enemyName : gameObject.name);
            var clear = new ClearInfo
            {
                targets    = activateOnClear,
                corpse     = _enemyHp,
                reward     = rewardItem,
                count      = rewardCount,
                once       = rewardOnlyOnce,
                dropTable= _enemyHp != null ? _enemyHp.dropTable : null,
                dropPos    = _deathPos,
            };
            // 대기·대사 진행은 씬에 남아있는 DialogueUI에서 돌린다 (보스 쪽 코루틴이 끊기는 경우 대비)
            DialogueUI.instance.StartCoroutine(DialogueThenClear(activateDelay, bossName, clearDialogue, clear));
            return;
        }

        if (activateDelay <= 0f)
        {
            _pending = true;
            Activate();
            return;
        }
        _pending = true;
        StartCoroutine(ActivateAfterDelay());
    }

    // 클리어 처리에 필요한 값 묶음 — 보스 오브젝트가 파괴돼도 쓸 수 있도록 값으로 들고 다님
    private struct ClearInfo
    {
        public GameObject[]     targets;
        public EnemyHp          corpse;
        public ItemData         reward;
        public int              count;
        public bool             once;
        public MonsterDropTable dropTable;
        public Vector3          dropPos;
    }

    private static IEnumerator DialogueThenClear(float delay, string speaker, DialogueLine[] lines, ClearInfo clear)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // 다른 대화창(NPC 등)이 열려 있으면 닫힐 때까지 대기
        while (DialogueUI.IsOpen) yield return null;

        // 대사가 끝나면(또는 스킵) 포탈 등을 켜고, 확정 아이템을 지급하고, 시체 파괴를 허용
        void Finish()
        {
            SetTargetsActive(clear.targets, true);
            GrantReward(clear.reward, clear.count, clear.once, clear.dropTable, clear.dropPos);
            if (clear.corpse != null) clear.corpse.holdDestroy = false;
        }

        if (DialogueUI.instance == null) { Finish(); yield break; }
        DialogueUI.instance.Open(speaker, lines, Finish, skippable: true);
    }

    private IEnumerator ActivateAfterDelay()
    {
        yield return new WaitForSeconds(activateDelay);
        Activate();
    }

    // 보스는 사망 후 EnemyHp.destroyDelay가 지나면 Destroy되므로, 대기 시간이 그보다 길면
    // 코루틴이 끊긴다 — 그 경우 파괴되는 순간에 바로 활성화해서 포탈이 안 나타나는 일이 없게 함
    // (씬 전환으로 파괴될 때는 활성화하지 않음)
    void OnDestroy()
    {
        if (_pending && gameObject.scene.isLoaded) Activate();
    }

    private void Activate()
    {
        if (!_pending) return;
        _pending = false;
        SetTargetsActive(activateOnClear, true);
        GrantReward(rewardItem, rewardCount, rewardOnlyOnce, _enemyHp != null ? _enemyHp.dropTable : null, _deathPos);
    }

    // 확정 아이템을 파티 인벤토리에 바로 넣는다 — 가득 차 있으면 보스 자리에 바닥 드랍 (스토리 아이템 유실 방지)
    private static void GrantReward(ItemData item, int count, bool once, MonsterDropTable dropTable, Vector3 pos)
    {
        if (item == null || DataManager.instance == null) return;

        // 1회 지급 — 이미 받은 기록이 있거나(이 기능 추가 전 세이브 대비) 인벤토리에 이미 있으면 지급하지 않음
        if (once)
        {
            if (DataManager.instance.HasGrantedOnce(item.itemId) || HasItem(item)) return;
            DataManager.instance.MarkGrantedOnce(item.itemId);
        }

        if (DataManager.instance.FindItemById(item.itemId) == null)
            Debug.LogWarning($"[BossClear] '{item.itemId}'가 itemRegistry에 없습니다 — 저장 후 불러오면 사라집니다. DataManager의 '자동 채우기'를 눌러 등록하세요.");

        var inst = new ItemInstance(item, Mathf.Max(1, count));
        if (DataManager.instance.sharedInventory.TryAddItem(inst))
        {
            AudioManager.instance?.PlaySFX("ItemPickup");
            return;
        }

        if (dropTable != null && dropTable.SpawnWorldItem(inst, pos))
            Debug.Log($"[BossClear] 인벤토리가 가득 차 '{item.itemName}'을(를) 바닥에 떨어뜨렸습니다.");
        else
            Debug.LogWarning($"[BossClear] 인벤토리가 가득 차 '{item.itemName}'을(를) 지급하지 못했습니다.");
    }

    private static bool HasItem(ItemData item)
    {
        foreach (var inst in DataManager.instance.sharedInventory.Items)
            if (inst != null && inst.data == item) return true;
        return false;
    }

    private static void SetTargetsActive(GameObject[] targets, bool active)
    {
        if (targets == null) return;
        foreach (var go in targets)
            if (go != null) go.SetActive(active);
    }
}

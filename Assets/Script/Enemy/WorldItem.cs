using TMPro;
using UnityEngine;

public class WorldItem : MonoBehaviour
{
    [Header("# References")]
    public TextMeshPro nameLabel;

    private ItemInstance _item;
    // 범위 안 파티원 — 숫자로만 세면 Player 태그가 붙은 평타 투사체·소환수가 범위 안에서 풀로 돌아가며(비활성화 시
    // OnTriggerExit가 오지 않음) 카운트가 남아, 맵 어디서든 F키로 주워졌음 → 파티원만, 실체로 추적 (Portal과 동일)
    private readonly System.Collections.Generic.HashSet<PartyMemberScript> _inside = new System.Collections.Generic.HashSet<PartyMemberScript>();
    private Camera _camera;
    private PoolAble _poolAble;
    private ParticleSystem[] _particleSystems;

    void Awake()
    {
        _poolAble = GetComponent<PoolAble>();
        _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
    }

    void Start()
    {
        _camera = Camera.main;
    }

    // 풀에서 재사용될 때마다 이전 상태(범위 체크 등)가 남아있지 않도록 초기화
    void OnEnable()
    {
        _inside.Clear();
    }

    public void Setup(ItemInstance item)
    {
        _item = item;
        ApplyGradeVisuals();

        // 풀에서 재사용될 때, 월드 스페이스로 시뮬레이션되는 파티클(빛기둥 등)이 이전 위치에서
        // 재생되던 게 그대로 남아있을 수 있어 새 위치로 옮긴 뒤 전부 초기화하고 다시 재생한다
        foreach (var ps in _particleSystems)
        {
            ps.Clear(true);
            ps.Play(true);
        }
    }

    void Update()
    {
        // 라벨 카메라 방향 고정
        if (_camera != null && nameLabel != null)
            nameLabel.transform.rotation = _camera.transform.rotation;

        // F키 줍기 — 메뉴·상점·대화·강화 창이 열려 있으면 막음 (NPC·포탈과 같은 기준)
        // 범위 안에서 죽어 숨겨진 파티원은 OnTriggerExit 없이 사라지므로 직접 정리
        if (_inside.Count > 0)
            _inside.RemoveWhere(m => m == null || !m.isActiveAndEnabled || m.CurrentState == PartyMemberScript.MemberState.Dead);

        bool anyUiOpen = MenuTabUI.IsOpen || ShopUI.IsOpen || DialogueUI.IsOpen || EnhancementUI.IsOpen || SceneLoader.IsLoading;
        if (_inside.Count > 0 && !anyUiOpen && Input.GetKeyDown(KeyCode.F))
            TryPickup();
    }

    void OnTriggerEnter(Collider other)
    {
        var member = GetPartyMember(other);
        if (member != null) _inside.Add(member);
    }

    void OnTriggerExit(Collider other)
    {
        var member = GetPartyMember(other);
        if (member != null) _inside.Remove(member);
    }

    // Player 태그 중 실제 파티원만 (투사체·소환수 제외)
    private static PartyMemberScript GetPartyMember(Collider other)
    {
        if (!other.CompareTag("Player")) return null;
        return other.GetComponentInParent<PartyMemberScript>();
    }

    private void ApplyGradeVisuals()
    {
        if (_item?.data == null) return;

        // 색상은 프리팹에서 미리 설정 — 이름 텍스트만 동적으로 세팅
        if (nameLabel != null)
            nameLabel.text = _item.data.itemName;
    }

    private void TryPickup()
    {
        if (DataManager.instance == null || _item == null) return;

        if (DataManager.instance.sharedInventory.TryAddItem(_item))
        {
            AudioManager.instance?.PlaySFX("ItemPickup");
            if (_poolAble != null) _poolAble.ReleaseObject();
            else                   Destroy(gameObject);
        }
        else
        {
            Debug.Log("[WorldItem] 인벤토리가 가득 찼습니다.");
        }
    }
}

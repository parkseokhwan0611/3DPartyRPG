using TMPro;
using UnityEngine;

public class WorldItem : MonoBehaviour
{
    [Header("# References")]
    public TextMeshPro nameLabel;

    private ItemInstance _item;
    private int _insideCount;
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
        _insideCount = 0;
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

        // F키 줍기
        if (_insideCount > 0 && Input.GetKeyDown(KeyCode.F))
            TryPickup();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) _insideCount++;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) _insideCount = Mathf.Max(0, _insideCount - 1);
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

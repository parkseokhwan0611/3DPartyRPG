using TMPro;
using UnityEngine;

public class WorldItem : MonoBehaviour
{
    [Header("# References")]
    public TextMeshPro nameLabel;

    private ItemInstance _item;
    private bool _playerInRange;
    private Camera _camera;
    private PoolAble _poolAble;

    void Awake()
    {
        _poolAble = GetComponent<PoolAble>();
    }

    void Start()
    {
        _camera = Camera.main;
    }

    // 풀에서 재사용될 때마다 이전 상태(범위 체크 등)가 남아있지 않도록 초기화
    void OnEnable()
    {
        _playerInRange = false;
    }

    public void Setup(ItemInstance item)
    {
        _item = item;
        ApplyGradeVisuals();
    }

    void Update()
    {
        // 라벨 카메라 방향 고정
        if (_camera != null && nameLabel != null)
            nameLabel.transform.rotation = _camera.transform.rotation;

        // F키 줍기
        if (_playerInRange && Input.GetKeyDown(KeyCode.F))
            TryPickup();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) _playerInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) _playerInRange = false;
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

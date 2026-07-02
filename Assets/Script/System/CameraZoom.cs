using UnityEngine;
using Cinemachine;

public class CameraZoom : MonoBehaviour
{
    [Header("# 카메라 참조")]
    [SerializeField] CinemachineVirtualCamera virtualCamera;

    [Header("# 줌 범위")]
    [SerializeField] Vector3 zoomOutOffset = new Vector3(0f,  7f, -6f);
    [SerializeField] Vector3 zoomInOffset  = new Vector3(0f,  3f, -2f);

    [Header("# 줌 속도")]
    [SerializeField] float scrollSpeed = 3f;  // 휠 1틱당 줌 변화량
    [SerializeField] float smoothTime  = 0.15f; // 보간 시간 (초)

    private CinemachineTransposer transposer;
    private float _zoomT      = 0f; // 0 = 최대 줌아웃, 1 = 최대 줌인
    private float _targetZoomT = 0f;
    private float _velocity    = 0f;

    void Awake()
    {
        if (virtualCamera != null)
            transposer = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
    }

    void Update()
    {
        // UI 열려있으면 줌 입력 차단
        if (MenuTabUI.IsOpen || ShopUI.IsOpen || DialogueUI.IsOpen) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
            _targetZoomT = Mathf.Clamp01(_targetZoomT + scroll * scrollSpeed);

        _zoomT = Mathf.SmoothDamp(_zoomT, _targetZoomT, ref _velocity, smoothTime);

        if (transposer != null)
            transposer.m_FollowOffset = Vector3.Lerp(zoomOutOffset, zoomInOffset, _zoomT);
    }
}

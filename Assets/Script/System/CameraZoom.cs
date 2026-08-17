using UnityEngine;
using Cinemachine;

public class CameraZoom : MonoBehaviour
{
    [Header("# 카메라 참조")]
    [SerializeField] CinemachineVirtualCamera virtualCamera;

    [Header("# 줌 범위 (카메라-타겟 오프셋)")]
    [SerializeField] Vector3 zoomOutOffset = new Vector3(0f, 7f, -6f);
    [SerializeField] Vector3 zoomInOffset  = new Vector3(0f, 1.2f, -2.2f);

    [Header("# 줌인 시 상하 각도 (좌우 회전은 항상 고정)")]
    [Tooltip("줌인했을 때의 카메라 피치(상하 각도, 도). 줌아웃 시엔 기존 고정 각도를 그대로 사용")]
    [SerializeField] float zoomInPitch = 25f;
    [Tooltip("줌 진행도(0=최대 줌아웃, 1=최대 줌인)가 이 값을 넘어야 상하 각도 변화가 시작됨. " +
             "예: 0.5면 줌인을 절반 이상 했을 때부터만 각도가 바뀌기 시작해서 zoomInPitch에 도달")]
    [SerializeField, Range(0f, 0.99f)] float pitchStartT = 0.5f;

    [Header("# 줌 속도")]
    [SerializeField] float scrollSpeed = 3f;  // 휠 1틱당 줌 변화량
    [SerializeField] float smoothTime  = 0.15f; // 보간 시간 (초)

    private CinemachineTransposer transposer;
    private float _zoomT      = 0f; // 0 = 최대 줌아웃, 1 = 최대 줌인
    private float _targetZoomT = 0f;
    private float _velocity    = 0f;

    // 기존에 고정돼있던 각도 — 줌아웃 시 피치 기준값. Yaw/Roll은 항상 이 값 그대로 유지.
    private Quaternion _baseRotation;
    private float       _basePitch;

    void Awake()
    {
        if (virtualCamera != null)
        {
            transposer    = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
            _baseRotation = virtualCamera.transform.rotation;
            _basePitch    = _baseRotation.eulerAngles.x;
        }
    }

    void Update()
    {
        // UI 열려있으면 줌 입력 차단
        if (MenuTabUI.IsOpen || ShopUI.IsOpen || DialogueUI.IsOpen || CombatDetailPopupUI.IsOpen) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
            _targetZoomT = Mathf.Clamp01(_targetZoomT + scroll * scrollSpeed);

        _zoomT = Mathf.SmoothDamp(_zoomT, _targetZoomT, ref _velocity, smoothTime);

        if (transposer != null)
            transposer.m_FollowOffset = Vector3.Lerp(zoomOutOffset, zoomInOffset, _zoomT);

        if (virtualCamera != null)
        {
            // Yaw(Y)/Roll(Z)은 원래 고정값 그대로, Pitch(X)만 줌 정도에 따라 보간 —
            // 단 pitchStartT를 넘기 전까지는 각도를 그대로 유지하고, 넘은 뒤부터 zoomInPitch까지 보간
            float pitchT = Mathf.Clamp01((_zoomT - pitchStartT) / (1f - pitchStartT));
            float pitch  = Mathf.LerpAngle(_basePitch, zoomInPitch, pitchT);
            virtualCamera.transform.rotation =
                Quaternion.Euler(pitch, _baseRotation.eulerAngles.y, _baseRotation.eulerAngles.z);
        }
    }
}

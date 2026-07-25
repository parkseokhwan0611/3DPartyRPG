using UnityEngine;

public class CameraFollowTarget : MonoBehaviour
{
    [Tooltip("실제 따라갈 캐릭터 Transform (리더)")]
    public Transform followTarget;

    [Tooltip("위치 추적 부드러움 — 값이 클수록 느리게 따라감 (0.05~0.15 권장)")]
    [Range(0f, 0.5f)]
    public float smoothTime = 0.08f;

    [Tooltip("Y축(수직) 추적 부드러움 — 루트 모션 상하 진동 흡수용으로 더 높게 설정")]
    [Range(0f, 0.5f)]
    public float smoothTimeY = 0.15f;

    [Tooltip("카메라가 실제로 바라보는 기준점을 캐릭터보다 남쪽(카메라 쪽)으로 물리는 정도(월드 Z). " +
             "값을 올리면 캐릭터가 화면상 더 위쪽에 위치해 남쪽 이동 시 화면 여유공간이 늘어남")]
    [Range(0f, 5f)]
    public float southBiasOffset = 2f;

    private Vector3 _velocity = Vector3.zero;

    void LateUpdate()
    {
        if (followTarget == null) return;

        Vector3 target  = ApplySouthBias(followTarget.position);
        Vector3 current = transform.position;

        // XZ는 빠르게, Y는 느리게 — 루트 모션의 수직 진동만 선별 흡수
        float newX = Mathf.SmoothDamp(current.x, target.x, ref _velocity.x, smoothTime);
        float newY = Mathf.SmoothDamp(current.y, target.y, ref _velocity.y, smoothTimeY);
        float newZ = Mathf.SmoothDamp(current.z, target.z, ref _velocity.z, smoothTime);

        transform.position = new Vector3(newX, newY, newZ);
    }

    // 캐릭터의 실제 위치를 카메라가 바라볼 기준점(남쪽으로 물린 지점)으로 변환 —
    // LateUpdate/WarpTo/SnapCameraToPosition이 모두 같은 기준점을 쓰도록 공용 헬퍼로 분리
    public Vector3 ApplySouthBias(Vector3 rawPosition) => rawPosition + new Vector3(0f, 0f, -southBiasOffset);

    // PartyManager에서 리더 변경 시 호출
    public void SetTarget(Transform target)
    {
        followTarget = target;
        if (target != null)
            WarpTo(ApplySouthBias(target.position));
    }

    // 세이브 로드 등 순간이동 상황에서 호출 — SmoothDamp로 서서히 따라오지 않고 즉시 이동.
    // position은 이미 ApplySouthBias가 적용된 카메라 기준점이어야 함
    public void WarpTo(Vector3 position)
    {
        transform.position = position;
        _velocity = Vector3.zero;
    }
}

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

    private Vector3 _velocity = Vector3.zero;

    void LateUpdate()
    {
        if (followTarget == null) return;

        Vector3 target  = followTarget.position;
        Vector3 current = transform.position;

        // XZ는 빠르게, Y는 느리게 — 루트 모션의 수직 진동만 선별 흡수
        float newX = Mathf.SmoothDamp(current.x, target.x, ref _velocity.x, smoothTime);
        float newY = Mathf.SmoothDamp(current.y, target.y, ref _velocity.y, smoothTimeY);
        float newZ = Mathf.SmoothDamp(current.z, target.z, ref _velocity.z, smoothTime);

        transform.position = new Vector3(newX, newY, newZ);
    }

    // PartyManager에서 리더 변경 시 호출
    public void SetTarget(Transform target)
    {
        followTarget = target;
        if (target != null)
            transform.position = target.position; // 순간이동 방지를 위해 위치 맞춤
    }
}

using UnityEngine;

// 보스 스킬 인디케이터 — 직선형(스킬샷 등) 예고 표시. MonsterSkillBase.OnWindupStart에서 Show()로
// 띄우고, OnWindupEnd()에서 Hide()로 반드시 정리한다 (자동 타이머 없음 — 시전이 취소돼도 안 남게).
// 프리팹 쪽에 pivot이 origin(발사 지점)에 오고, SpriteRenderer를 눕혀서(로컬 X 회전) 쓰는 걸 기준으로
// 로컬 X=폭, 로컬 Y=길이(스프라이트를 세로로 길게 그렸다는 전제) 축을 스케일한다. 원형 인디케이터와
// 같은 이유로, 다른 방식(3D Plane 등)으로 만들었다면 축이 안 맞을 수 있으니 그 경우 알려달라
public class LineSkillIndicator : PoolAble
{
    [Tooltip("크기를 조절할 시각 표현 트랜스폼 (비워두면 자기 자신)")]
    public Transform visual;
    [Tooltip("에디터에서 visual을 지금 배치해둔 상태의 실제 길이(m, 로컬 Y축 방향). " +
             "정확히 잴 필요 없이 눈대중으로 적으면 Show()가 그 비율로 스케일해준다")]
    public float baseLength = 1f;
    [Tooltip("에디터에서 visual을 지금 배치해둔 상태의 실제 폭(m, 로컬 X축 방향)")]
    public float baseWidth = 1f;

    private Vector3 _baseScale;

    void Awake()
    {
        Transform v = visual != null ? visual : transform;
        _baseScale = v.localScale;
    }

    public void Show(Vector3 origin, Vector3 direction, float length, float width)
    {
        if (direction == Vector3.zero) direction = transform.forward;

        transform.position = origin;
        transform.rotation = Quaternion.LookRotation(direction);

        Transform v            = visual != null ? visual : transform;
        float     lengthFactor = baseLength > 0f ? length / baseLength : 1f;
        float     widthFactor  = baseWidth  > 0f ? width  / baseWidth  : 1f;
        v.localScale = new Vector3(_baseScale.x * widthFactor, _baseScale.y * lengthFactor, _baseScale.z);
    }

    public void Hide() => ReleaseObject();
}

using UnityEngine;

// 보스 스킬 인디케이터 — 원형(범위 피해 등) 예고 표시. MonsterSkillBase.OnWindupStart에서 Show()로
// 띄우고, OnWindupEnd()에서 Hide()로 반드시 정리한다 (자동 타이머 없음 — 시전이 취소돼도 안 남게).
public class CircleSkillIndicator : PoolAble
{
    [Tooltip("크기를 조절할 시각 표현 트랜스폼 (비워두면 자기 자신)")]
    public Transform visual;
    [Tooltip("에디터에서 visual을 지금 배치해둔 상태의 실제 반지름(m). 정확히 잴 필요 없이 " +
             "보기 좋은 크기로 스프라이트를 배치한 뒤, 그게 대략 반지름 몇 m짜리인지 눈대중으로 적으면 됨. " +
             "Show()가 이 값 대비 목표 반지름의 비율로 스케일하기 때문에 기준 크기만 맞으면 됨")]
    public float baseRadius = 1f;

    private Vector3 _baseScale;

    void Awake()
    {
        Transform v = visual != null ? visual : transform;
        _baseScale = v.localScale;
    }

    // 원은 어느 축이 "지름 방향"이든 대칭이라, X/Z만 스케일하면 스프라이트 자체의 세로(Y) 축은
    // 그대로 남아 찌그러져 보인다 (특히 SpriteRenderer를 눕혀 쓰는 경우 스프라이트의 가로/세로가
    // 로컬 X/Y라 Z만으로는 대응이 안 됨). 세 축 모두 같은 배율로 스케일해서 항상 원형을 유지한다
    public void Show(Vector3 center, float radius)
    {
        transform.position = center;

        Transform v      = visual != null ? visual : transform;
        float     factor = baseRadius > 0f ? radius / baseRadius : 1f;
        v.localScale = _baseScale * factor;
    }

    public void Hide() => ReleaseObject();
}

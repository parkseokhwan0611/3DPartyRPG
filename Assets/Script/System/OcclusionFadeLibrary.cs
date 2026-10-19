using System.Collections.Generic;
using UnityEngine;

// 카메라와 파티원 사이를 가리는 벽을 반투명하게 할 때 쓰는 설정 + 원본→반투명 머티리얼 목록.
// Resources/OcclusionFadeLibrary.asset 하나만 두면 OcclusionFader가 게임 시작 시 자동으로 읽는다.
// 목록은 Tools → Occlusion 메뉴로 채운다 (빌드에 반투명 셰이더 변형이 포함되도록 머티리얼을 에셋으로 미리 만들어 둠)
[CreateAssetMenu(fileName = "OcclusionFadeLibrary", menuName = "Game/Occlusion Fade Library")]
public class OcclusionFadeLibrary : ScriptableObject
{
    [System.Serializable]
    public struct Pair
    {
        public Material original;
        public Material transparent;
    }

    [Tooltip("끄면 벽 반투명 기능 전체를 쓰지 않음")]
    public bool fadeEnabled = true;

    [Header("# 판정")]
    [Tooltip("반투명하게 만들 수 있는 레이어 (바닥 Plane은 넣지 말 것)")]
    public LayerMask occluderMask = ~0;
    [Tooltip("가림 판정 주기 (초)")]
    public float checkInterval = 0.1f;
    [Tooltip("카메라→파티원 판정 굵기 (m). 클수록 캐릭터 주변 벽까지 넉넉하게 걷어냄")]
    public float castRadius = 0.3f;
    [Tooltip("파티원 발밑에서 이만큼 위를 겨냥 (m)")]
    public float targetHeight = 1.2f;
    [Tooltip("파티원 바로 앞 이 거리는 판정에서 뺌 — 발밑 경사·바닥 장식이 잡히지 않게 (m)")]
    public float stopShort = 0.6f;

    [Header("# 연출")]
    [Range(0f, 1f)]
    [Tooltip("가릴 때의 불투명도 (0.3 = 30%)")]
    public float fadedAlpha = 0.3f;
    [Tooltip("반투명 ↔ 원래대로 바뀌는 시간 (초)")]
    public float fadeDuration = 0.15f;

    [Header("# 원본 → 반투명 머티리얼 (Tools → Occlusion으로 생성)")]
    public List<Pair> pairs = new List<Pair>();
}

using MagicaCloth2;
using UnityEngine;

// MagicaCloth가 본 위치를 읽기 직전에 치마 루트 본들을 캐릭터 기준 뒤쪽으로 기울인다.
// 전투 대기 자세처럼 골반이 앞으로 숙여지는 애니메이션에서 치마가 앞으로 젖혀지는 것을 보정하는 용도.
// 치마 본은 휴머노이드 클립이 건드리지 않으므로, 매 프레임 원래 로컬 회전에서 다시 계산해 누적되지 않게 한다.
public class ClothRootTilt : MonoBehaviour
{
    [Tooltip("기울기 기준이 되는 캐릭터 루트. 비워두면 이 오브젝트를 사용")]
    [SerializeField] Transform characterRoot;
    [Tooltip("MagicaCloth의 Root Bones와 같은 본들을 넣을 것")]
    [SerializeField] Transform[] clothRoots;
    [Tooltip("양수면 뒤로, 음수면 앞으로 기울어짐 (도)")]
    [SerializeField] float backTiltAngle = 15f;

    private Quaternion[] _baseLocalRotations;

    void Awake()
    {
        if (characterRoot == null) characterRoot = transform;

        _baseLocalRotations = new Quaternion[clothRoots.Length];
        for (int i = 0; i < clothRoots.Length; i++)
        {
            if (clothRoots[i] != null)
                _baseLocalRotations[i] = clothRoots[i].localRotation;
        }
    }

    void OnEnable()  => MagicaManager.OnPreSimulation += ApplyTilt;
    void OnDisable() => MagicaManager.OnPreSimulation -= ApplyTilt;

    private void ApplyTilt()
    {
        // 캐릭터 오른쪽 축 기준 양의 회전 = 아래 방향이 뒤쪽으로 넘어감
        Quaternion tilt = Quaternion.AngleAxis(backTiltAngle, characterRoot.right);

        for (int i = 0; i < clothRoots.Length; i++)
        {
            Transform bone = clothRoots[i];
            if (bone == null) continue;

            bone.localRotation = _baseLocalRotations[i];
            bone.rotation      = tilt * bone.rotation;
        }
    }
}

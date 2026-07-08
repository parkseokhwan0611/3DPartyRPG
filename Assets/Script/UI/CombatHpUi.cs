using UnityEngine;

// 전투 중 캐릭터 머리 위에 떠서 카메라를 바라보는 HP/MP 바
public class CombatHpUi : HpMpBarUI
{
    private Transform camTransform;

    protected override void Start()
    {
        base.Start();
        if (Camera.main != null) camTransform = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (camTransform == null) return;
        transform.LookAt(transform.position + camTransform.rotation * Vector3.forward,
                         camTransform.rotation * Vector3.up);
    }
}

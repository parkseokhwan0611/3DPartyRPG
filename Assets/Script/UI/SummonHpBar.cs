using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 소환수 머리 위 체력바 — 소환수 프리팹 안의 월드 스페이스 Canvas에 붙인다 (EnemyHpBar와 같은 방식).
// 체력·보호막·남은 지속시간을 매 프레임 비율로 읽어 부드럽게 따라가고, 항상 카메라를 바라본다
public class SummonHpBar : MonoBehaviour
{
    [Tooltip("비워두면 부모에서 자동으로 찾음")]
    public SummonUnit summon;
    public Image hpBar;
    [Tooltip("선택 — 비워두면 보호막 바 없음. HP 바와 반대 방향으로 깎이도록 Image의 Fill Origin을 " +
             "Right로 설정할 것 (HP는 Left 피벗, 보호막은 Right 피벗)")]
    public Image shieldBar;
    [Tooltip("선택 — 남은 지속시간 바 (비워두면 없음). 소환 시 가득 찼다가 사라질 때 0이 됨")]
    public Image durationBar;
    [Tooltip("선택 — 상태이상 텍스트 (기절 > 둔화 순으로 하나만 표시, 비워두면 없음)")]
    public TMP_Text statusText;
    [Tooltip("체력·보호막 바가 목표 값으로 따라가는 속도 (초당 비율)")]
    public float changeSpeed = 1.5f;

    private Transform camTransform;
    private string shownStatus;
    private float currentHpFill = 1f;
    private float currentShieldFill;

    void Awake()
    {
        if (summon == null) summon = GetComponentInParent<SummonUnit>();
    }

    void Start()
    {
        if (Camera.main != null) camTransform = Camera.main.transform;

        // 소환 직후엔 보간 없이 바로 현재 값으로 — 0에서 차오르거나 보호막이 꽉 찬 것처럼 보이지 않게
        currentHpFill     = HpRatio();
        currentShieldFill = ShieldRatio();
        Apply();
    }

    void Update()
    {
        if (summon == null) return;

        // 사망 모션 중에는 체력바를 바로 감춘다 (몬스터 체력바와 같은 방식)
        if (!summon.IsAlive) { gameObject.SetActive(false); return; }

        currentHpFill     = Mathf.MoveTowards(currentHpFill,     HpRatio(),     changeSpeed * Time.deltaTime);
        currentShieldFill = Mathf.MoveTowards(currentShieldFill, ShieldRatio(), changeSpeed * Time.deltaTime);
        Apply();
    }

    void LateUpdate()
    {
        if (camTransform == null) return;
        transform.LookAt(transform.position + camTransform.rotation * Vector3.forward, camTransform.rotation * Vector3.up);
    }

    private void Apply()
    {
        if (hpBar     != null) hpBar.fillAmount     = currentHpFill;
        if (shieldBar != null) shieldBar.fillAmount = currentShieldFill;
        if (durationBar != null && summon != null)
            durationBar.fillAmount = summon.Duration > 0f ? Mathf.Clamp01(summon.RemainingTime / summon.Duration) : 0f;

        // 상태이상 — 글자가 바뀔 때만 갱신 (TMP 메시 재생성 방지)
        if (statusText != null)
        {
            string status = summon == null ? "" : summon.IsStunned ? "기절" : summon.IsSlowed ? "둔화" : "";
            if (status != shownStatus)
            {
                shownStatus     = status;
                statusText.text = status;
            }
        }
    }

    // 보호막은 최대 체력 대비 비율로 표시 (HP 바와 같은 스케일)
    private float HpRatio()     => summon != null && summon.MaxHp > 0f ? Mathf.Clamp01(summon.Hp / summon.MaxHp) : 0f;
    private float ShieldRatio() => summon != null && summon.MaxHp > 0f ? Mathf.Clamp01(summon.CurrentShield / summon.MaxHp) : 0f;
}

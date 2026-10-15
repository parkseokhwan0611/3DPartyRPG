using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnemyHpBar : MonoBehaviour
{
    public EnemyHp enemyHp;
    public Image hpBar;
    public Image mask;
    [Tooltip("선택 — 비워두면 보호막 바 없음. HP 바와 반대 방향으로 깎이도록 Image의 Fill Origin을 " +
             "Right로 설정할 것 (HP는 Left 피벗, 보호막은 Right 피벗)")]
    public Image shieldBar;
    [Tooltip("머리 위 상태이상 텍스트 (기절/둔화). 비워두면 캔버스 자식 중 'StatusText' 이름의 TMP를 자동으로 찾음")]
    public TMP_Text statusText;
    public float hpAmount;
    private float currentHpFill; // 현재 HP 바의 채우기 정도를 추적하기 위한 변수
    private float currentShieldFill;
    private StatusEffectHandler statusHandler;
    private Quaternion fixedRotation;
    private Transform camTransform;

    public float changeSpeed = 1.5f; // HP 바 변경 속도

    [Tooltip("체력이 가득 차 있고 보호막·상태이상이 없으면 HP 바를 숨긴다 — 몬스터마다 캔버스가 하나씩이라, " +
             "싸우지 않는 몬스터의 캔버스 렌더링을 생략. 맞는 순간 다시 보인다. 끄면 항상 표시")]
    public bool hideWhenFull = true;
    private Canvas _canvas;

    void Start()
    {
        _canvas = GetComponent<Canvas>();
        fixedRotation = transform.rotation;
        // enemyHp.hp는 EnemyHp.Start()에서 초기화되는데 컴포넌트 간 Start() 실행 순서가
        // 보장되지 않아 여기서 읽으면 레이스 컨디션이 생김 — 스폰 시 항상 풀피이므로 1로 고정 시작,
        // 이후 HpChange()가 Update()에서 실제 값으로 자연스럽게 보간됨
        currentHpFill = 1f;
        if (Camera.main != null) camTransform = Camera.main.transform;

        if (enemyHp != null)
        {
            enemyHp.OnDied += HandleDied;
            statusHandler = enemyHp.GetComponent<StatusEffectHandler>();
        }

        if (statusText == null)
        {
            foreach (var tmp in GetComponentsInChildren<TMP_Text>(true))
                if (tmp.name == "StatusText") { statusText = tmp; break; }
        }
        // 프리팹에 미리보기용 글자가 들어 있어도 시작은 빈 칸
        shownStatus = null;
        StatusTextChange();
    }

    void OnDestroy()
    {
        if (enemyHp != null) enemyHp.OnDied -= HandleDied;
    }

    // 사망 시 HP가 0으로 서서히 보간되는 것을 기다리지 않고 체력바를 즉시 감춤
    private void HandleDied()
    {
        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (camTransform == null) return;
        if (_canvas != null && !_canvas.enabled) return; // 숨겨져 있으면 카메라를 바라볼 필요 없음
        transform.LookAt(transform.position + camTransform.rotation * Vector3.forward, camTransform.rotation * Vector3.up);
    }

    void Update()
    {
        HpChange();
        ShieldChange();
        StatusTextChange();
        UpdateVisibility();
    }

    // 캔버스 컴포넌트만 꺼서 그리지 않는다 — 오브젝트는 켜둔 채라 이 스크립트는 계속 돌며 맞는 순간 다시 켠다
    void UpdateVisibility()
    {
        if (_canvas == null) return;

        bool show = !hideWhenFull || enemyHp == null || enemyHp.maxHp <= 0f
                 || enemyHp.hp < enemyHp.maxHp
                 || currentHpFill < 0.999f
                 || (mask != null && mask.fillAmount > currentHpFill + 0.001f) // 깎이는 연출이 남아 있음
                 || currentShieldFill > 0.001f
                 || !string.IsNullOrEmpty(shownStatus);

        if (_canvas.enabled != show) _canvas.enabled = show;
    }

    // ─────────────────────────────────────────────────────────────────
    // 상태이상 텍스트 — 기절 > 둔화 우선순위로 하나만 표시 (파티원 CombatHpUi와 같은 규칙).
    // 공격력·방어력 감소는 표시하지 않음. 몬스터 핸들러엔 변경 이벤트가 없어 매 프레임 확인하되,
    // 글자가 바뀔 때만 text를 갱신해 TMP 메시 재생성을 피한다
    // ─────────────────────────────────────────────────────────────────

    private string shownStatus;

    void StatusTextChange()
    {
        if (statusText == null) return;

        string status = "";
        if (statusHandler != null)
        {
            if (statusHandler.HasDebuff(StatusEffectType.Stun))
                status = "기절";
            else if (statusHandler.HasDebuff(StatusEffectType.Slow) || statusHandler.HasDebuff(StatusEffectType.MoveSpeedDown))
                status = "둔화";
        }

        if (status == shownStatus) return;
        shownStatus     = status;
        statusText.text = status;
    }

    void HpChange()
    {
        if (enemyHp == null || enemyHp.maxHp <= 0f) return;

        hpAmount = enemyHp.hp;
        float targetFill = hpAmount / enemyHp.maxHp;
        // 부드럽게 보간하여 채우기 정도를 변경
        currentHpFill = Mathf.MoveTowards(currentHpFill, targetFill, changeSpeed * Time.deltaTime);
        if (hpBar != null) hpBar.fillAmount = currentHpFill;
        if (mask != null) mask.fillAmount = Mathf.MoveTowards(mask.fillAmount, currentHpFill, 0.8f * Time.deltaTime);
    }

    // 보호막 수치는 MaxHp 대비 비율로 표시 (HP 바와 같은 스케일) — 수치 텍스트는 아직 없음
    void ShieldChange()
    {
        if (shieldBar == null || enemyHp == null || enemyHp.maxHp <= 0f) return;

        float targetFill = statusHandler != null ? Mathf.Clamp01(statusHandler.CurrentShield / enemyHp.maxHp) : 0f;
        currentShieldFill = Mathf.MoveTowards(currentShieldFill, targetFill, changeSpeed * Time.deltaTime);
        shieldBar.fillAmount = currentShieldFill;
    }
}

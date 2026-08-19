using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// CombatHpUi / PartyHPUIScript 공통 로직 — HP/MP 바 부드러운 갱신 + 이벤트 구독
public class HpMpBarUI : MonoBehaviour
{
    public CharacterStat stat;
    public Image hpBar;
    public Image mpBar;
    [Tooltip("선택 — 비워두면 쉴드 바 없음. HP 바와 반대 방향으로 깎이도록 Image의 Fill Origin을 " +
             "Right로 설정할 것 (HP는 Left 피벗, 쉴드는 Right 피벗)")]
    public Image shieldBar;

    private Coroutine hpCoroutine;
    private Coroutine mpCoroutine;
    private Coroutine shieldCoroutine;
    private PartyStatusEffectHandler shieldHandler;

    protected virtual void Start()
    {
        if (stat == null) return;

        if (stat.MaxHp > 0 && hpBar != null)
            hpBar.fillAmount = stat.Hp / stat.MaxHp;

        if (stat.MaxMp > 0 && mpBar != null)
            mpBar.fillAmount = stat.Mp / stat.MaxMp;

        if (shieldHandler == null) shieldHandler = stat.GetComponent<PartyStatusEffectHandler>();

        // hp/mpBar와 마찬가지로 초기값은 코루틴 애니메이션 없이 즉시 세팅 — 그렇지 않으면 Image의
        // 기본 Fill Amount(1)에서 시작해 SmoothBar로 서서히 줄어드는 동안 쉴드가 꽉 찬 것처럼 보인다
        if (shieldBar != null)
            shieldBar.fillAmount = (shieldHandler != null && stat.MaxHp > 0f)
                ? Mathf.Clamp01(shieldHandler.CurrentShield / stat.MaxHp) : 0f;
    }

    protected virtual void OnEnable()
    {
        if (stat == null) return;
        stat.OnHpChanged += UpdateHpUI;
        stat.OnMpChanged += UpdateMpUI;

        if (shieldHandler == null) shieldHandler = stat.GetComponent<PartyStatusEffectHandler>();
        if (shieldHandler != null) shieldHandler.OnShieldChanged += UpdateShieldUI;
    }

    protected virtual void OnDisable()
    {
        if (stat == null) return;
        stat.OnHpChanged -= UpdateHpUI;
        stat.OnMpChanged -= UpdateMpUI;

        if (shieldHandler != null) shieldHandler.OnShieldChanged -= UpdateShieldUI;
    }

    void UpdateHpUI()
    {
        if (stat == null || stat.MaxHp <= 0 || hpBar == null) return;

        float target = stat.Hp / stat.MaxHp;
        if (hpCoroutine != null) StopCoroutine(hpCoroutine);
        hpCoroutine = StartCoroutine(SmoothBar(hpBar, target));
    }

    void UpdateMpUI()
    {
        if (stat == null || stat.MaxMp <= 0 || mpBar == null) return;

        float target = stat.Mp / stat.MaxMp;
        if (mpCoroutine != null) StopCoroutine(mpCoroutine);
        mpCoroutine = StartCoroutine(SmoothBar(mpBar, target));
    }

    // 쉴드 수치는 MaxHp 대비 비율로 표시 (HP/MP 바와 같은 스케일) — 수치 텍스트는 아직 없음
    void UpdateShieldUI()
    {
        if (shieldBar == null || stat == null || stat.MaxHp <= 0f) return;

        float target = shieldHandler != null ? Mathf.Clamp01(shieldHandler.CurrentShield / stat.MaxHp) : 0f;
        if (shieldCoroutine != null) StopCoroutine(shieldCoroutine);
        shieldCoroutine = StartCoroutine(SmoothBar(shieldBar, target));
    }

    IEnumerator SmoothBar(Image bar, float target)
    {
        while (!Mathf.Approximately(bar.fillAmount, target))
        {
            bar.fillAmount = Mathf.MoveTowards(bar.fillAmount, target, Time.deltaTime * 1.5f);
            yield return null;
        }
        bar.fillAmount = target;
    }
}

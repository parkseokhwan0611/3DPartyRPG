using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// CombatHpUi / PartyHPUIScript 공통 로직 — HP/MP 바 부드러운 갱신 + 이벤트 구독
public class HpMpBarUI : MonoBehaviour
{
    public CharacterStat stat;
    public Image hpBar;
    public Image mpBar;
    [Tooltip("디버프 상태 표시 텍스트 (선택 — 비워두면 표시 안 함). 스턴=\"기절\", 슬로우=\"둔화\". " +
             "DamageText처럼 Canvas 없는 3D 월드스페이스 TextMeshPro (UGUI 아님)")]
    public TextMeshPro debuffText;

    private Coroutine hpCoroutine;
    private Coroutine mpCoroutine;
    private PartyStatusEffectHandler statusHandler;

    protected virtual void Start()
    {
        if (stat == null) return;

        if (stat.MaxHp > 0 && hpBar != null)
            hpBar.fillAmount = stat.Hp / stat.MaxHp;

        if (stat.MaxMp > 0 && mpBar != null)
            mpBar.fillAmount = stat.Mp / stat.MaxMp;

        statusHandler = stat.GetComponent<PartyStatusEffectHandler>();
        RefreshDebuffText();
    }

    void OnEnable()
    {
        if (stat == null) return;
        stat.OnHpChanged += UpdateHpUI;
        stat.OnMpChanged += UpdateMpUI;

        if (statusHandler == null) statusHandler = stat.GetComponent<PartyStatusEffectHandler>();
        if (statusHandler != null) statusHandler.OnBuffChanged += HandleBuffChanged;
    }

    void OnDisable()
    {
        if (stat == null) return;
        stat.OnHpChanged -= UpdateHpUI;
        stat.OnMpChanged -= UpdateMpUI;

        if (statusHandler != null) statusHandler.OnBuffChanged -= HandleBuffChanged;
    }

    // ─────────────────────────────────────────────────────────────────
    // 디버프 표시 — 스턴 > 슬로우 우선순위로 하나만 표시 (둘 다 걸려도 한 줄이면 충분)
    // ─────────────────────────────────────────────────────────────────

    private void HandleBuffChanged(StatusEffectType type, bool active) => RefreshDebuffText();

    private void RefreshDebuffText()
    {
        if (debuffText == null || statusHandler == null) return;

        if (statusHandler.HasDebuff(StatusEffectType.Stun))
            debuffText.text = "기절";
        else if (statusHandler.HasDebuff(StatusEffectType.Slow))
            debuffText.text = "둔화";
        else
            debuffText.text = "";
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

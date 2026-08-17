using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// CombatHpUi / PartyHPUIScript 공통 로직 — HP/MP 바 부드러운 갱신 + 이벤트 구독
public class HpMpBarUI : MonoBehaviour
{
    public CharacterStat stat;
    public Image hpBar;
    public Image mpBar;

    private Coroutine hpCoroutine;
    private Coroutine mpCoroutine;

    protected virtual void Start()
    {
        if (stat == null) return;

        if (stat.MaxHp > 0 && hpBar != null)
            hpBar.fillAmount = stat.Hp / stat.MaxHp;

        if (stat.MaxMp > 0 && mpBar != null)
            mpBar.fillAmount = stat.Mp / stat.MaxMp;
    }

    protected virtual void OnEnable()
    {
        if (stat == null) return;
        stat.OnHpChanged += UpdateHpUI;
        stat.OnMpChanged += UpdateMpUI;
    }

    protected virtual void OnDisable()
    {
        if (stat == null) return;
        stat.OnHpChanged -= UpdateHpUI;
        stat.OnMpChanged -= UpdateMpUI;
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

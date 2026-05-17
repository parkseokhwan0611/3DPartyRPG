using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatHpUi : MonoBehaviour
{
    public CharacterStat stat;
    public Image hpBar;
    public Image mpBar;

    private Coroutine hpCoroutine;
    private Coroutine mpCoroutine;
    private Transform camTransform;

    void Start()
    {
        camTransform = Camera.main.transform;

        if (stat != null)
        {
            if (stat.MaxHp > 0 && hpBar != null)
                hpBar.fillAmount = stat.Hp / stat.MaxHp;

            if (stat.MaxMp > 0 && mpBar != null)
                mpBar.fillAmount = stat.Mp / stat.MaxMp;
        }
    }

    void OnEnable()
    {
        if (stat == null) return;
        stat.OnHpChanged += UpdateHpUI;
        stat.OnMpChanged += UpdateMpUI;
    }

    void OnDisable()
    {
        if (stat == null) return;
        stat.OnHpChanged -= UpdateHpUI;
        stat.OnMpChanged -= UpdateMpUI;
    }

    void LateUpdate()
    {
        transform.LookAt(transform.position + camTransform.rotation * Vector3.forward,
                         camTransform.rotation * Vector3.up);
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

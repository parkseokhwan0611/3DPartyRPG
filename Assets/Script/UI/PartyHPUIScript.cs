using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PartyHPUIScript : MonoBehaviour
{
    public CharacterStat stat;
    public Image hpBar;
    
    // 부드러운 연출을 위해 여전히 Lerp/MoveTowards를 쓴다면 코루틴이 효율적입니다.
    private Coroutine hpCoroutine;
    void Start()
    {
        // 게임 시작 시 현재 체력에 맞춰 UI 즉시 초기화
        if (stat != null && stat.MaxHp > 0)
        {
            hpBar.fillAmount = stat.Hp / stat.MaxHp;
        }
    }
    void OnEnable()
    {
        // 이벤트 구독 시작
        stat.OnHpChanged += UpdateHpUI;
    }

    void OnDisable()
    {
        // 오브젝트가 꺼질 때 구독 해제 (메모리 누수 방지)
        stat.OnHpChanged -= UpdateHpUI;
    }

    void UpdateHpUI()
    {
        // 1. stat이나 MaxHp가 0인 경우를 대비해 예외 처리를 해주면 안전합니다.
        if (stat == null || stat.MaxHp <= 0) return;

        // 2. stat.hp(소문자)를 stat.Hp(대문자 프로퍼티)로 변경합니다.
        float targetFill = stat.Hp / stat.MaxHp;
        
        if (hpCoroutine != null) StopCoroutine(hpCoroutine);
        hpCoroutine = StartCoroutine(SmoothUpdateBar(targetFill));
    }

    System.Collections.IEnumerator SmoothUpdateBar(float targetFill)
    {
        while (!Mathf.Approximately(hpBar.fillAmount, targetFill))
        {
            hpBar.fillAmount = Mathf.MoveTowards(hpBar.fillAmount, targetFill, Time.deltaTime * 1.5f);
            yield return null;
        }
    }
}
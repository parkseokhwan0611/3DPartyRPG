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
        // HP가 변했다는 신호를 받았을 때 실행됨
        float targetFill = stat.hp / stat.MaxHp;
        
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
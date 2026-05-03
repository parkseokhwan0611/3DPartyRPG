using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetHpScript : MonoBehaviour
{
    public static TargetHpScript instance;
    [Header("# UI References")]
    public GameObject rootVisual; // 평소엔 꺼두었다가 타겟팅 시 켜짐
    public TMPro.TextMeshProUGUI nameText;
    public UnityEngine.UI.Image hpBarFill;

    private EnemyHp currentTarget;

    void Awake() => instance = this;

    public void SetTarget(EnemyHp newTarget)
    {
        // 1. 기존 타겟 이벤트 구독 해제 (중요: 메모리 누수 방지)
        if (currentTarget != null) currentTarget.OnHpChanged -= UpdateHPBar;

        // 2. 새 타겟 설정
        currentTarget = newTarget;
        rootVisual.SetActive(true);
        nameText.text = currentTarget.enemyName;

        // 3. 새 타겟 이벤트 구독 및 초기화
        currentTarget.OnHpChanged += UpdateHPBar;
        UpdateHPBar(currentTarget.hp, currentTarget.maxHp);
    }

    void UpdateHPBar(float currentHp, float maxHp)
    {
        hpBarFill.fillAmount = currentHp / maxHp;
        
        // 적이 죽으면 UI 끄기
        if (currentHp <= 0) rootVisual.SetActive(false);
    }
}

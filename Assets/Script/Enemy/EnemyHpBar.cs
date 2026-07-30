using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHpBar : MonoBehaviour
{
    public EnemyHp enemyHp;
    public Image hpBar;
    public Image mask;
    public float hpAmount;
    private float currentHpFill; // 현재 HP 바의 채우기 정도를 추적하기 위한 변수
    private Quaternion fixedRotation;
    private Transform camTransform;

    public float changeSpeed = 1.5f; // HP 바 변경 속도

    void Start()
    {
        fixedRotation = transform.rotation;
        // enemyHp.hp는 EnemyHp.Start()에서 초기화되는데 컴포넌트 간 Start() 실행 순서가
        // 보장되지 않아 여기서 읽으면 레이스 컨디션이 생김 — 스폰 시 항상 풀피이므로 1로 고정 시작,
        // 이후 HpChange()가 Update()에서 실제 값으로 자연스럽게 보간됨
        currentHpFill = 1f;
        if (Camera.main != null) camTransform = Camera.main.transform;

        if (enemyHp != null) enemyHp.OnDied += HandleDied;
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
        transform.LookAt(transform.position + camTransform.rotation * Vector3.forward, camTransform.rotation * Vector3.up);
    }

    void Update()
    {
        HpChange();
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
}

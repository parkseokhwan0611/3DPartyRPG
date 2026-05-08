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
        currentHpFill = enemyHp.hp / enemyHp.maxHp; // 현재 HP로 초기화
        camTransform = Camera.main.transform;
    }

    void LateUpdate()
    {
        transform.LookAt(transform.position + camTransform.rotation * Vector3.forward, camTransform.rotation * Vector3.up);
    }

    void Awake() 
    { 
        hpAmount = enemyHp.hp;
    }

    void Update()
    {
        HpChange();
    }

    void HpChange()
    {
        hpAmount = enemyHp.hp;
        float targetFill = hpAmount / enemyHp.maxHp;
        // 부드럽게 보간하여 채우기 정도를 변경
        currentHpFill = Mathf.MoveTowards(currentHpFill, targetFill, changeSpeed * Time.deltaTime);
        hpBar.fillAmount = currentHpFill;
        mask.fillAmount = Mathf.MoveTowards(mask.fillAmount, hpBar.fillAmount, 0.8f * Time.deltaTime);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System; // Action을 사용하기 위해 필요

public class CharacterStat : MonoBehaviour, IDamageable
{
    [Header("# Refences")]
    public GameObject playerDamageText;
    public Transform hudPos;
    public ClassData classData;
    public event Action OnHpChanged;
    // [중요] 이제 모든 스탯 정보는 이 안에 들어있습니다.
    private CharacterStatus myStatus;
    public int partyIndex;
    public float Hp => myStatus.currentHp;
    public float MaxHp => myStatus.MaxHp;
    public float TotalAtk => myStatus.TotalAtk;
    public float TotalAp => myStatus.TotalAp;
    // void Awake()
    // {
    //     // 초기화 (StatManager에서 데이터를 받아올 수도 있음)
    //     hp = MaxHp;
    // }
    // CharacterStat.cs
    void Awake() // Start에서 Awake로 변경
    {
        if (DataManager.instance != null)
        {
            myStatus = DataManager.instance.partyStatuses[partyIndex];
        }
    }
    public void TakeDamage(float damage, GameObject attacker)
    {
        if (myStatus == null) return;

        // 1. 실제 데이터 매니저 내의 HP를 깎음
        myStatus.currentHp -= damage;
        myStatus.currentHp = Mathf.Clamp(myStatus.currentHp, 0, myStatus.MaxHp);

        // 2. 이벤트 호출 (두 군데 모두 호출하는 것이 좋습니다)
        myStatus.OnHpChanged?.Invoke(); // 데이터 중심 알림
        OnHpChanged?.Invoke();          // 로컬(Stat) 중심 알림

        // 3. 연출
        SpawnDamageText(damage);

        // 4. 사망 판정
        if (myStatus.currentHp <= 0) Die();
    }
    private void SpawnDamageText(float damage)
    {
        if (playerDamageText == null) return;

        // 1. 데미지 텍스트 생성 (위치는 몬스터의 hudPos 또는 현재 위치)
        // 1. 원하는 회전값을 쿼터니언으로 변환 (X축으로 60도)
        Quaternion spawnRotation = Quaternion.Euler(60f, 0f, 0f);

        // 2. Instantiate 시점에 해당 회전값 적용
        Vector3 spawnPos = hudPos != null ? hudPos.position : transform.position + Vector3.up * 2f;
        GameObject textObj = Instantiate(playerDamageText, spawnPos, spawnRotation);

        // 2. DamageText 스크립트의 damage 변수에 값 전달
        DamageText dt = textObj.GetComponent<DamageText>();
        if (dt != null)
        {
                // 생성 즉시 Setup을 호출하여 'text' 변수가 null인 상태로 Update가 도는 것을 방지합니다.
            dt.Setup(damage);
        }
    }
    void Die() { /* 사망 로직 */ }
}

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
    void Awake()
    {
        if (DataManager.instance != null)
        {
            // partyIndex가 리스트 범위를 벗어나면 IndexOutOfRangeException 발생
            if (partyIndex < DataManager.instance.partyStatuses.Count)
                myStatus = DataManager.instance.partyStatuses[partyIndex];
        }
    }
    public void TakeDamage(float damage, GameObject attacker)
    {
        if (myStatus == null) return;

        myStatus.currentHp -= damage;
        myStatus.currentHp = Mathf.Clamp(myStatus.currentHp, 0, myStatus.MaxHp);

        myStatus.RaiseHpChanged();
        OnHpChanged?.Invoke();

        // 피격은 항상 빨간색
        SpawnDamageText(damage, Color.red);

        if (myStatus.currentHp <= 0) Die();
    }
    private void SpawnDamageText(float damage, Color color)
    {
        if (playerDamageText == null) return;

        Quaternion spawnRotation = Quaternion.Euler(60f, 0f, 0f);
        Vector3 spawnPos = hudPos != null ? hudPos.position : transform.position + Vector3.up * 2f;
        GameObject textObj = Instantiate(playerDamageText, spawnPos, spawnRotation);

        DamageText dt = textObj.GetComponent<DamageText>();
        if (dt != null)
            dt.Setup(damage, color); // 전달받은 색상 사용
    }

    public Color GetDamageColor()
    {
        if (classData == null) return Color.white;

        switch (classData.classType)
        {
            case ClassData.ClassType.Tanker: return new Color(1f, 0.5f, 0f);
            case ClassData.ClassType.Dealer: return new Color(0.6f, 0f, 1f);
            case ClassData.ClassType.Healer: return new Color(1f, 0.9f, 0f);
            default: return Color.white;
        }
    }
    void Die() { /* 사망 로직 */ }
}

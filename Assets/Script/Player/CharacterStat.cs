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
    public event Action OnMpChanged;
    // [중요] 이제 모든 스탯 정보는 이 안에 들어있습니다.
    private CharacterStatus myStatus;
    public int partyIndex;
    public float Hp => myStatus.currentHp;
    public float MaxHp => myStatus.MaxHp;
    public float Mp    => myStatus.currentMp;
    public float MaxMp => myStatus.MaxMp;
    public float TotalAtk => myStatus.TotalAtk;
    public float TotalAp => myStatus.TotalAp;
    public float TotalDef => myStatus.TotalDef;
    //Critical
    public float TotalCritRate   => myStatus.TotalCritRate;
    public float TotalCritDamage => myStatus.TotalCritDamage;
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

        // 퍼센트 감산 방식 예시 (나중에 필요하면)
        float damageReduction = TotalDef / (TotalDef + 100f); // 100은 조정 가능한 상수
        float finalDamage = damage * (1f - damageReduction);

        myStatus.currentHp -= finalDamage;
        myStatus.currentHp = Mathf.Clamp(myStatus.currentHp, 0, myStatus.MaxHp);

        myStatus.RaiseHpChanged();
        OnHpChanged?.Invoke();

        // 피격은 항상 빨간색
        SpawnDamageText(finalDamage, Color.red);

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
    // 스킬 사용 가능 여부 체크 + MP 차감
    public bool TryUseMp(float cost)
    {
        if (myStatus == null) return false;

        // MP 부족하면 false 반환 → 스킬 발동 안 됨
        if (myStatus.currentMp < cost) return false;

        myStatus.currentMp -= cost;
        myStatus.currentMp = Mathf.Clamp(myStatus.currentMp, 0, myStatus.MaxMp);

        // MP 변경 이벤트 (UI 갱신용)
        OnMpChanged?.Invoke();
        return true;
    }
    public void RaiseHpChanged()
    {
        OnHpChanged?.Invoke();
    }
    void Die() { /* 사망 로직 */ }
}

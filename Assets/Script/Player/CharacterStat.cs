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
    [Header("# Base Stats")]
    public float hp;
    public float MaxHp => classData.hp + classData.baseVit * classData.hpPerVit;
    public event Action OnHpChanged;
    public float mp;
    public float MaxMp;
    public float atk;
    public float TotalAtk => classData.baseStr * classData.atkPerStr; 
    public float TotalAp => classData.baseInt * classData.apPerInt + classData.baseFht * classData.apPerFth; 
    void Awake()
    {
        // 초기화 (StatManager에서 데이터를 받아올 수도 있음)
        hp = MaxHp;
    }
    public void TakeDamage(float damage, GameObject attacker)
    {
        hp -= damage;
        OnHpChanged?.Invoke();

        SpawnDamageText(damage);

        if (hp <= 0) Die();
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

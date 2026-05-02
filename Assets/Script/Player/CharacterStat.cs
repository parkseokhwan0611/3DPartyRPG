using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterStat : MonoBehaviour, IDamageable
{
    [Header("# Base Stats")]
    public float maxHp;
    public float hp;
    void Awake()
    {
        // 초기화 (StatManager에서 데이터를 받아올 수도 있음)
        hp = maxHp;
    }

    public void TakeDamage(float damage, GameObject attacker)
    {
        hp -= damage;
        if (hp <= 0) Die();
    }

    void Die() { /* 사망 로직 */ }
}

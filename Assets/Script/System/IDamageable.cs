using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDamageable
{
    // 공격력(damage)과 공격한 주체(attacker)를 인자로 받습니다.
    void TakeDamage(float damage, GameObject attacker);
}
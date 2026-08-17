using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDamageable
{
    // 물리 피해 — 공격력(damage)과 공격한 주체(attacker)를 인자로 받습니다.
    // isCrit: 치명타 여부 — 데미지 텍스트에 치명타 아이콘을 표시하는 데 사용
    void TakeDamage(float damage, GameObject attacker, bool isCrit = false);

    // 마법 피해 — 물리와 별도의 저항 스탯으로 경감됩니다.
    void TakeMagicDamage(float damage, GameObject attacker, bool isCrit = false);
}
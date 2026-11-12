using UnityEngine;

// 머리 위 상태이상 텍스트(기절·둔화) 색 — 파티원(CombatHpUi)·몬스터(EnemyHpBar)·소환수(SummonHpBar)가 함께 쓴다
public static class StatusTextColors
{
    public static readonly Color Stun = Color.red;
    public static readonly Color Slow = Color.yellow;
    public static readonly Color Revive = new Color(0.45f, 1f, 0.45f); // 부활 패시브 발동 (파티원 전용)
    public static readonly Color Invulnerable = new Color(0.2f, 1f, 0.2f); // 무적 스킬 (파티원 전용)
}

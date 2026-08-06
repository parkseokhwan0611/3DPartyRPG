using UnityEngine;
using System.Collections;

// 포물선을 그리며 폭탄/수류탄류 투사체를 던지는 원거리 몬스터 공격.
// MonsterRangedAttack(직선 투사체)과 동일한 골격이지만, 투사체가 물리 충돌이 아니라
// 던지는 순간의 목표 위치에 고정 착지해 범위 폭발한다는 점이 달라 별도 클래스로 분리했다.
public class MonsterGrenadeAttack : AttackBase
{
    [Header("투척 공격 설정")]
    [Tooltip("ObjectPoolManager에 등록된 수류탄 투사체 풀 키 (GrenadeProjectile 컴포넌트 포함)")]
    public string grenadePoolKey = "MonsterGrenade";
    [Tooltip("투척 시작 위치 (없으면 자신 위치 + 1m 위)")]
    public Transform throwPoint;
    [Tooltip("애니메이션 시작 후 투척까지 딜레이 (초)")]
    public float damageDelay = 0.35f;
    [Tooltip("수류탄이 목표 지점까지 도달하는 데 걸리는 시간(초) — 포물선 궤적의 속도를 결정")]
    public float flightDuration = 0.8f;
    [Tooltip("포물선 최고 높이(m)")]
    public float arcHeight = 3f;
    [Tooltip("착지 시 폭발 판정 반경")]
    public float explosionRadius = 2.5f;
    [Tooltip("착지 지점의 실제 지면 높이를 찾기 위한 레이캐스트 대상 레이어. " +
             "비워두면(Nothing) 보정 없이 타겟의 조준점 높이 그대로 사용 (MonsterDropTable과 동일한 방식)")]
    public LayerMask groundLayer;

    private Coroutine attackCoroutine;
    public bool IsAttacking { get; private set; } = false;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    protected override void Start()
    {
        base.Start();
    }

    protected override void Update()
    {
        base.Update();
    }

    // ─────────────────────────────────────────────────────────────────
    // 공격 중 이동 차단
    // ─────────────────────────────────────────────────────────────────

    protected override void HandleAttackLogic()
    {
        if (IsAttacking) return;
        base.HandleAttackLogic();
    }

    // ─────────────────────────────────────────────────────────────────
    // 공격 실행
    // ─────────────────────────────────────────────────────────────────

    protected override void ExecuteAttack()
    {
        if (IsAttacking) return;

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        attackCoroutine = StartCoroutine(AttackRoutine());
    }

    protected override void StopAttackCoroutine()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        IsAttacking = false;
    }

    private IEnumerator AttackRoutine()
    {
        IsAttacking = true;

        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = true;

        // 타겟 방향 조준
        if (currentTarget != null)
        {
            Vector3 dir = (currentTarget.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        if (anim != null)
        {
            anim.SetTrigger("doNormalAttack");
            anim.SetBool("isWalking", false);
        }

        // 투척 타이밍 대기
        yield return new WaitForSeconds(damageDelay);

        if (currentTarget != null)
            ThrowGrenade();

        // 공격 지속시간 나머지 대기
        float remaining = Mathf.Max(0f, attackDuration - damageDelay);
        yield return new WaitForSeconds(remaining);

        bool isStunned = statusHandler != null && statusHandler.HasDebuff(StatusEffectType.Stun);
        if (!isStunned && agent != null && agent.isOnNavMesh)
            agent.isStopped = false;

        IsAttacking     = false;
        attackCoroutine = null;
        RaiseAttackEnded();
        attackCooldown  = 0f;
    }

    // ─────────────────────────────────────────────────────────────────
    // 투척
    // ─────────────────────────────────────────────────────────────────

    private void ThrowGrenade()
    {
        if (ObjectPoolManager.instance == null || string.IsNullOrEmpty(grenadePoolKey)) return;

        Vector3 spawnPos = throwPoint != null ? throwPoint.position : transform.position + Vector3.up;

        // 던지는 순간의 목표 위치를 그대로 착지 지점으로 고정 — 비행 중 타겟이 움직여도
        // 궤적이 흔들리지 않고, 플레이어는 착지 전에 그 자리를 피할 수 있다
        Vector3 landingPos = TargetPosition;

        // TargetPosition은 캐릭터의 조준점(가슴 높이 등)이라 그대로 쓰면 허공에서 터진 것처럼
        // 보일 수 있음 — 아래로 레이캐스트해서 실제 지면 높이에 맞춘다 (MonsterDropTable과 동일한 방식)
        if (groundLayer.value != 0)
        {
            Vector3 rayOrigin = landingPos + Vector3.up * 10f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, 30f, groundLayer, QueryTriggerInteraction.Ignore))
                landingPos = groundHit.point;
        }

        var grenadeGo = ObjectPoolManager.instance.GetGo(grenadePoolKey);
        if (grenadeGo == null) return;

        grenadeGo.transform.position = spawnPos;
        grenadeGo.transform.rotation = Quaternion.identity;

        GrenadeProjectile grenade = grenadeGo.GetComponent<GrenadeProjectile>();
        if (grenade == null)
        {
            Debug.LogWarning($"[MonsterGrenadeAttack] '{grenadePoolKey}' 프리팹에 GrenadeProjectile 컴포넌트가 없습니다.");
            return;
        }

        grenade.Launch(spawnPos, landingPos, flightDuration, arcHeight, attackDamage, explosionRadius, enemyLayer, gameObject);
    }

    public override void OnHit() { }

    // ─────────────────────────────────────────────────────────────────
    // 에디터 기즈모
    // ─────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    public void ResetAttackState()
    {
        IsAttacking = false;
    }
}

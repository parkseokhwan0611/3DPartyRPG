using UnityEngine;
using System.Collections;

// RangedAttack(원거리 딜러)과 HealerAttack(힐러)의 공용 기지 원거리 공격 로직.
// 두 클래스는 발사 시 SFX 키 하나만 다르고 나머지는 완전히 동일했기 때문에 공유 기지로 합쳤다.
// 컴포넌트 타입(RangedAttack/HealerAttack)은 그대로 유지되므로 기존 프리팹의 참조는 깨지지 않는다.
public abstract class RangedAttackBase : AttackBase
{
    protected CharacterStat myStat;

    [Header("Ranged Settings")]
    public string projectileName = "MagicBall";
    public Transform firePoint;
    public float damageDelay = 0.35f;
    [Tooltip("투사체 발사 이후 애니메이션 후딜레이 — 이 시간이 끝나야 걷는 모션으로 전환되며 이동을 재개함. " +
             "공격 애니메이션 클립 길이에서 damageDelay를 뺀 만큼으로 맞추면 됨")]
    public float recoveryDuration = 0.2f;

    private Coroutine attackCoroutine;
    private bool _isAttacking = false;

    // 발사 시 재생할 SFX 키 — RangedAttack/HealerAttack이 각자 자신의 키를 반환
    protected abstract string NormalAttackSfxKey { get; }

    protected override void Start()
    {
        base.Start();
        myStat = GetComponent<CharacterStat>();

        if (myStat == null)
            Debug.LogError($"[{GetType().Name}] {gameObject.name}에 CharacterStat이 없습니다!");
    }

    protected override void ExecuteAttack()
    {
        if (_isAttacking) return;
        attackCoroutine = StartCoroutine(AttackRoutine());
    }

    protected override void StopAttackCoroutine()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        IsAttackAnimPlaying = false;
        _isAttacking = false;
    }

    private IEnumerator AttackRoutine()
    {
        if (currentTarget == null) yield break;

        _isAttacking = true;
        IsAttackAnimPlaying = true;

        if (agent != null)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        // 공격속도 보너스만큼 모션과 발사·후딜 타이밍을 같이 빠르게
        float speed = AttackAnimSpeed;

        if (anim != null)
        {
            anim.ResetTrigger("doNormalAttack");
            yield return null;
            yield return null;
            ApplyAttackAnimSpeed();
            anim.SetTrigger("doNormalAttack");
        }
        else
        {
            yield return null;
            yield return null;
        }
        AudioManager.instance?.PlaySFX(NormalAttackSfxKey);

        yield return new WaitForSeconds(damageDelay / speed);

        if (currentTarget == null)
        {
            IsAttackAnimPlaying = false;
            _isAttacking = false;
            attackCoroutine = null;
            yield break;
        }

        Vector3 spawnPos      = firePoint != null ? firePoint.position : transform.position;
        Vector3 preciseDir    = (TargetPosition - spawnPos).normalized;
        Quaternion preciseRot = Quaternion.LookRotation(preciseDir);

        if (ObjectPoolManager.instance == null)
        {
            IsAttackAnimPlaying = false;
            _isAttacking = false;
            attackCoroutine = null;
            yield break;
        }

        var effect = ObjectPoolManager.instance.GetGo(projectileName);
        if (effect == null)
        {
            IsAttackAnimPlaying = false;
            _isAttacking = false;
            attackCoroutine = null;
            yield break;
        }

        effect.transform.position = spawnPos;
        effect.transform.rotation = preciseRot;

        float damage = myStat.TotalAp * (1f + myStat.MagicDmgBonus);
        bool  isCrit = Random.value < myStat.TotalCritRate;

        if (isCrit)
        {
            damage *= myStat.TotalCritDamage;
            if (CinemachineShake.Instance != null)
                CinemachineShake.Instance.ShakeCamera(10f, .2f);
        }

        ProjectileScript proj = effect.GetComponent<ProjectileScript>();
        if (proj != null)
            proj.SetProjectileData(damage, gameObject, enemy => OnProjectileHit(enemy, isCrit), isMagic: true, crit: isCrit);

        Rigidbody rb = effect.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position        = spawnPos;
            rb.rotation        = preciseRot;
            rb.velocity        = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 투사체 발사 후 애니메이션이 자연스럽게 마무리되는 후딜레이 — 이 시간이 끝나야
        // IsAttackAnimPlaying이 풀려서 걷는 모션으로 전환되며 이동을 재개한다
        yield return new WaitForSeconds(recoveryDuration / speed);

        IsAttackAnimPlaying = false;
        _isAttacking = false;
        attackCoroutine = null;
    }

    private void OnProjectileHit(EnemyHp enemyStat, bool isCrit)
    {
        if (enemyStat == null || myStat == null) return;

        if (myStat.HpOnHit > 0f)
            myStat.HealHp(myStat.HpOnHit, showAura: false); // 흡혈은 생명 흡수 버프 아우라만 표시

        if (myStat.MpOnHit > 0f)
            myStat.RecoverMp(myStat.MpOnHit, showAura: false, showText: false);

        // 발동형 패시브 (공격속도 증가·독·치명타 번개·쿨 초기화)
        myStat.NotifyBasicAttackHit(enemyStat, isCrit, isMagic: true);
    }

    public override void OnHit() { }
}

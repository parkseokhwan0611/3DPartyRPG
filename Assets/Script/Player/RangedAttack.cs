using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// 파티원 원거리 평타 (마법사·건슬링어·힐러·유틸 메이지 공용).
// 투사체·타이밍·효과음·물리/마법은 현재 클래스 SO(ClassData)에서 읽고, 씬에는 발사 위치(firePoint)만 둔다.
// 예전 HealerAttack/RangedAttackBase는 효과음 키만 달랐던 중복이라 여기로 합쳤다
public class RangedAttack : AttackBase
{
    private CharacterStat myStat;

    [Header("발사 위치 (비우면 캐릭터 위치)")]
    [Tooltip("기본 발사 위치 — 아래 목록에 현재 클래스가 없으면 이걸 사용")]
    public Transform firePoint;

    [System.Serializable]
    public class ClassFirePoint
    {
        public ClassData.ClassType classType;
        [Tooltip("이 클래스일 때 투사체가 나갈 위치 (예: 총구, 지팡이 끝). 무기 오브젝트의 자식으로 두면 무기와 함께 움직임")]
        public Transform firePoint;
    }

    [Tooltip("무기(클래스)별 발사 위치. 무기마다 총구·지팡이 끝 위치가 다를 때만 등록")]
    public List<ClassFirePoint> classFirePoints = new List<ClassFirePoint>();

    // 현재 클래스의 발사 위치 → 없으면 기본 발사 위치 → 그것도 없으면 null(캐릭터 위치)
    private Transform CurrentFirePoint
    {
        get
        {
            ClassData cls = CurrentClass;
            if (cls != null)
                foreach (var e in classFirePoints)
                    if (e != null && e.classType == cls.classType && e.firePoint != null) return e.firePoint;
            return firePoint;
        }
    }

    private Coroutine attackCoroutine;
    private bool _isAttacking = false;

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
        StopAttackSfx();
        IsAttackAnimPlaying = false;
        _isAttacking = false;
    }

    private void EndAttack()
    {
        IsAttackAnimPlaying = false;
        _isAttacking = false;
        attackCoroutine = null;
    }

    private IEnumerator AttackRoutine()
    {
        ClassData cls = CurrentClass;
        if (currentTarget == null || cls == null) yield break;

        _isAttacking = true;
        IsAttackAnimPlaying = true;

        if (agent != null)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        // 공격속도 보너스만큼 모션과 발사·후딜 타이밍을 같이 빠르게
        float speed = AttackAnimSpeed;

        if (anim != null) anim.ResetTrigger("doNormalAttack");
        yield return null;
        yield return null;
        if (anim != null)
        {
            ApplyAttackAnimSpeed();
            anim.SetTrigger("doNormalAttack");
        }
        // 효과음은 클래스 SO의 Delay대로 — Damage Delay와 같게 두면 투사체가 나가는 순간에 재생
        PlayAttackSfx(cls, speed);

        yield return new WaitForSeconds(cls.damageDelay / speed);

        // 발사 직전에 대상이 사라지면 쏘지 않으므로 아직 안 나간 효과음도 취소
        if (currentTarget == null || ObjectPoolManager.instance == null) { StopAttackSfx(); EndAttack(); yield break; }

        var effect = ObjectPoolManager.instance.GetGo(cls.projectilePoolKey);
        if (effect == null) { StopAttackSfx(); EndAttack(); yield break; }

        Transform  fp         = CurrentFirePoint;
        Vector3    spawnPos   = fp != null ? fp.position : transform.position;
        Quaternion preciseRot = Quaternion.LookRotation((TargetPosition - spawnPos).normalized);
        effect.transform.SetPositionAndRotation(spawnPos, preciseRot);

        bool  isMagic = IsMagicBasicAttack;
        float damage  = isMagic ? myStat.TotalAp * (1f + myStat.MagicDmgBonus)
                                : myStat.TotalAtk * (1f + myStat.PhysDmgBonus);
        bool  isCrit  = Random.value < myStat.TotalCritRate;

        if (isCrit)
        {
            damage *= myStat.TotalCritDamage;
            if (CinemachineShake.Instance != null)
                CinemachineShake.Instance.ShakeCamera(10f, .2f);
        }

        ProjectileScript proj = effect.GetComponent<ProjectileScript>();
        if (proj != null)
            proj.SetProjectileData(damage, gameObject, enemy => OnProjectileHit(enemy, isCrit, isMagic), isMagic: isMagic, crit: isCrit);

        Rigidbody rb = effect.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position        = spawnPos;
            rb.rotation        = preciseRot;
            rb.velocity        = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 발사 후 후딜레이 — 끝나야 IsAttackAnimPlaying이 풀려서 걷는 모션으로 전환되며 이동을 재개한다
        yield return new WaitForSeconds(cls.recoveryDuration / speed);

        EndAttack();
    }

    private void OnProjectileHit(EnemyHp enemyStat, bool isCrit, bool isMagic)
    {
        if (enemyStat == null || myStat == null) return;

        if (myStat.HpOnHit > 0f)
            myStat.HealHp(myStat.HpOnHit, showAura: false); // 흡혈은 생명 흡수 버프 아우라만 표시

        if (myStat.MpOnHit > 0f)
            myStat.RecoverMp(myStat.MpOnHit, showAura: false, showText: false);

        // 발동형 패시브 (공격속도 증가·독·치명타 번개·쿨 초기화)
        myStat.NotifyBasicAttackHit(enemyStat, isCrit, isMagic);
    }

    public override void OnHit() { }
}

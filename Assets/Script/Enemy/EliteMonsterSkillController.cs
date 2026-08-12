using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 정예 몬스터 전용 — 기본 공격(AttackBase, BasicMonsterScript로 이미 타겟팅되는 대상)에
// 스킬 1개를 얹어서, 사거리 안이고 쿨다운이 다 됐을 때 확률적으로 기본 공격 대신 스킬을 쓴다.
// BasicMonsterScript(타겟팅/어그로/배회) + MonsterMeleeAttack 등(기본 공격) 위에 추가로 붙이는 컴포넌트.
//
// AttackBase.HandleAttackLogic()은 사거리 안에 들어오는 바로 그 프레임에 곧바로 공격을 실행하고
// IsAttacking을 true로 바꿔버리기 때문에, 이 스크립트가 AttackBase보다 늦게 Update되면
// "공격 시작 여부를 판단하는 시점"을 놓쳐서 스킬을 낄 틈이 아예 없어진다(같은 프레임 안에서
// 기본 공격이 이미 IsAttacking=true로 확정된 뒤에야 이쪽 체크가 도는 꼴). 그래서 AttackBase/
// BasicMonsterScript(둘 다 기본 실행 순서 0)보다 먼저 실행되도록 강제해서, 매 공격 판단
// 타이밍마다 "스킬을 쓸지 기본 공격을 쓸지"를 먼저 결정할 기회를 갖게 한다
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(AttackBase))]
public class EliteMonsterSkillController : MonoBehaviour, ISkillCaster
{
    [Header("# 참조")]
    [Tooltip("이 정예 몬스터가 쓸 스킬. MonsterSkillBase를 상속한 컴포넌트를 만들어 연결")]
    public MonsterSkillBase skill;
    [Tooltip("useChance 확률을 다시 굴리는 간격(초). 매 프레임 굴리면 사거리에 들어오자마자 " +
             "사실상 100% 확률로 발동하는 것과 같아지므로(초당 60번 판정), 이 간격마다 한 번씩만 체크한다")]
    public float rerollInterval = 1f;

    private AttackBase       _attackBase;
    private NavMeshAgent      _agent;
    private Animator          _animator;
    private StatusEffectHandler _statusHandler;
    private EnemyHp           _enemyHp;
    private Coroutine         _castRoutine;
    private float             _rerollTimer;

    void Awake()
    {
        _attackBase    = GetComponent<AttackBase>();
        _agent         = GetComponent<NavMeshAgent>();
        _animator      = GetComponent<Animator>();
        _statusHandler = GetComponent<StatusEffectHandler>();
        _enemyHp       = GetComponent<EnemyHp>();
    }

    void OnEnable()
    {
        if (_enemyHp != null) _enemyHp.OnDied += ForceCancelSkill;
    }

    void OnDisable()
    {
        if (_enemyHp != null) _enemyHp.OnDied -= ForceCancelSkill;
    }

    void Update()
    {
        if (GameManager.instance == null || !GameManager.instance.isLive) return;
        if (skill == null || _attackBase == null) return;
        if (_enemyHp != null && _enemyHp.isDead) return; // 죽은 뒤엔 새 스킬 시전 자체를 시작하지 않음

        skill.Tick(Time.deltaTime);

        // IsAttacking 등으로 return하는 프레임에도 항상 줄어들어야 함 — 조건부 return 아래에 두면
        // 근접 공격 중인 대부분의 프레임에서 감소가 안 일어나 사실상 리롤이 거의 안 되는 버그가 생김
        if (_rerollTimer > 0f) _rerollTimer -= Time.deltaTime;

        if (_castRoutine != null) return; // 이미 시전 중
        if (_attackBase.currentTarget == null) return;
        if (_attackBase.IsCastingSkill) return; // 다른 곳(스턴 등)에서 이미 점유 중
        // 기본 공격이 윈드업~후딜레이(recoveryDuration)까지 진행 중이면 끼어들지 않음 —
        // 안 그러면 기본 공격 애니메이션이 끝나기 전에 스킬이 겹쳐 들어가서 뒤엉켜 보인다
        if (_attackBase.IsAttacking) return;
        if (_statusHandler != null && _statusHandler.HasDebuff(StatusEffectType.Stun)) return;
        if (!skill.IsReady(transform.position, _attackBase.currentTarget.position)) return;
        if (_rerollTimer > 0f) return; // 리롤 대기 중

        _rerollTimer = rerollInterval;

        if (Random.value <= skill.useChance)
            _castRoutine = StartCoroutine(CastRoutine(_attackBase.currentTarget));
    }

    private IEnumerator CastRoutine(Transform target)
    {
        _attackBase.IsCastingSkill = true; // AttackBase.HandleAttackLogic 일시정지 — 기본 공격 로직과 충돌 방지
        skill.StartCooldown();

        // ResetPath/velocity만으로는 그 순간의 경로만 지워질 뿐이라, MonsterRangedAttack/
        // MonsterGrenadeAttack의 시전 로직과 동일하게 isStopped까지 걸어서 시전 내내(예고~실행) 확실히 정지시킨다
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
        }

        skill.OnWindupStart(target);
        if (_animator != null && !string.IsNullOrEmpty(skill.animTrigger))
            _animator.SetTrigger(skill.animTrigger);

        yield return new WaitForSeconds(skill.windupDuration);

        // 예고 시간 동안 타겟이 죽거나 사라졌을 수 있음
        if (target != null)
            skill.ExecuteSkill(target);

        if (!string.IsNullOrEmpty(skill.skillSfxKey))
            AudioManager.instance?.PlaySFX(skill.skillSfxKey);

        // 이펙트 자체가 유지되는 시간이 있으면 인디케이터를 그만큼 더 붙잡아둠 (0이면 즉시 통과)
        if (skill.attackDuration > 0f)
            yield return new WaitForSeconds(skill.attackDuration);

        skill.OnWindupEnd(); // 인디케이터 등 예고 연출 정리

        // 발사/실행 이후에도 애니메이션이 끝날 때까지는 계속 정지 상태 유지 —
        // 여기서 바로 풀면 던지는 모션이 재생되는 도중에 이동을 시작해버려 어색해 보인다
        yield return new WaitForSeconds(skill.recoveryDuration);

        bool isStunned = _statusHandler != null && _statusHandler.HasDebuff(StatusEffectType.Stun);
        if (!isStunned && _agent != null && _agent.enabled && _agent.isOnNavMesh)
            _agent.isStopped = false;

        _attackBase.IsCastingSkill = false;
        _castRoutine = null;
    }

    // 스턴 등으로 외부에서 강제 중단이 필요할 때 사용
    public void ForceCancelSkill()
    {
        if (_castRoutine != null)
        {
            StopCoroutine(_castRoutine);
            _castRoutine = null;
            skill?.OnWindupEnd();       // 취소돼도 인디케이터 등 예고 연출은 반드시 정리
            skill?.OnForceCancelled();  // 지연 실행 중이던 효과(데미지 등)가 있다면 함께 정지
        }
        if (_attackBase != null) _attackBase.IsCastingSkill = false;
        // isStopped는 여기서 풀지 않음 — 스턴 때문에 취소된 거라면 BasicMonsterScript.HandleStunEnded가
        // 스턴이 끝날 때 알아서 풀어줌. 여기서 미리 풀면 스턴 중에도 움직여버릴 수 있음
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 보스 몬스터 전용 — EliteMonsterSkillController와 기본 골격은 같지만(사거리/쿨다운 다 된 스킬을
// 기본 공격 대신 사용), 스킬이 여러 개라는 점과 체력 구간별 스킬 해금 + 광폭화가 추가된 점이 다르다.
// 실행 순서를 강제하는 이유는 EliteMonsterSkillController와 동일(AttackBase.HandleAttackLogic이
// 사거리 안에 들어오는 프레임에 곧바로 기본 공격을 확정해버리는 것을 막기 위함).
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(AttackBase))]
public class BossMonsterSkillController : MonoBehaviour, ISkillCaster
{
    [Header("# 스킬 목록")]
    [Tooltip("이 보스가 쓸 스킬들. 각 스킬은 자기 쿨다운/사거리/useChance/unlockHpRatio를 갖는다")]
    public List<MonsterSkillBase> skills = new List<MonsterSkillBase>();
    [Tooltip("쿨다운이 다 된 스킬이 있는지 다시 확인하는 간격(초) — EliteMonsterSkillController의 " +
             "rerollInterval과 동일한 이유(매 프레임 굴리면 사실상 즉시 발동과 같아짐)")]
    public float rerollInterval = 1f;

    [Header("# 광폭화 (체력 구간)")]
    [Tooltip("이 체력 비율 이하로 떨어지면 광폭화 진입 — 스킬 쿨다운 단축 + 공격력 증가 (1회성, 이후 되돌아가지 않음)")]
    [Range(0f, 1f)] public float enrageHpRatio = 0.3f;
    [Tooltip("광폭화 시 스킬 쿨다운에 곱할 배율 (0.7 = 쿨다운 30% 감소)")]
    public float enrageCooldownScale = 0.7f;
    [Tooltip("광폭화 시 기본 공격력(AttackBase.attackDamage)에 곱할 배율")]
    public float enrageDamageMultiplier = 1.3f;
    [Tooltip("광폭화 진입 시 활성화할 아우라 이펙트 오브젝트 (보스 자식으로 미리 배치하고 기본 비활성화 " +
             "상태로 둘 것 — Play On Awake 파티클이면 SetActive만으로 재생된다). 비워두면 아우라 없음")]
    public GameObject enrageAuraObject;
    [Tooltip("AudioManager에 등록한 SFX 키. 광폭화 진입 시 1회 재생 (비워두면 재생 안 함)")]
    public string enrageSfxKey;

    private AttackBase          _attackBase;
    private NavMeshAgent         _agent;
    private Animator             _animator;
    private StatusEffectHandler  _statusHandler;
    private EnemyHp               _enemyHp;
    private Coroutine            _castRoutine;
    private MonsterSkillBase     _activeSkill;
    private float                _rerollTimer;
    private bool                 _isEnraged;

    private readonly List<MonsterSkillBase> _candidateBuffer = new List<MonsterSkillBase>();

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
        if (_enemyHp == null) return;
        _enemyHp.OnDied      += ForceCancelSkill;
        _enemyHp.OnHpChanged += HandleHpChanged;
    }

    void OnDisable()
    {
        if (_enemyHp == null) return;
        _enemyHp.OnDied      -= ForceCancelSkill;
        _enemyHp.OnHpChanged -= HandleHpChanged;
    }

    // ─────────────────────────────────────────────────────────────────
    // 광폭화 — 체력이 임계값 이하로 떨어지는 순간 1회만 발동
    // ─────────────────────────────────────────────────────────────────

    private void HandleHpChanged(float hp, float maxHp)
    {
        if (_isEnraged || maxHp <= 0f) return;
        if (hp / maxHp > enrageHpRatio) return;

        _isEnraged = true;

        if (_attackBase != null)
            _attackBase.attackDamage *= enrageDamageMultiplier;

        for (int i = 0; i < skills.Count; i++)
            skills[i]?.SetCooldownScale(enrageCooldownScale);

        if (enrageAuraObject != null)
            enrageAuraObject.SetActive(true);

        if (!string.IsNullOrEmpty(enrageSfxKey))
            AudioManager.instance?.PlaySFX(enrageSfxKey);
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 선택 / 시전
    // ─────────────────────────────────────────────────────────────────

    void Update()
    {
        if (GameManager.instance == null || !GameManager.instance.isLive) return;
        if (_attackBase == null || skills.Count == 0) return;
        if (_enemyHp != null && _enemyHp.isDead) return; // 죽은 뒤엔 새 스킬 시전 자체를 시작하지 않음

        for (int i = 0; i < skills.Count; i++)
            skills[i]?.Tick(Time.deltaTime);

        // IsAttacking 등으로 return하는 프레임에도 항상 줄어들어야 함 (EliteMonsterSkillController와 동일 이유)
        if (_rerollTimer > 0f) _rerollTimer -= Time.deltaTime;

        if (_castRoutine != null) return; // 이미 시전 중
        if (_attackBase.currentTarget == null) return;
        if (_attackBase.IsCastingSkill) return; // 다른 곳(스턴 등)에서 이미 점유 중
        if (_attackBase.IsAttacking) return;    // 기본 공격 윈드업~후딜 중이면 끼어들지 않음
        if (_statusHandler != null && _statusHandler.HasDebuff(StatusEffectType.Stun)) return;
        if (_rerollTimer > 0f) return; // 리롤 대기 중

        _rerollTimer = rerollInterval;

        MonsterSkillBase chosen = PickReadySkill();
        if (chosen != null)
            _castRoutine = StartCoroutine(CastRoutine(chosen, _attackBase.currentTarget));
    }

    // 체력 구간상 해금됐고, 쿨다운/사거리가 다 됐고, 확률(useChance)까지 통과한 스킬들 중 하나를 랜덤 선택
    private MonsterSkillBase PickReadySkill()
    {
        float hpRatio = _enemyHp != null && _enemyHp.maxHp > 0f ? _enemyHp.hp / _enemyHp.maxHp : 1f;

        _candidateBuffer.Clear();
        for (int i = 0; i < skills.Count; i++)
        {
            MonsterSkillBase s = skills[i];
            if (s == null) continue;
            if (!s.IsUnlockedAt(hpRatio)) continue;
            if (!s.IsReady(transform.position, _attackBase.currentTarget.position)) continue;
            if (Random.value <= s.useChance) _candidateBuffer.Add(s);
        }

        if (_candidateBuffer.Count == 0) return null;
        return _candidateBuffer[Random.Range(0, _candidateBuffer.Count)];
    }

    private IEnumerator CastRoutine(MonsterSkillBase skill, Transform target)
    {
        _activeSkill = skill;
        _attackBase.IsCastingSkill = true; // AttackBase.HandleAttackLogic 일시정지
        skill.StartCooldown();

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
        }

        skill.OnWindupStart(target); // 인디케이터 표시 등
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

        skill.OnWindupEnd(); // 인디케이터 정리

        yield return new WaitForSeconds(skill.recoveryDuration);

        bool isStunned = _statusHandler != null && _statusHandler.HasDebuff(StatusEffectType.Stun);
        if (!isStunned && _agent != null && _agent.enabled && _agent.isOnNavMesh)
            _agent.isStopped = false;

        _attackBase.IsCastingSkill = false;
        _activeSkill = null;
        _castRoutine = null;
    }

    // 스턴/사망 등으로 외부에서 강제 중단이 필요할 때 사용
    public void ForceCancelSkill()
    {
        if (_castRoutine != null)
        {
            StopCoroutine(_castRoutine);
            _castRoutine = null;
            _activeSkill?.OnWindupEnd();       // 취소돼도 인디케이터는 반드시 정리
            _activeSkill?.OnForceCancelled();  // 지연 실행 중이던 효과(데미지 등)가 있다면 함께 정지
            _activeSkill = null;
        }
        if (_attackBase != null) _attackBase.IsCastingSkill = false;
        // isStopped는 여기서 풀지 않음 — 스턴 때문에 취소된 거라면 BasicMonsterScript.HandleStunEnded가
        // 스턴이 끝날 때 알아서 풀어줌. 여기서 미리 풀면 스턴 중에도 움직여버릴 수 있음
    }
}

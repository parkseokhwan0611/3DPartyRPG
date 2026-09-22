using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 소환사(루나리스)의 소환수. 롤 요릭의 구울처럼 움직인다.
// - 주인이 노리는 적 → 리더가 노리는 적 → 주인 근처에서 가장 가까운 적 순서로 대상을 고른다
// - 파티원과는 겹쳐 지나가고(길 찾기 회피 우선순위 최하), 소환수끼리·적과는 서로 비켜 간다
// - 적 무리에 막혀 1초 넘게 다가가지 못하면 가장 가까운 다른 적으로 대상을 바꾼다
// - 체력이 있어 몬스터에게 맞는다. 지속시간이 끝나거나, 체력이 0이 되거나, 주인이 죽으면 사라진다
//
// 프리팹 준비: 이 컴포넌트 + NavMeshAgent + Collider(몬스터 공격 판정용).
// 레이어는 파티원과 같은 레이어로 — 몬스터 근접·광역 공격이 그 레이어를 대상으로 판정한다.
// 몬스터는 평소엔 파티원만 노리고, 도발(SummonSkillData Taunt)로 어그로를 받았을 때만 소환수를 공격한다
[RequireComponent(typeof(NavMeshAgent))]
public class SummonUnit : MonoBehaviour, IDamageable
{
    // 살아 있는 소환수 전체 — 파티 버프 적용·도발·재소환 판단에 사용
    public static readonly List<SummonUnit> All = new List<SummonUnit>();

    public enum Kind { Melee, Ranged }

    [Header("종류")]
    public Kind kind = Kind.Melee;

    [Header("공격")]
    [Tooltip("공격 사거리 (대상 몸 크기는 자동으로 더함)")]
    public float attackRange    = 1.2f;
    [Tooltip("공격 간격 (초)")]
    public float attackInterval = 1f;
    [Tooltip("공격 모션 시작 후 타격(근접)·발사(원거리)까지 걸리는 시간 (초)")]
    public float damageDelay    = 0.3f;
    [Tooltip("원거리 소환수 전용: 투사체 풀 키 (비우면 즉시 타격)")]
    public string projectilePoolKey;
    [Tooltip("원거리 소환수 전용: 발사 위치 (비우면 소환수 위치 + 위로 1m)")]
    public Transform firePoint;
    [Tooltip("공격 효과음 키 (비우면 없음)")]
    public string attackSfxKey;
    [Tooltip("타격 시 대상 위치 이펙트 풀 키 (즉시 타격일 때만, 비우면 없음)")]
    public string hitEffectPoolKey;

    [Header("이동")]
    [Tooltip("주인 기준 이 반경 안의 적만 스스로 찾아서 공격")]
    public float searchRadius   = 10f;
    [Tooltip("주인과 이보다 멀어지면 주인 곁으로 순간이동")]
    public float leashDistance  = 20f;
    [Tooltip("싸울 대상이 없을 때 주인에게서 이 거리 이상 떨어지면 따라감")]
    public float followDistance = 2.5f;

    [Header("애니메이터 파라미터 (컨트롤러에 없으면 무시)")]
    public string walkBool      = "isWalking";
    public string attackTrigger = "doAttack";

    [Header("연출")]
    [Tooltip("소환될 때 스폰할 이펙트 풀 키")]
    public string spawnEffectPoolKey;
    [Tooltip("사라질 때(사망·시간 종료) 스폰할 이펙트 풀 키")]
    public string despawnEffectPoolKey;
    [Tooltip("피격 데미지 텍스트 풀 키")]
    public string damageTextPoolKey = "PlayerDamageText";
    [Tooltip("피격 텍스트 위치 (비우면 머리 위 2m)")]
    public Transform hudPos;
    public Color physicalDamageColor = new Color32(0xFF, 0x6E, 0x01, 0xFF);
    public Color magicDamageColor    = new Color32(0x00, 0xC1, 0xFF, 0xFF);

    // ─────────────────────────────────────────────────────────────────
    // 런타임 상태
    // ─────────────────────────────────────────────────────────────────

    public CharacterStat   Owner       { get; private set; }
    public SummonSkillData SourceSkill { get; private set; }
    public float MaxHp         { get; private set; }
    public float Hp            { get; private set; }
    public float Duration      { get; private set; }
    public float RemainingTime { get; private set; }
    public bool  IsAlive => !_dead && Owner != null;
    public float CurrentShield => _shield != null ? _shield.Current : 0f;
    public event Action<float, float> OnHpChanged; // (현재, 최대) — 체력바 UI용

    private NavMeshAgent _agent;
    private Animator     _anim;
    private AttackBase   _ownerAttack;
    private ShieldPool   _shield;
    private bool _dead;

    private bool  _useAp;
    private float _atkRatio;
    private float _defRatio;
    private float _baseSpeed;

    // 대상
    private Transform _target;
    private EnemyHp   _targetHp;
    private float     _targetRadius;
    private Transform _ignoreTarget;      // 막혀서 포기한 대상 — 잠시 다시 고르지 않음
    private float     _ignoreUntil;
    private float     _retargetTimer;
    private Vector3   _lastDestination = Vector3.positiveInfinity;

    // 막힘 감지
    private float _stuckTimer;
    private float _stuckCheckDist = float.MaxValue;
    private const float StuckCheckInterval = 1f;
    private const float StuckMinProgress   = 0.15f;

    // 공격
    private float _attackCooldown;
    private float _pendingHitTimer = -1f;
    private int   _spawnIndex;

    // 버프 (파티 버프가 소환수에게도 걸림)
    private class Buff { public StatusEffectType type; public float value; public ModifierMode mode; public float remaining; }
    private readonly List<Buff> _buffs = new List<Buff>();
    private float _buffAtkPct, _buffAtkFlat, _buffCritRate, _buffCritDmg, _buffDmgReduction, _buffMoveSpeed;
    private int   _invulnerableCount;

    private static readonly int NoHash = 0;
    private int _walkHash, _attackHash;

    // ─────────────────────────────────────────────────────────────────
    // 생성 / 제거
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        _agent  = GetComponent<NavMeshAgent>();
        _anim   = GetComponentInChildren<Animator>();
        _shield = new ShieldPool(this);
        _baseSpeed = _agent.speed;

        _walkHash   = FindParam(walkBool,      AnimatorControllerParameterType.Bool);
        _attackHash = FindParam(attackTrigger, AnimatorControllerParameterType.Trigger);
    }

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    // SummonSkill이 Instantiate 직후 호출
    public void Initialize(CharacterStat owner, SummonSkillData data, int level, int spawnIndex)
    {
        Owner        = owner;
        SourceSkill  = data;
        _spawnIndex  = spawnIndex;
        _ownerAttack = owner != null ? owner.GetComponent<AttackBase>() : null;

        _useAp    = data.useAp;
        _atkRatio = data.GetAtkRatio(level);
        _defRatio = data.GetDefRatio(level);

        MaxHp         = Mathf.Max(1f, (owner != null ? owner.MaxHp : 0f) * data.GetHpRatio(level));
        Hp            = MaxHp;
        Duration      = Mathf.Max(0.1f, data.GetDuration(level));
        RemainingTime = Duration;

        // 캐릭터는 소환수를 무시하고 지나가고(우선순위가 낮은 에이전트는 높은 쪽이 피하지 않음),
        // 소환수는 같은 우선순위인 소환수끼리와 적을 비켜 간다
        _agent.avoidancePriority = 99;
        _agent.stoppingDistance  = 0f;

        SpawnFx(spawnEffectPoolKey, transform.position);
        OnHpChanged?.Invoke(Hp, MaxHp);
    }

    // 시간 종료·주인 사망·재소환 교체
    public void Despawn()
    {
        if (_dead) return;
        _dead = true;
        SpawnFx(despawnEffectPoolKey, transform.position);
        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────
    // 매 프레임
    // ─────────────────────────────────────────────────────────────────

    void Update()
    {
        if (_dead) return;
        if (Owner == null || Owner.Hp <= 0f) { Despawn(); return; }

        float dt = Time.deltaTime;
        RemainingTime -= dt;
        if (RemainingTime <= 0f) { Despawn(); return; }

        UpdateBuffs(dt);
        if (_attackCooldown > 0f) _attackCooldown -= dt;

        // 타격 대기 중(공격 모션) — 끝날 때까지 이동·대상 변경 없음
        if (_pendingHitTimer >= 0f)
        {
            _pendingHitTimer -= dt;
            FaceTarget();
            if (_pendingHitTimer < 0f) DealHit();
            UpdateWalkAnim();
            return;
        }

        // 주인에게서 너무 멀어지면 곁으로 순간이동 (포탈·대시 등)
        if ((transform.position - Owner.transform.position).sqrMagnitude > leashDistance * leashDistance)
        {
            WarpNear(Owner.transform);
            ClearTarget();
        }

        _retargetTimer -= dt;
        if (_retargetTimer <= 0f)
        {
            _retargetTimer = 0.25f;
            PickTarget();
        }

        if (_target != null) CombatMove(dt);
        else                 FollowOwner();

        UpdateWalkAnim();
    }

    // ─────────────────────────────────────────────────────────────────
    // 대상 선택
    // ─────────────────────────────────────────────────────────────────

    private void PickTarget()
    {
        // 1순위: 주인이 노리는 적 (요릭의 구울처럼 주인을 따라 친다)
        Transform wanted = ValidEnemy(_ownerAttack != null ? _ownerAttack.currentTarget : null);

        // 2순위: 리더가 노리는 적
        if (wanted == null && PartyManager.instance != null && PartyManager.instance.currentLeader != null)
        {
            var leaderAttack = PartyManager.instance.currentLeader.AttackComp;
            wanted = ValidEnemy(leaderAttack != null ? leaderAttack.currentTarget : null);
        }

        // 지금 싸우던 대상이 아직 유효하면 유지 (주인이 대상을 지정하지 않았을 때)
        if (wanted == null && ValidEnemy(_target) != null) return;

        // 3순위: 주인 근처에서 가장 가까운 적
        if (wanted == null) wanted = NearestEnemy(Owner.transform.position, searchRadius, null);

        if (wanted != _target) SetTarget(wanted);
    }

    private Transform ValidEnemy(Transform t)
    {
        if (t == null) return null;
        if (t == _ignoreTarget && Time.time < _ignoreUntil) return null;
        if (!t.TryGetComponent(out EnemyHp hp) || hp.isDead || !t.gameObject.activeInHierarchy) return null;
        // 주인에게서 너무 먼 적은 쫓지 않음 (leash로 끌려오는 왕복 방지)
        if ((t.position - Owner.transform.position).sqrMagnitude > leashDistance * leashDistance * 0.64f) return null;
        return t;
    }

    private Transform NearestEnemy(Vector3 center, float radius, Transform exclude)
    {
        Transform best = null;
        float bestSqr  = radius * radius;
        foreach (var e in EnemyHp.AllInstances)
        {
            if (e == null || e.isDead || e.transform == exclude) continue;
            if (e.transform == _ignoreTarget && Time.time < _ignoreUntil) continue;
            float sqr = (e.transform.position - center).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = e.transform; }
        }
        return best;
    }

    private void SetTarget(Transform t)
    {
        _target           = t;
        _targetHp         = t != null ? t.GetComponent<EnemyHp>() : null;
        _targetRadius     = t != null ? BodyRadius(t) : 0f;
        _lastDestination  = Vector3.positiveInfinity;
        _stuckTimer       = 0f;
        _stuckCheckDist   = float.MaxValue;
    }

    private void ClearTarget() => SetTarget(null);

    // 대상 몸 크기 — 큰 몬스터에게는 그만큼 떨어져서 때리도록
    private static float BodyRadius(Transform t)
    {
        var col = t.GetComponentInChildren<Collider>();
        if (col == null) return 0.5f;
        Vector3 ext = col.bounds.extents;
        return Mathf.Max(ext.x, ext.z);
    }

    // ─────────────────────────────────────────────────────────────────
    // 이동 / 공격
    // ─────────────────────────────────────────────────────────────────

    private void CombatMove(float dt)
    {
        if (_targetHp == null || _targetHp.isDead) { ClearTarget(); return; }

        float reach = attackRange + _targetRadius;
        float dist  = Vector3.Distance(transform.position, _target.position);

        if (dist <= reach)
        {
            StopMoving();
            FaceTarget();
            _stuckTimer = 0f;
            _stuckCheckDist = float.MaxValue;
            if (_attackCooldown <= 0f) StartAttack();
            return;
        }

        MoveTo(_target.position);

        // 막힘 감지 — 1초 동안 거의 못 다가가면 가장 가까운 다른 적으로 교체
        _stuckTimer += dt;
        if (_stuckTimer >= StuckCheckInterval)
        {
            if (_stuckCheckDist - dist < StuckMinProgress)
            {
                _ignoreTarget = _target;
                _ignoreUntil  = Time.time + 2f;
                Transform other = NearestEnemy(transform.position, searchRadius, _target);
                SetTarget(other);
                return;
            }
            _stuckTimer     = 0f;
            _stuckCheckDist = dist;
        }
    }

    // 싸울 대상이 없으면 주인 뒤쪽 좌우에 붙어 따라다닌다
    private void FollowOwner()
    {
        Transform owner = Owner.transform;
        float side      = (_spawnIndex % 2 == 0) ? -1f : 1f;
        Vector3 spot    = owner.position + owner.rotation * new Vector3(side * 1.2f, 0f, -1.2f);

        if ((transform.position - spot).sqrMagnitude > followDistance * followDistance) MoveTo(spot);
        else if (!_agent.pathPending && _agent.hasPath && _agent.remainingDistance < 0.3f) StopMoving();
    }

    private void MoveTo(Vector3 dest)
    {
        if (!_agent.enabled || !_agent.isOnNavMesh) return;
        _agent.isStopped = false;
        // 목적지가 조금만 바뀌면 경로를 다시 잡지 않음 (떨림 방지)
        if ((_lastDestination - dest).sqrMagnitude > 0.09f || !_agent.hasPath)
        {
            _agent.SetDestination(dest);
            _lastDestination = dest;
        }
    }

    private void StopMoving()
    {
        if (!_agent.enabled || !_agent.isOnNavMesh) return;
        if (_agent.hasPath) _agent.ResetPath();
        _agent.velocity   = Vector3.zero;
        _lastDestination  = Vector3.positiveInfinity;
    }

    private void FaceTarget()
    {
        if (_target == null) return;
        Vector3 dir = _target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
    }

    private void StartAttack()
    {
        _attackCooldown  = Mathf.Max(0.1f, attackInterval);
        _pendingHitTimer = Mathf.Max(0f, damageDelay);
        if (_anim != null && _attackHash != NoHash) _anim.SetTrigger(_attackHash);
        if (!string.IsNullOrEmpty(attackSfxKey)) AudioManager.instance?.PlaySFX(attackSfxKey);
    }

    private void DealHit()
    {
        _pendingHitTimer = -1f;
        if (_targetHp == null || _targetHp.isDead || Owner == null) return;

        bool  isMagic = _useAp;
        float damage  = Attack * (1f + (isMagic ? Owner.MagicDmgBonus : Owner.PhysDmgBonus));
        bool  isCrit  = UnityEngine.Random.value < CritRate;
        if (isCrit) damage *= CritDamage;
        if (damage <= 0f) return;

        // 공격자는 주인으로 넘긴다 — 데미지 보정 패시브·처치 판정이 루나리스 기준으로 동작 (장판 스킬과 같은 방식)
        GameObject attacker = Owner.gameObject;

        if (!string.IsNullOrEmpty(projectilePoolKey) && ObjectPoolManager.instance != null)
        {
            var go = ObjectPoolManager.instance.GetGo(projectilePoolKey);
            if (go != null)
            {
                Vector3 from = firePoint != null ? firePoint.position : transform.position + Vector3.up;
                Vector3 to   = _target.position + Vector3.up;
                Quaternion rot = Quaternion.LookRotation((to - from).normalized);
                go.transform.SetPositionAndRotation(from, rot);
                if (go.TryGetComponent(out ProjectileScript proj))
                    proj.SetProjectileData(damage, attacker, OnProjectileHit, isMagic, isCrit);
                return;
            }
        }

        if (isMagic) _targetHp.TakeMagicDamage(damage, attacker, isCrit);
        else         _targetHp.TakeDamage(damage, attacker, isCrit);
        SpawnFx(hitEffectPoolKey, _target.position);
        OnAttackLanded();
    }

    private void OnProjectileHit(EnemyHp enemy)
    {
        if (enemy != null) OnAttackLanded();
    }

    // 적중 시 주인에게 알림 (소환수 적중 마나 회복 패시브)
    private void OnAttackLanded()
    {
        if (Owner != null) Owner.NotifySummonHit();
    }

    private void UpdateWalkAnim()
    {
        if (_anim == null || _walkHash == NoHash) return;
        bool walking = _pendingHitTimer < 0f && _agent.enabled && _agent.velocity.sqrMagnitude > 0.01f;
        _anim.SetBool(_walkHash, walking);
    }

    private void WarpNear(Transform t)
    {
        Vector3 pos = t.position - t.forward * 1.5f;
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 3f, NavMesh.AllAreas)) pos = hit.position;
        if (_agent.enabled) _agent.Warp(pos);
        else transform.position = pos;
    }

    // ─────────────────────────────────────────────────────────────────
    // 능력치 (주인 능력치 × 비율, 실시간 반영)
    // ─────────────────────────────────────────────────────────────────

    public float Attack
    {
        get
        {
            if (Owner == null) return 0f;
            float baseAtk = (_useAp ? Owner.TotalAp : Owner.TotalAtk) * _atkRatio;
            return baseAtk * (1f + _buffAtkPct) + _buffAtkFlat;
        }
    }
    public float Def        => Owner != null ? Owner.TotalDef      * _defRatio : 0f;
    public float MagicRes   => Owner != null ? Owner.TotalMagicRes * _defRatio : 0f;
    public float CritRate   => Mathf.Min(1f, (Owner != null ? Owner.TotalCritRate : 0f) + _buffCritRate);
    public float CritDamage => (Owner != null ? Owner.TotalCritDamage : 1.5f) + _buffCritDmg;

    // ─────────────────────────────────────────────────────────────────
    // 피격 (IDamageable)
    // ─────────────────────────────────────────────────────────────────

    public void TakeDamage(float damage, GameObject attacker, bool isCrit = false)
        => ReceiveDamage(damage * (1f - Def / (Def + 100f)), physicalDamageColor, isCrit);

    public void TakeMagicDamage(float damage, GameObject attacker, bool isCrit = false)
        => ReceiveDamage(damage * (1f - MagicRes / (MagicRes + 100f)), magicDamageColor, isCrit);

    private void ReceiveDamage(float damage, Color color, bool isCrit)
    {
        if (_dead || _invulnerableCount > 0) return;

        damage *= 1f - Mathf.Clamp01(_buffDmgReduction);
        damage  = _shield.Absorb(damage);
        if (damage <= 0f) return;

        Hp = Mathf.Max(0f, Hp - damage);
        SpawnDamageText(damage, color, isCrit);
        OnHpChanged?.Invoke(Hp, MaxHp);

        if (Hp <= 0f) Despawn();
    }

    public void Heal(float amount)
    {
        if (_dead || amount <= 0f) return;
        Hp = Mathf.Min(MaxHp, Hp + amount);
        OnHpChanged?.Invoke(Hp, MaxHp);
    }

    public void ApplyShield(float amount, float duration) => _shield.Apply(amount, duration);

    // ─────────────────────────────────────────────────────────────────
    // 버프 — 파티 전체 버프(BuffSkill)가 소환수에게도 걸린다. 지원: 받는 피해 감소, 무적,
    // 공격력·마법 공격력 증가(소환수 공격력에 반영), 치명타 확률·데미지, 이동속도
    // ─────────────────────────────────────────────────────────────────

    public static bool SupportsBuff(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.DmgReductionUp:
            case StatusEffectType.Invulnerable:
            case StatusEffectType.AtkUp:
            case StatusEffectType.ApUp:
            case StatusEffectType.CritRateUp:
            case StatusEffectType.CritDamageUp:
            case StatusEffectType.MoveSpeedUp:
                return true;
            default:
                return false;
        }
    }

    public void ApplyBuff(StatusEffect effect)
    {
        if (_dead || effect == null || !SupportsBuff(effect.effectType) || effect.duration <= 0f) return;
        // 소환수 공격 기준(마법/물리)과 다른 공격력 버프는 무시
        if (effect.effectType == StatusEffectType.AtkUp && _useAp)  return;
        if (effect.effectType == StatusEffectType.ApUp  && !_useAp) return;

        _buffs.Add(new Buff { type = effect.effectType, value = effect.value, mode = effect.mode, remaining = effect.duration });
        RecalcBuffs();
    }

    private void UpdateBuffs(float dt)
    {
        if (_buffs.Count == 0) return;
        bool changed = false;
        for (int i = _buffs.Count - 1; i >= 0; i--)
        {
            _buffs[i].remaining -= dt;
            if (_buffs[i].remaining <= 0f) { _buffs.RemoveAt(i); changed = true; }
        }
        if (changed) RecalcBuffs();
    }

    private void RecalcBuffs()
    {
        _buffAtkPct = _buffAtkFlat = _buffCritRate = _buffCritDmg = _buffDmgReduction = _buffMoveSpeed = 0f;
        _invulnerableCount = 0;
        foreach (var b in _buffs)
        {
            switch (b.type)
            {
                case StatusEffectType.AtkUp:
                case StatusEffectType.ApUp:
                    if (b.mode == ModifierMode.Percent) _buffAtkPct += b.value;
                    else                                _buffAtkFlat += b.value;
                    break;
                case StatusEffectType.CritRateUp:     _buffCritRate     += b.value; break;
                case StatusEffectType.CritDamageUp:   _buffCritDmg      += b.value; break;
                case StatusEffectType.DmgReductionUp: _buffDmgReduction += b.value; break;
                case StatusEffectType.MoveSpeedUp:    _buffMoveSpeed    += Mathf.Max(0f, b.value); break;
                case StatusEffectType.Invulnerable:   _invulnerableCount++;         break;
            }
        }
        _agent.speed = _baseSpeed * (1f + _buffMoveSpeed);
    }

    // ─────────────────────────────────────────────────────────────────
    // 조회 헬퍼 (스킬·UI용)
    // ─────────────────────────────────────────────────────────────────

    // 주인의 살아 있는 소환수 수 (kind 지정 시 그 종류만, skill 지정 시 그 스킬로 부른 것만)
    public static int CountFor(CharacterStat owner, Kind? kind = null, SummonSkillData skill = null)
    {
        int n = 0;
        foreach (var s in All)
            if (s != null && s.IsAlive && s.Owner == owner
                && (kind == null || s.kind == kind.Value)
                && (skill == null || s.SourceSkill == skill)) n++;
        return n;
    }

    // 같은 스킬로 부른 소환수를 전부 없앤다 (재소환 시 교체)
    public static void DespawnFrom(CharacterStat owner, SummonSkillData skill)
    {
        for (int i = All.Count - 1; i >= 0; i--)
        {
            var s = All[i];
            if (s != null && s.Owner == owner && s.SourceSkill == skill) s.Despawn();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 연출
    // ─────────────────────────────────────────────────────────────────

    private void SpawnDamageText(float damage, Color color, bool isCrit)
    {
        if (string.IsNullOrEmpty(damageTextPoolKey) || ObjectPoolManager.instance == null || !ObjectPoolManager.instance.IsReady) return;
        var go = ObjectPoolManager.instance.GetGo(damageTextPoolKey);
        if (go == null) return;

        Vector3 pos = hudPos != null ? hudPos.position : transform.position + Vector3.up * 2f;
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(60f, 0f, 0f));
        go.GetComponent<DamageText>()?.Setup(damage, color, showMinus: true, isCrit: isCrit);
    }

    private static void SpawnFx(string key, Vector3 pos)
    {
        if (string.IsNullOrEmpty(key) || ObjectPoolManager.instance == null) return;
        var go = ObjectPoolManager.instance.GetGo(key);
        if (go != null) go.transform.position = pos;
    }

    private int FindParam(string name, AnimatorControllerParameterType type)
    {
        if (_anim == null || string.IsNullOrEmpty(name) || _anim.runtimeAnimatorController == null) return NoHash;
        int hash = Animator.StringToHash(name);
        foreach (var p in _anim.parameters)
            if (p.nameHash == hash && p.type == type) return hash;
        return NoHash;
    }
}

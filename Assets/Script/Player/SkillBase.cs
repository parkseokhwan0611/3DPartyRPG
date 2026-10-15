using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System;

public abstract class SkillBase : MonoBehaviour
{
    protected CharacterStat myStat;
    protected Animator anim;
    protected AttackBase attackBase;
    protected SkillManager skillManager;
    protected NavMeshAgent agent;
    protected PartyStatusEffectHandler statusHandler;

    public SkillData skillData;
    public int skillLevel = 1;

    private float cooldownTimer = 0f;
    private float cooldownTotal = 0f; // 이번 쿨타임의 전체 길이 (시전 시점 레벨 기준, 쿨타임 비율 표시용)
    private Coroutine skillCoroutine;
    private Coroutine executeCoroutine; // SkillRoutine 안에서 돌리는 ExecuteSkill — ForceStop 때 같이 멈춤

    // 스킬 종료 시 이동/타겟팅 재개를 알리는 이벤트
    public event Action OnSkillFinished;

    public bool IsReady           => cooldownTimer <= 0f;
    public float CooldownRemaining => Mathf.Max(cooldownTimer, 0f);
    public float CooldownRatio
    {
        get
        {
            // 외부에서 남은 시간만 지정한 경우(SetCooldown)는 전체 길이를 모르므로 지금 레벨 기준 쿨타임으로 나눈다
            float total = cooldownTotal > 0f ? cooldownTotal : (skillData != null ? CooldownAt(LiveLevel) : 0f);
            return total > 0f ? Mathf.Clamp01(cooldownTimer / total) : 0f;
        }
    }

    // 캐릭터의 실시간 스킬 레벨 — 슬롯은 씬 시작 시점 레벨로 만들어지므로, 그 뒤에 스킬을 올렸으면 skillLevel과 다를 수 있다
    private int LiveLevel => myStat != null && skillData != null ? Mathf.Max(1, myStat.GetSkillLevel(skillData)) : skillLevel;

    // skillLevel을 실시간 레벨로 맞춘다. 스킬을 실제로 쓰는 순간(TryUseSkill)마다 호출되므로
    // 리더 입력·팔로워 자동 시전·힐 자동 시전·사거리 대기 후 발동 모두 최신 레벨로 나간다
    public void SyncLevel() => skillLevel = LiveLevel;

    // maxLevel과 배열(cooldown/mpCost) 길이가 어긋나 있어도 크래시 없이 마지막 유효값으로 대체
    private static float GetArrayValue(float[] arr, int level)
    {
        if (arr == null || arr.Length == 0) return 0f;
        int idx = Mathf.Clamp(level - 1, 0, arr.Length - 1);
        return arr[idx];
    }

    public void SetCooldown(float remaining)
    {
        cooldownTimer = remaining;
        cooldownTotal = 0f;
    }

    // 장비의 "스킬 쿨타임 감소"·"마나 소모 감소" 옵션 상한 — 여러 장비를 겹쳐도 쿨타임·마나가 0에 가까워지지 않게
    public const float MaxCooldownReduce = 0.5f;
    public const float MaxMpCostReduce   = 0.5f;

    // 장비 감소를 반영한 실제 마나·쿨타임 — 스킬 사용과 설명창(SkillDetailPanelUI·CombatDetailPopupUI)이 같은 값을 쓰도록 공개
    public static float ReducedMpCost(float baseCost, CharacterStat caster)
        => baseCost * (1f - Mathf.Clamp(caster != null ? caster.TotalMpReduce : 0f, 0f, MaxMpCostReduce));
    public static float ReducedCooldown(float baseCooldown, CharacterStat caster)
        => baseCooldown * (1f - Mathf.Clamp(caster != null ? caster.TotalCDReduce : 0f, 0f, MaxCooldownReduce));

    // 실제로 쓰는 마나·쿨타임 (레벨별 값 × 장비 감소). 예전엔 장비 감소 옵션이 표시만 되고 적용되지 않았음
    private float MpCostAt(int level)   => ReducedMpCost(GetArrayValue(skillData.mpCost, level), myStat);
    private float CooldownAt(int level) => ReducedCooldown(GetArrayValue(skillData.cooldown, level), myStat);

    // 지금 레벨 기준 마나 소모량 — skillLevel은 스킬을 실제로 쓸 때만 동기화되므로(SyncLevel)
    // UI 표시용으로는 캐릭터의 실시간 스킬 레벨을 사용. 패시브는 0
    public float CurrentMpCost
    {
        get
        {
            if (skillData == null || skillData.skillType == SkillData.SkillType.Passive) return 0f;
            return MpCostAt(LiveLevel);
        }
    }

    // 마나가 충분한지 (UI 표시용 — 실제 사용 판정은 TryUseSkill의 TryUseMp)
    public bool HasEnoughMp => myStat == null || myStat.Mp >= CurrentMpCost;

    // 지금 시전 조건을 만족하는지 (예: 소환수 도발은 소환수가 있어야 함). false면 마나·쿨타임을 쓰지 않고 거절
    public virtual bool CanUseNow => true;

    // CanUseNow가 false일 때 스킬 상태 텍스트에 띄울 이유 (null이면 표시 안 함)
    public virtual string CannotUseMessage => null;

    // 팔로워 자동 사용 시 지금 쓸 가치가 있는지 (예: 소환수가 멀쩡하면 재소환하지 않음)
    public virtual bool IsWorthAutoUsing => true;

    protected virtual void Awake()
    {
        myStat       = GetComponent<CharacterStat>();
        anim         = GetComponent<Animator>();
        attackBase   = GetComponent<AttackBase>();
        skillManager = GetComponent<SkillManager>();
        agent        = GetComponent<NavMeshAgent>();
        statusHandler = GetComponent<PartyStatusEffectHandler>();
    }

    protected virtual void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 사용 시도
    // ─────────────────────────────────────────────────────────────────

    public bool TryUseSkill(Transform target = null)
    {
        if (skillData == null) return false;
        SyncLevel(); // 지속시간·수치·마나·쿨타임 모두 지금 레벨 기준으로
        if (!IsReady) return false;
        if (!CanUseNow) return false;

        // 발동 중인 스킬이 있으면 캔슬 불가
        if (skillManager != null && skillManager.IsActivatingSkill) return false;

        // 데미지 스킬 사거리 체크
        if (skillData.skillType == SkillData.SkillType.Damage)
        {
            if (target == null) return false;

            DamageSkillData dmgData = skillData as DamageSkillData;
            if (dmgData != null)
            {
                float dist = Vector3.Distance(transform.position, target.position);
                if (dist > dmgData.castRange)
                    return false;
            }
        }

        // 패시브는 MP 소모 없음
        if (skillData.skillType != SkillData.SkillType.Passive)
        {
            float cost = MpCostAt(skillLevel);
            if (!myStat.TryUseMp(cost)) return false;
        }

        skillCoroutine = StartCoroutine(SkillRoutine(target));
        cooldownTimer  = CooldownAt(skillLevel);
        cooldownTotal  = cooldownTimer;
        return true;
    }

    // ─────────────────────────────────────────────────────────────────
    // 외부에서 스킬 코루틴 강제 종료 (다음 스킬 후딜 캔슬용)
    // ─────────────────────────────────────────────────────────────────

    public void ForceStop()
    {
        if (skillCoroutine != null)
        {
            StopCoroutine(skillCoroutine);
            skillCoroutine = null;
        }
        // 바깥 SkillRoutine만 멈추면 안쪽 ExecuteSkill은 계속 돌아서 끊긴 스킬의 이펙트·데미지가 뒤늦게 나감
        if (executeCoroutine != null)
        {
            StopCoroutine(executeCoroutine);
            executeCoroutine = null;
        }
        // 아직 안 나간 지연·반복 효과음도 취소 — 끊긴 스킬의 소리가 뒤늦게 나지 않게
        StopSkillSfx();

        // IsCastingSkill 즉시 해제
        if (attackBase != null) attackBase.IsCastingSkill = false;

        // SkillRoutine 끝의 이동 재개가 건너뛰어지므로 여기서 대신 푼다 — 안 그러면 다음 스킬이 발동에
        // 실패했을 때(쿨타임·사거리 밖 등) 이동 잠금이 남아 제자리에 멈춘다. 스턴 중이면 EndStun이 푼다
        bool isStunned = statusHandler != null && statusHandler.HasDebuff(StatusEffectType.Stun);
        if (!isStunned && agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = false;
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 루틴
    // ─────────────────────────────────────────────────────────────────
    private IEnumerator SkillRoutine(Transform target)
    {
        // 이전 스킬(후딜 중)을 먼저 끊는다 — 아래에서 시전 플래그·이동 잠금을 건 뒤에 끊으면
        // 이전 스킬의 ForceStop이 방금 건 플래그들을 도로 풀어버림
        skillManager?.RegisterCurrentSkill(this);

        if (attackBase != null)
        {
            attackBase.CancelCurrentAttack();
            attackBase.IsCastingSkill = true;
            attackBase.ResetFirstAttackDelay(0.3f);
        }

        // 스킬 시전 중 이동 중단 + 걷기 애니메이션 즉시 해제 — ResetPath/velocity만으로는 그 순간의
        // 경로만 지워질 뿐이라, 시전 중에 새로 클릭해서 이동 명령이 들어오는 것 자체는 못 막는다.
        // isStopped까지 걸어야 몬스터 스킬 시전과 동일하게 시전 내내 확실히 묶어둘 수 있음
        // (실제 이동 명령 차단은 PartyManager.HandleCommandInput의 IsCastingSkill 체크가 담당)
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
        if (anim != null)
            anim.SetBool("isWalking", false);

        if (skillManager != null) skillManager.IsActivatingSkill = true;

        executeCoroutine = StartCoroutine(ExecuteSkill(target));
        yield return executeCoroutine;
        executeCoroutine = null;

        if (skillManager != null && skillManager.IsActivatingSkill)
            ReleaseActivating();

        bool isStunned = statusHandler != null && statusHandler.HasDebuff(StatusEffectType.Stun);
        if (!isStunned && agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = false;

        if (attackBase != null) attackBase.IsCastingSkill = false;
        skillManager?.UnregisterCurrentSkill();
        skillCoroutine = null;
        OnSkillFinished?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────
    // 발동 플래그 제어
    // ─────────────────────────────────────────────────────────────────

    // 후딜 캔슬 허용 — 이펙트/데미지 판정 완료 후 각 스킬에서 호출
    protected void ReleaseActivating()
    {
        if (skillManager != null) skillManager.IsActivatingSkill = false;
        // IsCastingSkill은 SkillRoutine 끝에서 해제 (후딜 중 기본 공격 방지)
    }

    protected abstract IEnumerator ExecuteSkill(Transform target);

    // ─────────────────────────────────────────────────────────────────
    // 스킬 SFX 재생 (애니메이션 시작 시점 기준, 항목별 delay 뒤에 재생)
    // ─────────────────────────────────────────────────────────────────

    // 예약된 지연·반복 효과음 — ForceStop(스턴·다음 스킬로 후딜 캔슬 등) 때 같이 멈춤
    private readonly System.Collections.Generic.List<Coroutine> _sfxRoutines = new System.Collections.Generic.List<Coroutine>();

    protected void PlaySkillSfx(SkillData data)
    {
        // 이전 시전에서 이미 끝난 코루틴 참조는 버린다 (정상 종료된 스킬의 남은 소리는 끊지 않음)
        _sfxRoutines.Clear();
        if (data.sfxEntries == null) return;

        foreach (var entry in data.sfxEntries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.sfxKey)) continue;

            if (entry.delay <= 0f && entry.PlayCount == 1)
                AudioManager.instance?.PlaySFX(entry.sfxKey);
            else
                _sfxRoutines.Add(StartCoroutine(PlayDelayedSfx(entry.sfxKey, entry.delay, entry.PlayCount, entry.repeatInterval)));
        }
    }

    private void StopSkillSfx()
    {
        foreach (var r in _sfxRoutines)
            if (r != null) StopCoroutine(r);
        _sfxRoutines.Clear();
    }

    // delay 뒤 첫 재생, 이후 interval마다 count번까지 반복
    private IEnumerator PlayDelayedSfx(string key, float delay, int count, float interval)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        for (int i = 0; i < count; i++)
        {
            if (i > 0 && interval > 0f) yield return new WaitForSeconds(interval);
            AudioManager.instance?.PlaySFX(key);
        }
    }

    // DamageSkill/HealSkill이 공통으로 사용하는 스탯 스케일링 조회
    protected float GetScalingStatValue(DamageSkillData.ScalingStat stat)
    {
        if (myStat == null) return 0f;
        return stat switch
        {
            DamageSkillData.ScalingStat.Str => myStat.TotalStr,
            DamageSkillData.ScalingStat.Vit => myStat.TotalVit,
            DamageSkillData.ScalingStat.Int => myStat.TotalInt,
            DamageSkillData.ScalingStat.Fth => myStat.TotalFth,
            DamageSkillData.ScalingStat.Dex => myStat.TotalDex,
            _                               => 0f,
        };
    }
}
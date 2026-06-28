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

    public SkillData skillData;
    public int skillLevel = 1;

    private float cooldownTimer = 0f;
    private Coroutine skillCoroutine;

    // 스킬 종료 시 이동/타겟팅 재개를 알리는 이벤트
    public event Action OnSkillFinished;

    public bool IsReady           => cooldownTimer <= 0f;
    public float CooldownRemaining => Mathf.Max(cooldownTimer, 0f);
    public float CooldownRatio    => skillData != null && skillData.cooldown.Length > 0
        ? Mathf.Clamp01(cooldownTimer / skillData.cooldown[skillLevel - 1])
        : 0f;

    public void SetCooldown(float remaining) => cooldownTimer = remaining;

    protected virtual void Awake()
    {
        myStat       = GetComponent<CharacterStat>();
        anim         = GetComponent<Animator>();
        attackBase   = GetComponent<AttackBase>();
        skillManager = GetComponent<SkillManager>();
        agent        = GetComponent<NavMeshAgent>();
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
        if (!IsReady) return false;

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
            float cost = skillData.mpCost[skillLevel - 1];
            if (!myStat.TryUseMp(cost)) return false;
        }

        skillCoroutine = StartCoroutine(SkillRoutine(target));
        cooldownTimer  = skillData.cooldown[skillLevel - 1];
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

        // IsCastingSkill 즉시 해제
        if (attackBase != null) attackBase.IsCastingSkill = false;
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 루틴
    // ─────────────────────────────────────────────────────────────────
    private IEnumerator SkillRoutine(Transform target)
    {
        if (attackBase != null)
        {
            attackBase.CancelCurrentAttack();
            attackBase.IsCastingSkill = true;
            attackBase.ResetFirstAttackDelay(0.3f);
        }

        // 스킬 시전 중 이동 중단 + 걷기 애니메이션 즉시 해제
        if (agent != null)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
        if (anim != null)
            anim.SetBool("isWalking", false);

        if (skillManager != null) skillManager.IsActivatingSkill = true;
        skillManager?.RegisterCurrentSkill(this);

        yield return StartCoroutine(ExecuteSkill(target));

        if (skillManager != null && skillManager.IsActivatingSkill)
            ReleaseActivating();

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
}
using UnityEngine;
using System.Collections;

public abstract class SkillBase : MonoBehaviour
{
    private AttackBase attackBase;
    protected CharacterStat myStat;
    protected Animator anim;

    public SkillData skillData;
    public int skillLevel = 1;

    private float cooldownTimer = 0f;

    // 쿨다운 진행률 (UI용 0~1)
    public bool IsReady      => cooldownTimer <= 0f;
    public float CooldownRatio => skillData != null
        ? Mathf.Clamp01(cooldownTimer / skillData.cooldown[skillLevel - 1])
        : 0f;

    protected virtual void Awake()
    {
        myStat = GetComponent<CharacterStat>();
        anim   = GetComponent<Animator>();

        attackBase = GetComponent<AttackBase>();
    }

    protected virtual void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    // 외부(SkillManager)에서 호출
    public bool TryUseSkill(Transform target = null)
    {
        if (skillData == null) return false;
        if (!IsReady) return false;

        // 데미지 스킬이면 타겟 범위 체크
        if (skillData.skillType == SkillData.SkillType.Damage)
        {
            if (target == null) return false;

            DamageSkillData dmgData = skillData as DamageSkillData;
            if (dmgData != null)
            {
                float dist  = Vector3.Distance(transform.position, target.position);
                float range = dmgData.GetRange(skillLevel);

                if (dist > range)
                {
                    Debug.Log("[SkillBase] 타겟이 스킬 범위 밖입니다.");
                    return false;
                }
            }
        }

        // 패시브는 MP 소모 없음
        if (skillData.skillType != SkillData.SkillType.Passive)
        {
            float cost = skillData.mpCost[skillLevel - 1];
            if (!myStat.TryUseMp(cost)) return false;
        }

        StartCoroutine(SkillRoutine(target));
        cooldownTimer = skillData.cooldown[skillLevel - 1];
        return true;
    }

    // ExecuteSkill을 래핑해서 시작/종료 플래그 관리
    private IEnumerator SkillRoutine(Transform target)
    {
        // 스킬 시전 시작 — 기본 공격 중지
        if (attackBase != null) attackBase.IsCastingSkill = true;

        yield return StartCoroutine(ExecuteSkill(target));

        // 스킬 시전 종료 — 기본 공격 재개
        if (attackBase != null) attackBase.IsCastingSkill = false;
    }

    protected abstract IEnumerator ExecuteSkill(Transform target);
}
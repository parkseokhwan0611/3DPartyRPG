using UnityEngine;
using System.Collections;

public abstract class SkillBase : MonoBehaviour
{
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

        float cost = skillData.mpCost[skillLevel - 1];
        if (!myStat.TryUseMp(cost)) return false;

        StartCoroutine(ExecuteSkill(target));
        cooldownTimer = skillData.cooldown[skillLevel - 1];
        return true;
    }

    protected abstract IEnumerator ExecuteSkill(Transform target);
}
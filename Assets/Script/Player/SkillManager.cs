using UnityEngine;
using System.Collections.Generic;

public class SkillManager : MonoBehaviour
{
    private PartyMemberScript memberScript;
    private AttackBase attackBase;
    private CharacterStat myStat;

    [Header("스킬 슬롯 (Q/W/E/R)")]
    public SkillData slotQ;
    public SkillData slotW;
    public SkillData slotE;
    public SkillData slotR;

    private SkillBase[] slots = new SkillBase[4];

    [Header("자동 스킬 설정 (팔로워 전용)")]
    public int attackPerSkill = 2;
    private int attackCount   = 0;

    // 스킬 발동 중 플래그 (캔슬 불가 구간)
    public bool IsActivatingSkill { get; set; } = false;

    // 현재 실행 중인 스킬 (후딜 포함 전체)
    private SkillBase currentSkill;

    // 사거리 밖이라 즉시 발동 못 한 데미지 스킬 — 접근 중 대기, 도착 시 자동 발동
    private SkillBase _pendingSkill;
    private Transform _pendingTarget;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        memberScript = GetComponent<PartyMemberScript>();
        attackBase   = GetComponent<AttackBase>();
        myStat       = GetComponent<CharacterStat>();

        slots[0] = CreateSkill(slotQ);
        slots[1] = CreateSkill(slotW);
        slots[2] = CreateSkill(slotE);
        slots[3] = CreateSkill(slotR);

        if (attackBase != null)
            attackBase.OnAttackExecuted += HandleAttackCount;
    }

    void Update()
    {
        if (memberScript == null) return;
        if (memberScript.CurrentState == PartyMemberScript.MemberState.Dead) return;

        TryExecutePendingSkill();

        if (!memberScript.isLeader)
            TryAutoUseHealSkill();
    }

    void OnDestroy()
    {
        if (attackBase != null)
            attackBase.OnAttackExecuted -= HandleAttackCount;

        foreach (var slot in slots)
        {
            if (slot != null)
                slot.OnSkillFinished -= OnAnySkillFinished;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 현재 스킬 등록/해제
    // ─────────────────────────────────────────────────────────────────

    public void RegisterCurrentSkill(SkillBase skill)
    {
        // 이전 스킬이 후딜 중이면 강제 종료
        if (currentSkill != null && currentSkill != skill)
            currentSkill.ForceStop();

        currentSkill = skill;
    }

    public void UnregisterCurrentSkill()
    {
        currentSkill = null;
    }

    // 후딜 캔슬 시 현재 스킬의 후딜 코루틴 강제 종료
    public void ForceStopCurrentSkill()
    {
        if (currentSkill != null)
        {
            currentSkill.ForceStop();
            currentSkill = null;
        }
        IsActivatingSkill = false;
        _pendingSkill  = null;
        _pendingTarget = null;
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 동적 생성
    // ─────────────────────────────────────────────────────────────────

    private SkillBase CreateSkill(SkillData data)
    {
        if (data == null) return null;

        SkillBase skill = null;

        switch (data.skillType)
        {
            case SkillData.SkillType.Damage:
                skill = gameObject.AddComponent<DamageSkill>();
                break;
            case SkillData.SkillType.Buff:
                skill = gameObject.AddComponent<BuffSkill>();
                break;
            case SkillData.SkillType.Heal:
                skill = gameObject.AddComponent<HealSkill>();
                break;
            case SkillData.SkillType.Debuff:
                skill = gameObject.AddComponent<DebuffSkill>();
                break;
            case SkillData.SkillType.Passive:
                Debug.LogWarning("[SkillManager] 패시브 스킬은 슬롯에 등록하지 않습니다.");
                break;
        }

        if (skill != null)
        {
            skill.skillData     = data;
            skill.skillLevel    = Mathf.Max(1, myStat != null ? myStat.GetSkillLevel(data) : 0);
            skill.OnSkillFinished += OnAnySkillFinished;
        }

        return skill;
    }

    // ─────────────────────────────────────────────────────────────────
    // 리더 입력
    // ─────────────────────────────────────────────────────────────────

    public void HandleKeyInput()
    {
        if (!memberScript.isLeader) return;

        if (Input.GetKeyDown(KeyCode.Q)) UseSkillByIndex(0);
        if (Input.GetKeyDown(KeyCode.W)) UseSkillByIndex(1);
        if (Input.GetKeyDown(KeyCode.E)) UseSkillByIndex(2);
        if (Input.GetKeyDown(KeyCode.R)) UseSkillByIndex(3);
    }

    public void UseSkillByIndex(int index)
    {
        if (!memberScript.isLeader) return;

        SkillBase skill = GetSlot(index);
        if (skill == null || skill.skillData == null) return;

        // 슬롯에 등록된 이후 레벨업했을 수 있으므로 실제 사용 직전에 최신 레벨로 재동기화
        SyncSkillLevel(skill);

        // 후딜 캔슬 — 현재 스킬 후딜 강제 종료 후 다음 스킬 실행
        if (currentSkill != null && !IsActivatingSkill)
            ForceStopCurrentSkill();

        Transform target = ResolveSkillTarget(skill);

        // 데미지 스킬이 사거리 밖이면 즉시 실패시키지 않고, 접근해서 도착하면 자동 발동되도록 대기
        if (skill.skillData.skillType == SkillData.SkillType.Damage
            && target != null && skill.IsReady && !IsActivatingSkill
            && skill.skillData is DamageSkillData dmgData
            && Vector3.Distance(transform.position, target.position) > dmgData.castRange)
        {
            _pendingSkill  = skill;
            _pendingTarget = target;
            return;
        }

        skill.TryUseSkill(target);
    }

    // 대기 중인 스킬이 사거리 안에 들어오면 자동 발동, 타겟을 잃으면 대기 취소
    private void TryExecutePendingSkill()
    {
        if (_pendingSkill == null) return;

        if (_pendingTarget == null || attackBase.currentTarget != _pendingTarget || IsActivatingSkill)
        {
            _pendingSkill  = null;
            _pendingTarget = null;
            return;
        }

        var dmgData = _pendingSkill.skillData as DamageSkillData;
        if (dmgData == null || Vector3.Distance(transform.position, _pendingTarget.position) > dmgData.castRange)
            return; // 아직 사거리 밖 — 계속 대기

        SkillBase skill  = _pendingSkill;
        Transform target = _pendingTarget;
        _pendingSkill  = null;
        _pendingTarget = null;
        SyncSkillLevel(skill);
        skill.TryUseSkill(target);
    }

    // 슬롯에 등록된 이후 레벨업이 발생했을 수 있으므로 사용 직전 최신 스킬 레벨로 동기화
    private void SyncSkillLevel(SkillBase skill)
    {
        if (skill == null || myStat == null) return;
        skill.skillLevel = Mathf.Max(1, myStat.GetSkillLevel(skill.skillData));
    }

    private Transform ResolveSkillTarget(SkillBase skill)
    {
        if (skill.skillData.skillType == SkillData.SkillType.Heal)
        {
            HealSkillData healData = skill.skillData as HealSkillData;
            if (healData != null && healData.targetType == HealSkillData.HealTargetType.Single)
                return GetLowestHpMember();
            return null;
        }

        return attackBase.currentTarget;
    }

    private Transform GetLowestHpMember()
    {
        if (PartyManager.instance == null) return null;

        Transform lowestTarget = null;
        float lowestRatio      = float.MaxValue;

        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member == null) continue;
            if (member.CurrentState == PartyMemberScript.MemberState.Dead) continue;
            var stat = member.GetComponent<CharacterStat>();
            if (stat == null || stat.MaxHp <= 0f) continue;

            float hpRatio = stat.Hp / stat.MaxHp;
            if (hpRatio < lowestRatio)
            {
                lowestRatio  = hpRatio;
                lowestTarget = member.transform;
            }
        }

        return lowestTarget;
    }

    // ─────────────────────────────────────────────────────────────────
    // 팔로워 자동 스킬
    // ─────────────────────────────────────────────────────────────────

    private void HandleAttackCount()
    {
        if (memberScript.isLeader) return;

        attackCount++;
        if (attackCount >= attackPerSkill)
        {
            attackCount = 0;
            TryAutoUseSkill();
        }
    }

    private void TryAutoUseSkill()
    {
        if (IsActivatingSkill) return;
        if (PartyManager.instance != null && !PartyManager.instance.AutoSkillEnabled) return;

        Transform target = attackBase.currentTarget;

        // 준비된 비힐·비패시브 스킬 수집
        List<SkillBase> readySlots = new List<SkillBase>();
        foreach (var slot in slots)
        {
            if (slot == null || !slot.IsReady) continue;
            if (slot.skillData.skillType == SkillData.SkillType.Heal)    continue;
            if (slot.skillData.skillType == SkillData.SkillType.Passive) continue;

            // DispelDebuff 효과가 있는 스킬은 파티원에 디버프가 있을 때만 후보에 포함
            if (IsDispelSkill(slot.skillData) && !AnyMemberHasDebuff()) continue;

            readySlots.Add(slot);
        }

        if (readySlots.Count == 0) return;

        // 우선순위 오름차순 정렬 후 최고 우선순위 그룹에서 랜덤 선택
        readySlots.Sort((a, b) => a.skillData.skillPriority.CompareTo(b.skillData.skillPriority));
        int topPriority = readySlots[0].skillData.skillPriority;

        List<SkillBase> topGroup = new List<SkillBase>();
        foreach (var slot in readySlots)
        {
            if (slot.skillData.skillPriority != topPriority) break;
            topGroup.Add(slot);
        }

        SkillBase chosen = topGroup[Random.Range(0, topGroup.Count)];

        if (chosen.skillData.skillType == SkillData.SkillType.Buff)
            chosen.TryUseSkill(null);
        else
            chosen.TryUseSkill(target);
    }

    // ─────────────────────────────────────────────────────────────────
    // 힐 스킬 자동 발동 — HP 조건 기반 (Update에서 호출)
    // ─────────────────────────────────────────────────────────────────

    private void TryAutoUseHealSkill()
    {
        if (IsActivatingSkill) return;
        if (PartyManager.instance == null || !PartyManager.instance.AutoSkillEnabled) return;

        foreach (var slot in slots)
        {
            if (slot == null || !slot.IsReady) continue;
            if (slot.skillData.skillType != SkillData.SkillType.Heal) continue;

            HealSkillData healData = slot.skillData as HealSkillData;
            if (healData == null) continue;

            if (healData.targetType == HealSkillData.HealTargetType.Party)
            {
                // 파티원 중 누구라도 HP 50% 미만이면 파티 힐
                if (AnyMemberBelowHpRatio(0.5f))
                    slot.TryUseSkill(null);
            }
            else
            {
                // HP 비율이 가장 낮고 50% 미만인 파티원에게 단일 힐
                Transform lowestTarget = GetLowestHpMemberBelow(0.5f);
                if (lowestTarget != null)
                    slot.TryUseSkill(lowestTarget);
            }
        }
    }

    private bool AnyMemberBelowHpRatio(float ratio)
    {
        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member == null) continue;
            if (member.CurrentState == PartyMemberScript.MemberState.Dead) continue;
            var stat = member.GetComponent<CharacterStat>();
            if (stat == null || stat.MaxHp <= 0f) continue;
            if (stat.Hp / stat.MaxHp < ratio) return true;
        }
        return false;
    }

    private bool IsDispelSkill(SkillData data)
    {
        var buffData = data as BuffSkillData;
        if (buffData == null) return false;
        foreach (var effect in buffData.buffEffects)
        {
            if (effect.effectType == BuffSkillData.BuffEffectType.DispelDebuff) return true;
        }
        return false;
    }

    private bool AnyMemberHasDebuff()
    {
        if (PartyManager.instance == null) return false;
        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member == null) continue;
            if (member.CurrentState == PartyMemberScript.MemberState.Dead) continue;
            var handler = member.GetComponent<PartyStatusEffectHandler>();
            if (handler != null && handler.HasActiveDebuff()) return true;
        }
        return false;
    }

    private Transform GetLowestHpMemberBelow(float ratio)
    {
        Transform lowestTarget = null;
        float lowestRatio      = ratio; // 이 값 미만인 대상만 선택

        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member == null) continue;
            if (member.CurrentState == PartyMemberScript.MemberState.Dead) continue;
            var stat = member.GetComponent<CharacterStat>();
            if (stat == null || stat.MaxHp <= 0f) continue;

            float hpRatio = stat.Hp / stat.MaxHp;
            if (hpRatio < lowestRatio)
            {
                lowestRatio  = hpRatio;
                lowestTarget = member.transform;
            }
        }

        return lowestTarget;
    }

    // ─────────────────────────────────────────────────────────────────
    // 슬롯 관리
    // ─────────────────────────────────────────────────────────────────

    public SkillBase GetSlot(int index)
    {
        if (index < 0 || index >= slots.Length) return null;
        return slots[index];
    }

    public void SetSlot(int index, SkillData newData)
    {
        if (index < 0 || index >= slots.Length) return;

        if (slots[index] != null)
            Destroy(slots[index]);

        slots[index] = CreateSkill(newData);

        switch (index)
        {
            case 0: slotQ = newData; break;
            case 1: slotW = newData; break;
            case 2: slotE = newData; break;
            case 3: slotR = newData; break;
        }
    }

    private void OnAnySkillFinished()
    {
        // 버프/힐 스킬 종료 후 이동/타겟팅 즉시 재개
        if (memberScript != null)
            memberScript.ResumeAfterSkill();
    }

    public float GetCooldownRatio(int index)
    {
        SkillBase skill = GetSlot(index);
        return skill != null ? skill.CooldownRatio : 0f;
    }

    public float GetCooldownRemaining(int index)
    {
        SkillBase skill = GetSlot(index);
        return skill != null ? skill.CooldownRemaining : 0f;
    }

    public void ResetAttackCount() => attackCount = 0;

    public float GetBuffRemainingRatio(int index)
    {
        SkillBase skill = GetSlot(index);
        if (skill is BuffSkill buffSkill)
            return buffSkill.BuffRemainingRatio;
        return 0f;
    }

    public Sprite GetSkillIcon(int index)
    {
        SkillBase skill = GetSlot(index);
        return skill?.skillData?.icon;
    }
}
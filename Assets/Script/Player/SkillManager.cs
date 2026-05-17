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
        if (memberScript == null || memberScript.isLeader) return;
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
        if (skill == null) return;

        // 후딜 캔슬 — 현재 스킬 후딜 강제 종료 후 다음 스킬 실행
        if (currentSkill != null && !IsActivatingSkill)
            ForceStopCurrentSkill();

        Transform target = ResolveSkillTarget(skill);
        skill.TryUseSkill(target);
    }

    private Transform ResolveSkillTarget(SkillBase skill)
    {
        if (skill.skillData.skillType == SkillData.SkillType.Heal)
        {
            HealSkillData healData = skill.skillData as HealSkillData;
            if (healData != null && !healData.isAoe)
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

        Transform target = attackBase.currentTarget;

        List<SkillBase> readySlots = new List<SkillBase>();
        foreach (var slot in slots)
        {
            if (slot == null || !slot.IsReady) continue;
            // 힐 스킬은 HP 조건 체크 로직(TryAutoUseHealSkill)이 담당
            if (slot.skillData.skillType == SkillData.SkillType.Heal) continue;
            readySlots.Add(slot);
        }

        if (readySlots.Count == 0) return;

        SkillBase chosen = readySlots[Random.Range(0, readySlots.Count)];

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
        if (PartyManager.instance == null) return;

        foreach (var slot in slots)
        {
            if (slot == null || !slot.IsReady) continue;
            if (slot.skillData.skillType != SkillData.SkillType.Heal) continue;

            HealSkillData healData = slot.skillData as HealSkillData;
            if (healData == null) continue;

            if (healData.isAoe)
            {
                // 파티원 중 누구라도 HP 50% 미만이면 광역 힐
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

    public Sprite GetSkillIcon(int index)
    {
        SkillBase skill = GetSlot(index);
        return skill?.skillData?.icon;
    }
}
using UnityEngine;
using System.Collections.Generic;

public class SkillManager : MonoBehaviour
{
    private PartyMemberScript memberScript;
    private AttackBase attackBase;

    [Header("스킬 슬롯 (Q/W/E/R)")]
    public SkillData slotQ;
    public SkillData slotW;
    public SkillData slotE;
    public SkillData slotR;

    private SkillBase[] slots = new SkillBase[4];

    [Header("자동 스킬 설정 (팔로워 전용)")]
    public int attackPerSkill = 2;
    private int attackCount   = 0;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        memberScript = GetComponent<PartyMemberScript>();
        attackBase   = GetComponent<AttackBase>();

        slots[0] = CreateSkill(slotQ);
        slots[1] = CreateSkill(slotW);
        slots[2] = CreateSkill(slotE);
        slots[3] = CreateSkill(slotR);

        if (attackBase != null)
            attackBase.OnAttackExecuted += HandleAttackCount;
    }

    void OnDestroy()
    {
        if (attackBase != null)
            attackBase.OnAttackExecuted -= HandleAttackCount;
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
            skill.skillData = data;

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

        Transform target = attackBase.currentTarget;
        skill.TryUseSkill(target);
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
        Transform target = attackBase.currentTarget;

        List<SkillBase> readySlots = new List<SkillBase>();
        foreach (var slot in slots)
        {
            if (slot != null && slot.IsReady)
                readySlots.Add(slot);
        }

        if (readySlots.Count == 0) return;

        SkillBase chosen = readySlots[Random.Range(0, readySlots.Count)];

        // 힐/버프 스킬은 타겟 없어도 사용 가능
        if (chosen.skillData.skillType == SkillData.SkillType.Heal ||
            chosen.skillData.skillType == SkillData.SkillType.Buff)
        {
            chosen.TryUseSkill(null);
        }
        else
        {
            chosen.TryUseSkill(target);
        }
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
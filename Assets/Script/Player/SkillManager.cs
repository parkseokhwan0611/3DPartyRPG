using UnityEngine;
using System.Collections.Generic;

public class SkillManager : MonoBehaviour
{
    // ─────────────────────────────────────────
    // 컴포넌트 참조
    // ─────────────────────────────────────────
    private PartyMemberScript memberScript;
    private AttackBase attackBase;

    // ─────────────────────────────────────────
    // 스킬 슬롯 (Q/W/E/R)
    // 인스펙터에서 SO만 드래그하면 런타임에 자동 생성
    // ─────────────────────────────────────────
    [Header("스킬 슬롯 (Q/W/E/R)")]
    public SkillData slotQ;
    public SkillData slotW;
    public SkillData slotE;
    public SkillData slotR;

    // 런타임에 생성된 실제 스킬 컴포넌트
    private SkillBase[] slots = new SkillBase[4];

    // ─────────────────────────────────────────
    // 자동 사용 설정 (팔로워 전용)
    // ─────────────────────────────────────────
    [Header("자동 스킬 설정 (팔로워 전용)")]
    public int attackPerSkill = 2;
    private int attackCount = 0;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        memberScript = GetComponent<PartyMemberScript>();
        attackBase   = GetComponent<AttackBase>();

        // SO 기반으로 슬롯 스킬 자동 생성
        slots[0] = CreateSkill(slotQ);
        slots[1] = CreateSkill(slotW);
        slots[2] = CreateSkill(slotE);
        slots[3] = CreateSkill(slotR);

        if (attackBase != null)
            attackBase.OnAttackStarted += HandleAttackCount;
    }

    void OnDestroy()
    {
        if (attackBase != null)
            attackBase.OnAttackStarted -= HandleAttackCount;
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
            case SkillData.SkillType.Passive:
                // 패시브는 슬롯에 등록하지 않고 별도로 관리
                Debug.LogWarning("[SkillManager] 패시브 스킬은 슬롯이 아닌 패시브 목록에 등록하세요.");
                break;
            default:
                Debug.LogWarning($"[SkillManager] 알 수 없는 스킬 타입: {data.skillType}");
                break;
        }

        if (skill != null)
            skill.skillData = data;

        return skill;
    }

    // ─────────────────────────────────────────────────────────────────
    // 리더 — 플레이어 입력
    // ─────────────────────────────────────────────────────────────────

    public void HandleKeyInput()
    {
        if (!memberScript.isLeader) return;

        if (Input.GetKeyDown(KeyCode.Q)) UseSkillByIndex(0);
        if (Input.GetKeyDown(KeyCode.W)) UseSkillByIndex(1);
        if (Input.GetKeyDown(KeyCode.E)) UseSkillByIndex(2);
        if (Input.GetKeyDown(KeyCode.R)) UseSkillByIndex(3);
    }

    // UI 버튼에서 인덱스로 호출 (0=Q, 1=W, 2=E, 3=R)
    public void UseSkillByIndex(int index)
    {
        if (!memberScript.isLeader) return;

        SkillBase skill = GetSlot(index);
        if (skill == null) return;

        Transform target = attackBase.currentTarget;
        skill.TryUseSkill(target);
    }

    // ─────────────────────────────────────────────────────────────────
    // 팔로워 — 자동 스킬 사용
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
        if (target == null) return;

        // 준비된 슬롯 중 랜덤으로 하나 선택
        List<SkillBase> readySlots = new List<SkillBase>();
        foreach (var slot in slots)
        {
            if (slot != null && slot.IsReady)
                readySlots.Add(slot);
        }

        if (readySlots.Count == 0) return;

        SkillBase chosen = readySlots[Random.Range(0, readySlots.Count)];
        chosen.TryUseSkill(target);
    }

    // ─────────────────────────────────────────────────────────────────
    // 슬롯 관리
    // ─────────────────────────────────────────────────────────────────

    public SkillBase GetSlot(int index)
    {
        if (index < 0 || index >= slots.Length) return null;
        return slots[index];
    }

    // 런타임에서 슬롯 교체 (스킬 장착 UI에서 호출)
    public void SetSlot(int index, SkillData newData)
    {
        if (index < 0 || index >= slots.Length) return;

        // 기존 슬롯 컴포넌트 제거
        if (slots[index] != null)
            Destroy(slots[index]);

        // 새 스킬 생성
        slots[index] = CreateSkill(newData);

        // 인스펙터 SO도 동기화
        switch (index)
        {
            case 0: slotQ = newData; break;
            case 1: slotW = newData; break;
            case 2: slotE = newData; break;
            case 3: slotR = newData; break;
        }
    }

    // 슬롯의 쿨다운 비율 반환 (UI 쿨다운 표시용)
    public float GetCooldownRatio(int index)
    {
        SkillBase skill = GetSlot(index);
        return skill != null ? skill.CooldownRatio : 0f;
    }

    // 슬롯의 스킬 아이콘 반환 (UI용)
    public Sprite GetSkillIcon(int index)
    {
        SkillBase skill = GetSlot(index);
        return skill?.skillData?.icon;
    }
}
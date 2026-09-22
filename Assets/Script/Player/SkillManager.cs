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
    private readonly List<SkillBase> _tempSkills = new List<SkillBase>(4); // 자동 스킬 후보·쿨 초기화 대상 계산용 (매번 할당 방지)

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

        ApplySavedSlotsFromDataManager();

        slots[0] = CreateSkill(slotQ);
        slots[1] = CreateSkill(slotW);
        slots[2] = CreateSkill(slotE);
        slots[3] = CreateSkill(slotR);

        if (attackBase != null)
            attackBase.OnAttackExecuted += HandleAttackCount;
    }

    void Start()
    {
        if (DataManager.instance != null)
            DataManager.instance.OnPartyClassChanged += HandlePartyClassChanged;
    }

    // 무기(클래스)를 바꾸면 배운 스킬이 초기화되므로, 이전 클래스 스킬이 남지 않게 퀵슬롯을 전부 비운다
    private void HandlePartyClassChanged(int partyIndex)
    {
        if (myStat == null || partyIndex != myStat.partyIndex) return;

        for (int i = 0; i < slots.Length; i++)
            SetSlot(i, null);

        CombatQuickSlotUI.instance?.RefreshSlots();
    }

    // PartyManager/SkillManager는 씬 로컬이라 포탈 등으로 씬이 바뀌면 파괴 후 재생성됨.
    // DataManager(DontDestroyOnLoad)에 이전에 배정한 퀵슬롯이 남아있으면 그걸로 덮어써서
    // 씬 전환 후에도 유지되도록 한다. 아직 한 번도 등록한 적 없으면(null) 프리팹 기본값 유지
    private void ApplySavedSlotsFromDataManager()
    {
        if (DataManager.instance == null || myStat == null) return;

        var saved = DataManager.instance.GetQuickSlotSave(myStat.partyIndex);
        if (saved == null) return;

        slotQ = ResolveSavedSkill(saved.slot0, slotQ);
        slotW = ResolveSavedSkill(saved.slot1, slotW);
        slotE = ResolveSavedSkill(saved.slot2, slotE);
        slotR = ResolveSavedSkill(saved.slot3, slotR);
    }

    // 저장된 skillId가 비어있으면 "명시적으로 빈 슬롯"으로 취급 — 프리팹 기본값으로 되돌리지 않음
    private SkillData ResolveSavedSkill(string skillId, SkillData fallback)
    {
        if (string.IsNullOrEmpty(skillId)) return null;
        return DataManager.instance.FindSkillById(skillId) ?? fallback;
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
        if (DataManager.instance != null)
            DataManager.instance.OnPartyClassChanged -= HandlePartyClassChanged;

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
            case SkillData.SkillType.Summon:
                skill = gameObject.AddComponent<SummonSkill>();
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
            var stat = member.StatComp;
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
        List<SkillBase> readySlots = _tempSkills;
        readySlots.Clear();
        foreach (var slot in slots)
        {
            if (slot == null || !slot.IsReady) continue;
            if (slot.skillData.skillType == SkillData.SkillType.Heal)    continue;
            if (slot.skillData.skillType == SkillData.SkillType.Passive) continue;
            if (!slot.CanUseNow || !slot.IsWorthAutoUsing)               continue; // 소환수 없는 도발, 멀쩡한 소환수 재소환 등

            // DispelDebuff 효과가 있는 스킬은 파티원에 디버프가 있을 때만 후보에 포함
            if (IsDispelSkill(slot.skillData) && !AnyMemberHasDebuff()) continue;

            // 자기 자신 쿨 초기화 스킬은 초기화할 스킬이 쿨타임 중일 때만 사용 (파티 대상은 다른 파티원 상황을 모르므로 그대로)
            if (slot.skillData.IsCooldownResetSkill && slot.skillData is BuffSkillData resetData
                && !resetData.isPartyBuff && !HasResettableCooldown()) continue;

            readySlots.Add(slot);
        }

        if (readySlots.Count == 0) return;

        // 우선순위 오름차순 정렬 후 최고 우선순위 그룹에서 랜덤 선택
        readySlots.Sort((a, b) => a.skillData.skillPriority.CompareTo(b.skillData.skillPriority));
        int topPriority = readySlots[0].skillData.skillPriority;

        // 정렬돼 있으므로 앞에서부터 최고 우선순위 그룹 개수만 세서 그 안에서 고른다
        int topCount = 0;
        while (topCount < readySlots.Count && readySlots[topCount].skillData.skillPriority == topPriority)
            topCount++;

        SkillBase chosen = readySlots[Random.Range(0, topCount)];
        readySlots.Clear();

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
            var stat = member.StatComp;
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
            var stat = member.StatComp;
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
        {
            // 실행 중(후딜 등)인 스킬을 교체하는 경우 currentSkill이 파괴될 컴포넌트를
            // 계속 참조하지 않도록 먼저 정리
            if (currentSkill == slots[index])
            {
                // 컴포넌트가 파괴되면 SkillRoutine 끝의 정리(발동 플래그 해제·이동 재개)가 실행되지 않으므로 여기서 대신 처리
                ForceStopCurrentSkill();
                var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
                var status = GetComponent<PartyStatusEffectHandler>();
                bool stunned = status != null && status.HasDebuff(StatusEffectType.Stun);
                if (!stunned && agent != null && agent.enabled && agent.isOnNavMesh)
                    agent.isStopped = false;
                OnAnySkillFinished();
            }
            if (_pendingSkill == slots[index]) { _pendingSkill = null; _pendingTarget = null; }
            Destroy(slots[index]);
        }

        slots[index] = CreateSkill(newData);

        switch (index)
        {
            case 0: slotQ = newData; break;
            case 1: slotW = newData; break;
            case 2: slotE = newData; break;
            case 3: slotR = newData; break;
        }

        // 씬 전환(포탈 이동 등) 후에도 배정이 유지되도록 DataManager에도 실시간 반영
        if (DataManager.instance != null && myStat != null)
            DataManager.instance.SetQuickSlotSkillId(myStat.partyIndex, index, newData?.skillId ?? "");
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

    private bool HasResettableCooldown()
    {
        foreach (var slot in slots)
            if (slot != null && slot.skillData != null && !slot.IsReady && !slot.skillData.IsCooldownResetSkill)
                return true;
        return false;
    }

    // 퀵슬롯 스킬 쿨타임 초기화. maxCount가 0 이하면 전부, 아니면 남은 쿨타임이 긴 것부터 maxCount개.
    // 쿨 초기화 효과가 있는 스킬은 대상에서 뺀다 — 쿨 초기화 스킬끼리 서로 돌려주며 무한히 쓰는 연쇄 방지
    // 반환값: 실제로 초기화한 스킬 개수
    public int ResetCooldowns(int maxCount)
    {
        var candidates = _tempSkills; // 평타 발동형 패시브로 자주 불리므로 리스트 재사용
        candidates.Clear();
        foreach (var slot in slots)
        {
            if (slot == null || slot.skillData == null || slot.IsReady) continue;
            if (slot.skillData.IsCooldownResetSkill) continue;
            candidates.Add(slot);
        }

        int count = candidates.Count;
        if (maxCount > 0 && count > maxCount)
        {
            candidates.Sort((a, b) => b.CooldownRemaining.CompareTo(a.CooldownRemaining));
            count = maxCount;
        }

        for (int i = 0; i < count; i++)
            candidates[i].SetCooldown(0f);

        candidates.Clear();
        return count;
    }

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
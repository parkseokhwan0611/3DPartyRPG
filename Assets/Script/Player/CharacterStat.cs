using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System; // Action을 사용하기 위해 필요

public class CharacterStat : MonoBehaviour, IDamageable
{
    [Header("# Refences")]
    public GameObject playerDamageText;
    [Tooltip("ObjectPoolManager에 등록된 플레이어 피격 텍스트 풀 키")]
    public string playerDamageTextPoolKey = "PlayerDamageText";
    public Transform hudPos;
    public ClassData classData;
    [Header("# 스킬 연계 버프 VFX")]
    public GameObject nextSkillBuffAura;
    [Header("# 버프 아우라 슬롯 (인덱스 0~N)")]
    public GameObject[] buffAuras;
    [Header("# 힐 아우라")]
    public int   healAuraIndex    = -1;   // buffAuras 배열에서 힐 아우라의 인덱스 (-1 = 비활성)
    public float healAuraDuration = 1.5f; // 아우라 유지 시간 (초)
    [Header("# 힐 텍스트")]
    public string healTextPoolKey = "HealText";
    public Color  healTextColor   = new Color(0.2f, 1f, 0.2f);
    [Header("# 마나 회복 아우라")]
    public int   manaAuraIndex    = -1;   // buffAuras 배열에서 마나 회복 아우라의 인덱스 (-1 = 비활성)
    public float manaAuraDuration = 1.5f; // 아우라 유지 시간 (초)
    [Header("# 마나 회복 텍스트")]
    public string manaTextPoolKey = "HealText"; // HealText 풀 재사용 (범용 +수치 텍스트)
    public Color  manaTextColor   = new Color(0.3f, 0.5f, 1f);
    public event Action OnHpChanged;
    public event Action OnMpChanged;
    private CharacterStatus myStatus;
    private PartyMemberScript myMember;
    private PartyStatusEffectHandler shieldHandler;
    private Coroutine healAuraCoroutine;
    private Coroutine manaAuraCoroutine;
    public int partyIndex;
    public float Hp     => myStatus != null ? myStatus.currentHp   : 0f;
    public float MaxHp  => myStatus != null ? myStatus.MaxHp       : 0f;
    public float Mp     => myStatus != null ? myStatus.currentMp   : 0f;
    public float MaxMp  => myStatus != null ? myStatus.MaxMp       : 0f;
    public float TotalAtk        => myStatus != null ? myStatus.TotalAtk        : 0f;
    public float TotalAp         => myStatus != null ? myStatus.TotalAp         : 0f;
    public float TotalDef        => myStatus != null ? myStatus.TotalDef        : 0f;
    public float TotalMagicRes   => myStatus != null ? myStatus.TotalMagicRes   : 0f;
    public float TotalCritRate   => myStatus != null ? myStatus.TotalCritRate   : 0f;
    public float TotalCritDamage => myStatus != null ? myStatus.TotalCritDamage : 0f;
    public float HpOnHit       => myStatus != null ? myStatus.hpOnHit      : 0f;
    public float MpOnHit       => myStatus != null ? myStatus.mpOnHit      : 0f;
    // 패시브 + 장비 합산 (TotalPhysDmgBonus / TotalMagicDmgBonus 사용)
    public float PhysDmgBonus  => myStatus != null ? myStatus.TotalPhysDmgBonus  : 0f;
    public float MagicDmgBonus => myStatus != null ? myStatus.TotalMagicDmgBonus : 0f;
    public float HealBonus     => myStatus != null ? myStatus.healBonus           : 0f;
    public float TotalMoveSpeed      => myStatus != null ? myStatus.TotalMoveSpeed          : 3f;
    public float MoveSpeedMultiplier => myStatus != null ? myStatus.moveSpeedMultiplier     : 1f;
    // 스킬 쿨타임·마나 소모 감소
    public float TotalCDReduce => myStatus != null ? myStatus.TotalCDReduce : 0f;
    public float TotalMpReduce => myStatus != null ? myStatus.TotalMpReduce : 0f;

    // 스킬 레벨 조회 (SkillManager가 SkillBase.skillLevel 동기화에 사용)
    public int GetSkillLevel(SkillData skill) => myStatus != null ? myStatus.GetSkillLevel(skill) : 0;

    // 아이템/장비 평탄 보너스 (읽기 + 쓰기 모두 필요해서 프로퍼티로 래핑)
    public float BonusAtk { get => myStatus?.bonusAtk ?? 0f; set { if (myStatus != null) myStatus.bonusAtk = value; } }
    public float BonusAp  { get => myStatus?.bonusAp  ?? 0f; set { if (myStatus != null) myStatus.bonusAp  = value; } }
    public float BonusDef { get => myStatus?.bonusDef ?? 0f; set { if (myStatus != null) myStatus.bonusDef = value; } }

    // 원시 스탯 (base + added)
    public float TotalStr => myStatus != null ? myStatus.classData.baseStr + myStatus.addedStr : 0f;
    public float TotalVit => myStatus != null ? myStatus.classData.baseVit + myStatus.addedVit : 0f;
    public float TotalInt => myStatus != null ? myStatus.classData.baseInt + myStatus.addedInt : 0f;
    public float TotalFth => myStatus != null ? myStatus.classData.baseFht + myStatus.addedFht : 0f;

    void Awake()
    {
        myMember      = GetComponent<PartyMemberScript>();
        shieldHandler = GetComponent<PartyStatusEffectHandler>();
    }

    // DataManager.Awake()가 이 오브젝트의 Awake()보다 늦게 실행될 수 있어(오브젝트 간 실행 순서
    // 미보장) Awake에서 바로 구독하면 DataManager.instance가 아직 null이라 조용히 스킵될 수 있음.
    // Awake는 항상 모든 Start보다 먼저 끝나는 것이 보장되므로 Start에서 바인딩/구독
    void OnDestroy()
    {
        if (DataManager.instance != null)
            DataManager.instance.OnDataInitialized -= BindStatus;

        // 구독자가 죽은 오브젝트를 참조하지 않도록 이벤트 초기화
        OnHpChanged = null;
        OnMpChanged = null;
    }

    private void BindStatus()
    {
        if (DataManager.instance == null)
        {
            Debug.LogWarning($"[CharacterStat] {gameObject.name}: DataManager가 없습니다.");
            return;
        }

        if (partyIndex < 0 || partyIndex >= DataManager.instance.partyStatuses.Count)
        {
            Debug.LogWarning($"[CharacterStat] {gameObject.name}: partyIndex({partyIndex})가 범위를 벗어났습니다. Inspector를 확인하세요.");
            return;
        }

        myStatus = DataManager.instance.partyStatuses[partyIndex];
    }

    void Start()
    {
        BindStatus();
        if (DataManager.instance != null)
            DataManager.instance.OnDataInitialized += BindStatus;

        StartCoroutine(RegenRoutine());
    }

    // 1초 틱 — 힐 텍스트·아우라 없이 조용히 HP/MP만 회복
    private IEnumerator RegenRoutine()
    {
        var tick = new WaitForSeconds(1f);
        while (true)
        {
            yield return tick;
            if (myStatus == null || myStatus.currentHp <= 0f) continue;

            float hpRegen = myStatus.TotalHpRegen;
            if (hpRegen > 0f && myStatus.currentHp < myStatus.MaxHp)
            {
                myStatus.currentHp = Mathf.Clamp(myStatus.currentHp + hpRegen, 0f, myStatus.MaxHp);
                myStatus.RaiseHpChanged();
                OnHpChanged?.Invoke();
            }

            float mpRegen = myStatus.TotalMpRegen;
            if (mpRegen > 0f && myStatus.currentMp < myStatus.MaxMp)
            {
                myStatus.currentMp = Mathf.Clamp(myStatus.currentMp + mpRegen, 0f, myStatus.MaxMp);
                myStatus.RaiseMpChanged();
                OnMpChanged?.Invoke();
            }
        }
    }

    void Update()
    {
        if (myStatus == null) return;

        if (myStatus.nextSkillBonusTimer > 0)
        {
            myStatus.nextSkillBonusTimer -= Time.deltaTime;

            if (myStatus.nextSkillBonusTimer <= 0f)
            {
                myStatus.nextSkillDamageBonus = 0f;

                if (nextSkillBuffAura != null)
                    nextSkillBuffAura.SetActive(false);
            }
        }

        // 부활 쿨타임 카운트다운
        if (myStatus.reviveCooldownTimer > 0f)
            myStatus.reviveCooldownTimer -= Time.deltaTime;
    }

    // 물리 데미지 (방어력으로 경감)
    public void TakeDamage(float damage, GameObject attacker)
    {
        if (myStatus == null) return;

        float reduction   = TotalDef / (TotalDef + 100f);
        float finalDamage = damage * (1f - reduction);
        ApplyDamage(finalDamage);
    }

    // 마법 데미지 (마법저항력으로 경감)
    public void TakeMagicDamage(float damage, GameObject attacker)
    {
        if (myStatus == null) return;

        float reduction   = TotalMagicRes / (TotalMagicRes + 100f);
        float finalDamage = damage * (1f - reduction);
        ApplyDamage(finalDamage);
    }

    private void ApplyDamage(float finalDamage)
    {
        if (shieldHandler != null)
            finalDamage = shieldHandler.AbsorbDamage(finalDamage);

        if (finalDamage <= 0f) return;

        myStatus.currentHp = Mathf.Clamp(myStatus.currentHp - finalDamage, 0, myStatus.MaxHp);
        myStatus.RaiseHpChanged();
        OnHpChanged?.Invoke();

        SpawnDamageText(finalDamage, new Color(1f, 0.55f, 0f)); // 주황색 피격 텍스트

        if (myStatus.currentHp <= 0) Die();
    }

    public void HealHp(float amount)
    {
        if (myStatus == null || amount <= 0f) return;
        myStatus.currentHp = Mathf.Clamp(myStatus.currentHp + amount, 0, myStatus.MaxHp);
        myStatus.RaiseHpChanged();
        OnHpChanged?.Invoke();
        SpawnHealText(amount);
        ShowHealAura();
    }

    public void ShowHealAura()
    {
        if (healAuraIndex < 0) return;
        if (buffAuras == null || healAuraIndex >= buffAuras.Length) return;
        if (buffAuras[healAuraIndex] == null) return;

        if (healAuraCoroutine != null)
            StopCoroutine(healAuraCoroutine);

        healAuraCoroutine = StartCoroutine(HealAuraRoutine());
    }

    private IEnumerator HealAuraRoutine()
    {
        // 껐다가 켜기 (이미 켜진 경우에도 깜박임 효과)
        DeactivateBuffAura(healAuraIndex);
        yield return new WaitForSeconds(0.05f);
        ActivateBuffAura(healAuraIndex);
        yield return new WaitForSeconds(healAuraDuration);
        DeactivateBuffAura(healAuraIndex);
        healAuraCoroutine = null;
    }

    private void SpawnHealText(float amount)
    {
        if (string.IsNullOrEmpty(healTextPoolKey)) return;
        if (ObjectPoolManager.instance == null || !ObjectPoolManager.instance.IsReady) return;

        var go = ObjectPoolManager.instance.GetGo(healTextPoolKey);
        if (go == null) return;

        Vector3 spawnPos = hudPos != null ? hudPos.position : transform.position + Vector3.up * 2f;
        go.transform.position = spawnPos;
        go.transform.rotation = Quaternion.Euler(60f, 0f, 0f);

        go.GetComponent<HealText>()?.Setup(amount, healTextColor);
    }

    public void ShowManaAura()
    {
        if (manaAuraIndex < 0) return;
        if (buffAuras == null || manaAuraIndex >= buffAuras.Length) return;
        if (buffAuras[manaAuraIndex] == null) return;

        if (manaAuraCoroutine != null)
            StopCoroutine(manaAuraCoroutine);

        manaAuraCoroutine = StartCoroutine(ManaAuraRoutine());
    }

    private IEnumerator ManaAuraRoutine()
    {
        // 껐다가 켜기 (이미 켜진 경우에도 깜박임 효과)
        DeactivateBuffAura(manaAuraIndex);
        yield return new WaitForSeconds(0.05f);
        ActivateBuffAura(manaAuraIndex);
        yield return new WaitForSeconds(manaAuraDuration);
        DeactivateBuffAura(manaAuraIndex);
        manaAuraCoroutine = null;
    }

    private void SpawnManaText(float amount)
    {
        if (string.IsNullOrEmpty(manaTextPoolKey)) return;
        if (ObjectPoolManager.instance == null || !ObjectPoolManager.instance.IsReady) return;

        var go = ObjectPoolManager.instance.GetGo(manaTextPoolKey);
        if (go == null) return;

        Vector3 spawnPos = hudPos != null ? hudPos.position : transform.position + Vector3.up * 2f;
        go.transform.position = spawnPos;
        go.transform.rotation = Quaternion.Euler(60f, 0f, 0f);

        go.GetComponent<HealText>()?.Setup(amount, manaTextColor);
    }

    private void SpawnDamageText(float damage, Color color)
    {
        Vector3    spawnPos = hudPos != null ? hudPos.position : transform.position + Vector3.up * 2f;
        Quaternion spawnRot = Quaternion.Euler(60f, 0f, 0f);

        GameObject textObj = null;

        if (!string.IsNullOrEmpty(playerDamageTextPoolKey)
            && ObjectPoolManager.instance != null
            && ObjectPoolManager.instance.IsReady)
        {
            textObj = ObjectPoolManager.instance.GetGo(playerDamageTextPoolKey);
        }

        if (textObj == null && playerDamageText != null)
            textObj = Instantiate(playerDamageText); // 풀 미등록 시 fallback

        if (textObj == null) return;

        textObj.transform.SetPositionAndRotation(spawnPos, spawnRot);
        textObj.GetComponent<DamageText>()?.Setup(damage, color, showMinus: true);
    }

    // isPhysical: true = 물리 피해 (연한 붉은색), false = 마법 피해 (연한 파란색)
    public Color GetDamageColor(bool isPhysical)
    {
        return isPhysical
            ? new Color(1f, 0f, 0f)   // 빨강
            : new Color(0f, 0f, 1f);  // 파랑
    }

    public bool TryUseMp(float cost)
    {
        if (myStatus == null) return false;
        if (myStatus.currentMp < cost) return false;

        myStatus.currentMp = Mathf.Clamp(myStatus.currentMp - cost, 0, myStatus.MaxMp);
        myStatus.RaiseMpChanged();
        OnMpChanged?.Invoke();
        return true;
    }

    public void RecoverMp(float amount)
    {
        if (myStatus == null || amount <= 0f) return;
        myStatus.RecoverMp(amount);
        OnMpChanged?.Invoke();
        SpawnManaText(amount);
        ShowManaAura();
    }

    public void RaiseHpChanged() => OnHpChanged?.Invoke();
    public void RaiseMpChanged() => OnMpChanged?.Invoke();

    public void ApplyNextSkillBuff(float bonus, float duration)
    {
        if (myStatus == null) return;
        myStatus.nextSkillDamageBonus = bonus;
        myStatus.nextSkillBonusTimer  = duration;

        if (nextSkillBuffAura != null)
            nextSkillBuffAura.SetActive(true);
    }

    public float ConsumeNextSkillBonus()
    {
        if (myStatus == null || myStatus.nextSkillBonusTimer <= 0f) return 0f;

        float bonus = myStatus.nextSkillDamageBonus;
        myStatus.nextSkillDamageBonus = 0f;
        myStatus.nextSkillBonusTimer  = 0f;

        if (nextSkillBuffAura != null)
            nextSkillBuffAura.SetActive(false);

        return bonus;
    }

    public void ActivateBuffAura(int index)
    {
        if (buffAuras == null || index < 0 || index >= buffAuras.Length) return;
        if (buffAuras[index] != null) buffAuras[index].SetActive(true);
    }

    public void DeactivateBuffAura(int index)
    {
        if (buffAuras == null || index < 0 || index >= buffAuras.Length) return;
        if (buffAuras[index] != null) buffAuras[index].SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────
    // 버프 아우라 참조 카운트 — 같은 아우라 인덱스를 여러 버프가 동시에 쓸 때,
    // 그중 마지막 버프가 꺼질 때만 실제로 아우라를 끔
    // ─────────────────────────────────────────────────────────────────
    private int[] _buffAuraRefCounts;

    private void EnsureBuffAuraRefCounts()
    {
        int len = buffAuras != null ? buffAuras.Length : 0;
        if (_buffAuraRefCounts == null || _buffAuraRefCounts.Length != len)
            _buffAuraRefCounts = new int[len];
    }

    public void AcquireBuffAura(int index)
    {
        if (buffAuras == null || index < 0 || index >= buffAuras.Length) return;
        EnsureBuffAuraRefCounts();

        _buffAuraRefCounts[index]++;
        if (_buffAuraRefCounts[index] == 1)
            ActivateBuffAura(index);
    }

    public void ReleaseBuffAura(int index)
    {
        if (buffAuras == null || index < 0 || index >= buffAuras.Length) return;
        EnsureBuffAuraRefCounts();

        _buffAuraRefCounts[index] = Mathf.Max(0, _buffAuraRefCounts[index] - 1);
        if (_buffAuraRefCounts[index] == 0)
            DeactivateBuffAura(index);
    }

    void Die()
    {
        if (myStatus == null) return;

        // Revive 패시브 체크 (쿨타임이 0이고 스킬 보유 시 부활)
        if (TryRevive()) return;

        // 실제 사망 처리 (myMember 없으면 파티원이 아님 → 파티 알림 스킵)
        if (myMember == null) return;
        myMember.Die();
        AudioManager.instance?.PlaySFX("PartyMemberDeath");
        PartyManager.instance?.OnMemberDied(myMember);
    }

    private bool TryRevive()
    {
        if (myStatus.reviveCooldownTimer > 0f) return false;

        foreach (var kvp in myStatus.skillLevels)
        {
            if (kvp.Key is PassiveSkillData passive &&
                passive.effectType == PassiveSkillData.PassiveEffectType.Revive &&
                kvp.Value > 0)
            {
                float ratio = passive.GetValue(kvp.Value);
                if (ratio <= 0f) ratio = 0.2f; // SO에 값 미설정 시 기본 20%

                myStatus.currentHp           = myStatus.MaxHp * ratio;
                myStatus.reviveCooldownTimer = 600f; // 10분
                myStatus.RaiseHpChanged();
                OnHpChanged?.Invoke();
                return true;
            }
        }
        return false;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System; // Action을 사용하기 위해 필요

public class CharacterStat : MonoBehaviour, IDamageable
{
    [Header("# Refences")]
    public GameObject playerDamageText;
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
    public event Action OnHpChanged;
    public event Action OnMpChanged;
    private CharacterStatus myStatus;
    private Coroutine healAuraCoroutine;
    public int partyIndex;
    public float Hp     => myStatus.currentHp;
    public float MaxHp  => myStatus.MaxHp;
    public float Mp     => myStatus.currentMp;
    public float MaxMp  => myStatus.MaxMp;
    public float TotalAtk       => myStatus.TotalAtk;
    public float TotalAp        => myStatus.TotalAp;
    public float TotalDef       => myStatus.TotalDef;
    public float TotalMagicRes  => myStatus.TotalMagicRes;
    public float TotalCritRate   => myStatus.TotalCritRate;
    public float TotalCritDamage => myStatus.TotalCritDamage;
    public float HpOnHit       => myStatus != null ? myStatus.hpOnHit      : 0f;
    public float MpOnHit       => myStatus != null ? myStatus.mpOnHit      : 0f;
    public float PhysDmgBonus  => myStatus != null ? myStatus.physDmgBonus  : 0f;
    public float MagicDmgBonus => myStatus != null ? myStatus.magicDmgBonus : 0f;
    public float HealBonus     => myStatus != null ? myStatus.healBonus     : 0f;

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
        if (DataManager.instance != null)
        {
            if (partyIndex < DataManager.instance.partyStatuses.Count)
                myStatus = DataManager.instance.partyStatuses[partyIndex];
        }
    }

    void Start()
    {
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
        var shieldHandler = GetComponent<PartyStatusEffectHandler>();
        if (shieldHandler != null)
            finalDamage = shieldHandler.AbsorbDamage(finalDamage);

        if (finalDamage <= 0f) return;

        myStatus.currentHp = Mathf.Clamp(myStatus.currentHp - finalDamage, 0, myStatus.MaxHp);
        myStatus.RaiseHpChanged();
        OnHpChanged?.Invoke();

        SpawnDamageText(finalDamage, Color.red);

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

    private void SpawnDamageText(float damage, Color color)
    {
        if (playerDamageText == null) return;

        Quaternion spawnRotation = Quaternion.Euler(60f, 0f, 0f);
        Vector3 spawnPos = hudPos != null ? hudPos.position : transform.position + Vector3.up * 2f;
        GameObject textObj = Instantiate(playerDamageText, spawnPos, spawnRotation);

        DamageText dt = textObj.GetComponent<DamageText>();
        if (dt != null)
            dt.Setup(damage, color);
    }

    public Color GetDamageColor()
    {
        if (classData == null) return Color.white;

        switch (classData.classType)
        {
            case ClassData.ClassType.Tanker: return new Color(1f, 0.5f, 0f);
            case ClassData.ClassType.Dealer: return new Color(0.6f, 0f, 1f);
            case ClassData.ClassType.Healer: return new Color(1f, 0.9f, 0f);
            default: return Color.white;
        }
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
        if (myStatus == null) return;
        myStatus.RecoverMp(amount);
        OnMpChanged?.Invoke();
    }

    public void RaiseHpChanged() => OnHpChanged?.Invoke();

    public void ApplyNextSkillBuff(float bonus, float duration)
    {
        myStatus.nextSkillDamageBonus = bonus;
        myStatus.nextSkillBonusTimer  = duration;

        if (nextSkillBuffAura != null)
            nextSkillBuffAura.SetActive(true);
    }

    public float ConsumeNextSkillBonus()
    {
        if (myStatus.nextSkillBonusTimer <= 0f) return 0f;

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

    void Die() { /* 사망 로직 */ }
}

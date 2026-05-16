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
    public event Action OnHpChanged;
    public event Action OnMpChanged;
    private CharacterStatus myStatus;
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
    public float HpOnHit => myStatus != null ? myStatus.hpOnHit : 0f;

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

        myStatus.currentMp -= cost;
        myStatus.currentMp = Mathf.Clamp(myStatus.currentMp, 0, myStatus.MaxMp);
        OnMpChanged?.Invoke();
        return true;
    }

    public void RaiseHpChanged()
    {
        OnHpChanged?.Invoke();
    }

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

using UnityEngine;
using UnityEngine.AI;
using System.Collections;

// 소환사 스킬 실행 — SummonSkillData.action에 따라 소환수를 부르거나(Summon), 소환수에게 도발을 시킨다(Taunt)
public class SummonSkill : SkillBase
{
    private static readonly Collider[] _hitBuffer = new Collider[32];
    private int enemyLayer;

    private SummonSkillData summonData;

    protected override void Awake()
    {
        base.Awake();
        enemyLayer = LayerMask.GetMask("Enemy");
    }

    private SummonSkillData GetSummonData()
    {
        if (summonData == null)
            summonData = skillData as SummonSkillData;

        if (summonData == null)
            Debug.LogError($"[SummonSkill] {gameObject.name}의 skillData가 SummonSkillData가 아닙니다!");

        return summonData;
    }

    // 도발은 명령할 소환수가 1기 이상 있어야 사용 가능 (없으면 마나·쿨타임을 쓰지 않고 거절)
    public override bool CanUseNow
    {
        get
        {
            var data = GetSummonData();
            if (data == null) return false;
            if (data.action == SummonSkillData.SummonAction.Taunt)
                return SummonUnit.CountFor(myStat, data.tauntKind) > 0;
            if (data.action == SummonSkillData.SummonAction.Summon)
                return data.summonPrefab != null;
            return true;
        }
    }

    // 지금 못 쓰는 이유 (스킬 상태 텍스트용) — 도발할 소환수가 없을 때
    public override string CannotUseMessage
    {
        get
        {
            var data = GetSummonData();
            if (data != null && data.action == SummonSkillData.SummonAction.Taunt && !CanUseNow)
                return "소환수가 존재하지 않습니다.";
            return null;
        }
    }

    // 팔로워 자동 사용 판단 — 소환수가 이미 다 있고 시간이 넉넉하면 재소환하지 않고,
    // 도발은 소환수 주변에 몬스터가 있을 때만
    public override bool IsWorthAutoUsing
    {
        get
        {
            var data = GetSummonData();
            if (data == null || !CanUseNow) return false;

            if (data.action == SummonSkillData.SummonAction.Summon)
            {
                int want = data.GetSummonCount(skillLevel);
                int have = 0;
                float minRemainRatio = 1f;
                foreach (var s in SummonUnit.All)
                {
                    if (s == null || !s.IsAlive || s.Owner != myStat || s.SourceSkill != data) continue;
                    have++;
                    minRemainRatio = Mathf.Min(minRemainRatio, s.RemainingTime / s.Duration);
                }
                return have < want || minRemainRatio < 0.3f;
            }

            foreach (var s in SummonUnit.All)
            {
                if (s == null || !s.IsAlive || s.Owner != myStat || s.kind != data.tauntKind) continue;
                if (Physics.OverlapSphereNonAlloc(s.transform.position, data.tauntRadius, _hitBuffer, enemyLayer) > 0) return true;
            }
            return false;
        }
    }

    protected override IEnumerator ExecuteSkill(Transform target)
    {
        var data = GetSummonData();
        if (data == null) yield break;

        // 1. 애니메이션 즉시 실행
        if (anim != null && !string.IsNullOrEmpty(data.animTriggerName))
        {
            anim.ResetTrigger(data.animTriggerName);
            anim.SetTrigger(data.animTriggerName);
        }

        PlaySkillSfx(data);

        // 2. 이펙트 타이밍 대기
        if (data.effectSpawnDelay > 0f)
            yield return new WaitForSeconds(data.effectSpawnDelay);

        // 3. 시전자 이펙트 + 효과
        SpawnCasterEffect(data);
        if (data.action == SummonSkillData.SummonAction.Summon) DoSummon(data);
        else                                                   DoTaunt(data);

        // 효과 적용 완료 → 후딜 캔슬 허용
        ReleaseActivating();

        // 4. 후딜 대기
        float remaining = data.animDuration - data.effectSpawnDelay;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    // ─────────────────────────────────────────────────────────────────
    // 소환 — 같은 스킬로 부른 소환수는 교체, 처음부터 따라다니는 자리(시전자 뒤쪽 좌우)에 배치
    // ─────────────────────────────────────────────────────────────────

    private void DoSummon(SummonSkillData data)
    {
        if (data.summonPrefab == null || myStat == null) return;

        SummonUnit.DespawnFrom(myStat, data);

        int count = data.GetSummonCount(skillLevel);
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = SummonUnit.FollowSpot(transform, i);
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 2f, NavMesh.AllAreas)) pos = hit.position;
            else pos = transform.position;

            SummonUnit unit = Instantiate(data.summonPrefab, pos, transform.rotation);
            unit.Initialize(myStat, data, skillLevel, i);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 도발 — 지정 종류 소환수마다 주변 몬스터의 어그로를 그 소환수에게 몰아준다
    // ─────────────────────────────────────────────────────────────────

    private readonly System.Collections.Generic.HashSet<EnemyHp> _tauntHit = new System.Collections.Generic.HashSet<EnemyHp>();

    private void DoTaunt(SummonSkillData data)
    {
        float aggro = data.GetTauntAggro(skillLevel);

        // 도발 데미지 — 시전자 공격력 × 비율 × (1 + 물리/마법 데미지 증가). 치명타는 몬스터마다 굴림
        float baseDamage = 0f;
        float ratio = data.GetTauntDamageRatio(skillLevel);
        if (ratio > 0f && myStat != null)
            baseDamage = (data.useAp ? myStat.TotalAp : myStat.TotalAtk) * ratio
                       * (1f + (data.useAp ? myStat.MagicDmgBonus : myStat.PhysDmgBonus));

        _tauntHit.Clear();
        bool anyCrit = false;

        foreach (var s in SummonUnit.All.ToArray())
        {
            if (s == null || !s.IsAlive || s.Owner != myStat || s.kind != data.tauntKind) continue;

            int hitCount = Physics.OverlapSphereNonAlloc(s.transform.position, data.tauntRadius, _hitBuffer, enemyLayer);
            for (int i = 0; i < hitCount; i++)
            {
                var monster = _hitBuffer[i].GetComponentInParent<BasicMonsterScript>();
                if (monster != null && aggro > 0f) monster.AddAggro(s.transform, aggro);

                // 소환수 여럿의 범위가 겹쳐도 몬스터당 데미지는 한 번만
                if (baseDamage <= 0f) continue;
                var enemyHp = _hitBuffer[i].GetComponentInParent<EnemyHp>();
                if (enemyHp == null || enemyHp.isDead || !_tauntHit.Add(enemyHp)) continue;

                bool  isCrit = Random.value < myStat.TotalCritRate;
                float damage = isCrit ? baseDamage * myStat.TotalCritDamage : baseDamage;
                if (data.useAp) enemyHp.TakeMagicDamage(damage, gameObject, isCrit);
                else            enemyHp.TakeDamage(damage, gameObject, isCrit);
                anyCrit |= isCrit;
            }

            SpawnEffectAt(data.tauntEffectPoolKey, s.transform.position);
        }

        // 다른 스킬과 같이 치명타가 하나라도 있으면 한 번 흔든다
        if (anyCrit) CinemachineShake.ShakeCrit();
    }

    // ─────────────────────────────────────────────────────────────────
    // 이펙트
    // ─────────────────────────────────────────────────────────────────

    private void SpawnCasterEffect(SummonSkillData data)
    {
        if (string.IsNullOrEmpty(data.effectPoolKey) || ObjectPoolManager.instance == null) return;

        var effect = ObjectPoolManager.instance.GetGo(data.effectPoolKey);
        if (effect != null)
        {
            effect.transform.position = transform.position + transform.rotation * data.effectSpawnOffset;
            effect.transform.rotation = transform.rotation * Quaternion.Euler(data.effectSpawnRotation);
        }
    }

    private static void SpawnEffectAt(string key, Vector3 pos)
    {
        if (string.IsNullOrEmpty(key) || ObjectPoolManager.instance == null) return;
        var effect = ObjectPoolManager.instance.GetGo(key);
        if (effect != null) effect.transform.position = pos;
    }
}

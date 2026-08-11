using UnityEngine;

// 정예/보스 몬스터의 "기본 공격 외 추가 스킬" 공통 기반. AttackBase가 기본 공격을 담당하는 것과
// 같은 방식으로, 스킬 하나당 이 클래스를 상속받은 컴포넌트 하나를 몬스터에 붙인다.
// 구체적인 효과(범위 피해, 소환, 자버프 등)는 자식 클래스의 ExecuteSkill()에서 구현하면 되고,
// 트리거 판단(쿨다운/사거리/사용 확률)은 EliteMonsterSkillController/BossMonsterSkillController가 담당한다.
public abstract class MonsterSkillBase : MonoBehaviour
{
    [Header("# 스킬 공통 설정")]
    [Tooltip("스킬 재사용 대기시간(초)")]
    public float cooldown = 10f;
    [Tooltip("이 거리 안에 타겟이 있어야 스킬 사용을 고려함 (기본 공격 사거리와 별개)")]
    public float range = 6f;
    [Tooltip("쿨다운이 다 됐고 사거리 안일 때, 기본 공격 대신 이 스킬을 사용할 확률")]
    [Range(0f, 1f)] public float useChance = 0.4f;
    [Tooltip("시전 예고(텔레그래프) 시간 — 보스 스킬 인디케이터를 붙일 지점. 이 시간 동안 " +
             "몬스터는 멈춰서 시전 모션만 재생하고, 끝나면 ExecuteSkill이 실행된다")]
    public float windupDuration = 0.6f;
    [Tooltip("ExecuteSkill 실행 후(투사체 발사 등) 애니메이션 후딜레이 — 이 시간이 끝나야 " +
             "이동을 재개한다. 시전 애니메이션 클립 길이에서 windupDuration을 뺀 만큼으로 맞추면 됨. " +
             "너무 짧게 두면 애니메이션이 끝나기 전에 움직이기 시작해서 어색해 보인다")]
    public float recoveryDuration = 0.5f;
    [Tooltip("이 스킬 시전 시 재생할 애니메이터 트리거 이름. 스킬마다 다른 시전 모션을 쓸 수 있도록 " +
             "컨트롤러가 아니라 스킬 자신이 갖고 있음 (비워두면 트리거 없이 진행)")]
    public string animTrigger = "doSkill";
    [Tooltip("AudioManager에 등록한 SFX 키. ExecuteSkill 실행과 같은 타이밍에 재생 (비워두면 재생 안 함)")]
    public string skillSfxKey;

    private float _cooldownRemaining;

    public bool IsReady(Vector3 selfPos, Vector3 targetPos)
        => _cooldownRemaining <= 0f && Vector3.Distance(selfPos, targetPos) <= range;

    public void Tick(float dt)
    {
        if (_cooldownRemaining > 0f) _cooldownRemaining -= dt;
    }

    public void StartCooldown() => _cooldownRemaining = cooldown;

    // 예고 연출 시작 시 호출 (스킬 인디케이터 표시 등). 필요 없으면 비워둬도 됨
    public virtual void OnWindupStart(Transform target) { }

    // 예고 시간이 끝난 뒤 실제 효과 실행
    public abstract void ExecuteSkill(Transform target);
}

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
    [Tooltip("ExecuteSkill 실행 후 그 효과 자체가 유지/전개되는 시간(초) — 예: 폭발 이펙트가 실제로 " +
             "터지는 데 시간이 걸리거나, 판정 존이 windupDuration이 끝난 뒤에도 한동안 유효한 경우. " +
             "이 시간 동안 인디케이터가 계속 보이다가 끝나는 시점에 사라진다. 즉시 끝나는 스킬이면 0")]
    public float attackDuration = 0f;
    [Tooltip("ExecuteSkill + attackDuration이 모두 끝난 뒤(인디케이터가 사라진 뒤) 애니메이션 후딜레이 — " +
             "이 시간이 끝나야 이동을 재개한다. 시전 애니메이션 클립 길이에서 windupDuration+attackDuration을 " +
             "뺀 만큼으로 맞추면 됨. 너무 짧게 두면 애니메이션이 끝나기 전에 움직이기 시작해서 어색해 보인다")]
    public float recoveryDuration = 0.5f;
    [Tooltip("이 스킬 시전 시 재생할 애니메이터 트리거 이름. 스킬마다 다른 시전 모션을 쓸 수 있도록 " +
             "컨트롤러가 아니라 스킬 자신이 갖고 있음 (비워두면 트리거 없이 진행)")]
    public string animTrigger = "doSkill";
    [Tooltip("AudioManager에 등록한 SFX 키. ExecuteSkill 실행과 같은 타이밍에 재생 (비워두면 재생 안 함)")]
    public string skillSfxKey;
    [Tooltip("이 스킬을 사용할 수 있는 체력 비율 상한 (1 = 항상 사용 가능, 0.7 = 체력이 70% 이하로 " +
             "떨어져야 비로소 사용 가능 — 보스 페이즈 해금용)")]
    [Range(0f, 1f)] public float unlockHpRatio = 1f;

    private float _cooldownRemaining;
    private float _cooldownScale = 1f;

    public virtual bool IsReady(Vector3 selfPos, Vector3 targetPos)
        => _cooldownRemaining <= 0f && Vector3.Distance(selfPos, targetPos) <= range;

    public bool IsUnlockedAt(float hpRatio) => hpRatio <= unlockHpRatio;

    public void Tick(float dt)
    {
        if (_cooldownRemaining > 0f) _cooldownRemaining -= dt;
    }

    public void StartCooldown() => _cooldownRemaining = cooldown * _cooldownScale;

    // 광폭화 등으로 쿨다운에 배율을 적용 (1 = 원래대로, 0.7 = 쿨다운 30% 감소)
    public void SetCooldownScale(float scale) => _cooldownScale = Mathf.Max(0.01f, scale);

    // 예고 연출 시작 시 호출 (스킬 인디케이터 표시 등). 필요 없으면 비워둬도 됨
    public virtual void OnWindupStart(Transform target) { }

    // 예고 시간이 끝나는 시점(정상 실행 직전) 또는 시전이 강제 취소됐을 때 호출 —
    // OnWindupStart에서 띄운 인디케이터를 여기서 반드시 정리해야 취소 시에도 화면에 안 남는다
    public virtual void OnWindupEnd() { }

    // 시전이 강제 취소됐을 때만 호출 (정상 완료 시에는 호출되지 않음) — ExecuteSkill 안에서
    // 자체적으로 지연 실행 코루틴(예: 이펙트 후 데미지 지연 적용) 등을 띄웠다면 여기서 정지해야
    // 몬스터가 죽거나 스턴당한 뒤에도 뒤늦게 데미지가 나가는 걸 막을 수 있다
    public virtual void OnForceCancelled() { }

    // 예고 시간이 끝난 뒤 실제 효과 실행
    public abstract void ExecuteSkill(Transform target);
}

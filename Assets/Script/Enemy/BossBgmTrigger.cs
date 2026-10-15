using UnityEngine;

// 보스 몬스터가 어그로를 끌면(첫 타겟 획득) 보스 전용 BGM으로 전환하고, 보스가 죽으면
// 어그로가 끌리기 직전에 재생 중이던 BGM으로 되돌린다. 보스 몬스터 오브젝트에 붙인다.
[RequireComponent(typeof(AttackBase))]
[RequireComponent(typeof(EnemyHp))]
public class BossBgmTrigger : MonoBehaviour
{
    [Tooltip("AudioManager에 등록된 보스 전용 BGM 키")]
    public string bossBgmKey;
    [Tooltip("보스가 타겟 없이 이 시간(초)이 지나면 전투가 끝난 것으로 보고 원래 BGM으로 되돌린다 " +
             "(파티가 추격 범위 밖으로 도망친 경우). 다시 타겟을 잡으면 보스 BGM으로 재전환")]
    public float resetDelay = 5f;

    private AttackBase _attackBase;
    private EnemyHp _enemyHp;

    private bool _hasAggroed;
    private string _previousBgmKey;
    private float _noTargetTimer;

    void Awake()
    {
        _attackBase = GetComponent<AttackBase>();
        _enemyHp    = GetComponent<EnemyHp>();
    }

    void OnEnable()
    {
        if (_attackBase != null) _attackBase.OnAttackStarted += HandleAggro;
        if (_enemyHp != null)    _enemyHp.OnDied += HandleDeath;
    }

    void OnDisable()
    {
        if (_attackBase != null) _attackBase.OnAttackStarted -= HandleAggro;
        if (_enemyHp != null)    _enemyHp.OnDied -= HandleDeath;
    }

    // OnAttackStarted는 타겟이 파티원 사이에서 바뀔 때도 다시 호출되므로, 이미 어그로 상태면 무시 —
    // 최초로 타겟을 획득한 순간에만 BGM을 전환한다
    private void HandleAggro()
    {
        if (_hasAggroed) return;
        if (string.IsNullOrEmpty(bossBgmKey) || AudioManager.instance == null) return;

        _hasAggroed     = true;
        _noTargetTimer  = 0f;
        _previousBgmKey = AudioManager.instance.CurrentBgmKey;
        AudioManager.instance.PlayBGM(bossBgmKey);
    }

    // 보스가 죽지 않고 전투를 푼 경우(파티가 도망쳐 타겟을 잃음) — 타겟 없는 상태가 resetDelay만큼 이어지면 원래 BGM으로
    void Update()
    {
        if (!_hasAggroed || _attackBase == null) return;
        if (_enemyHp != null && _enemyHp.isDead) return;

        if (_attackBase.currentTarget != null)
        {
            _noTargetTimer = 0f;
            return;
        }

        _noTargetTimer += Time.deltaTime;
        if (_noTargetTimer >= resetDelay)
            RestorePreviousBgm();
    }

    private void HandleDeath() => RestorePreviousBgm();

    private void RestorePreviousBgm()
    {
        if (!_hasAggroed) return;
        _hasAggroed    = false;
        _noTargetTimer = 0f;

        if (AudioManager.instance == null) return;
        if (!string.IsNullOrEmpty(_previousBgmKey))
            AudioManager.instance.PlayBGM(_previousBgmKey);
    }
}

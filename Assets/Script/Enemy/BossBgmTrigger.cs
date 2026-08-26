using UnityEngine;

// 보스 몬스터가 어그로를 끌면(첫 타겟 획득) 보스 전용 BGM으로 전환하고, 보스가 죽으면
// 어그로가 끌리기 직전에 재생 중이던 BGM으로 되돌린다. 보스 몬스터 오브젝트에 붙인다.
[RequireComponent(typeof(AttackBase))]
[RequireComponent(typeof(EnemyHp))]
public class BossBgmTrigger : MonoBehaviour
{
    [Tooltip("AudioManager에 등록된 보스 전용 BGM 키")]
    public string bossBgmKey;

    private AttackBase _attackBase;
    private EnemyHp _enemyHp;

    private bool _hasAggroed;
    private string _previousBgmKey;

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

        _hasAggroed = true;
        _previousBgmKey = AudioManager.instance.CurrentBgmKey;
        AudioManager.instance.PlayBGM(bossBgmKey);
    }

    private void HandleDeath()
    {
        if (!_hasAggroed) return;
        _hasAggroed = false;

        if (AudioManager.instance == null) return;
        if (!string.IsNullOrEmpty(_previousBgmKey))
            AudioManager.instance.PlayBGM(_previousBgmKey);
    }
}

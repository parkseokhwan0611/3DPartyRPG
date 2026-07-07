using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetHpScript : MonoBehaviour
{
    public static TargetHpScript instance;
    [Header("# UI References")]
    public GameObject rootVisual; // 평소엔 꺼두었다가 타겟팅 시 켜짐
    public TMPro.TextMeshProUGUI nameText;
    public TMPro.TextMeshProUGUI hpText;
    public UnityEngine.UI.Image hpBarFill;

    private EnemyHp currentTarget;

    // 리더 추적용 캐시 — 리더가 바뀔 때만 AttackBase를 다시 가져옴
    private PartyMemberScript _trackedLeader;
    private AttackBase        _leaderAttack;
    private Transform         _trackedTargetTransform;

    void Awake() => instance = this;

    // 매 타격이 아니라, 리더가 지금 공격 중인 대상 하나만 추적
    void Update()
    {
        var leader = PartyManager.instance != null ? PartyManager.instance.currentLeader : null;
        if (leader != _trackedLeader)
        {
            _trackedLeader = leader;
            _leaderAttack  = leader != null ? leader.GetComponent<AttackBase>() : null;
        }

        Transform leaderTarget = _leaderAttack != null ? _leaderAttack.currentTarget : null;
        if (leaderTarget == _trackedTargetTransform) return;

        _trackedTargetTransform = leaderTarget;

        EnemyHp enemyHp = leaderTarget != null ? leaderTarget.GetComponent<EnemyHp>() : null;
        if (enemyHp != null) SetTarget(enemyHp);
        else                 ClearTarget();
    }

    public void SetTarget(EnemyHp newTarget)
    {
        // 1. 기존 타겟 이벤트 구독 해제 (중요: 메모리 누수 방지)
        if (currentTarget != null) currentTarget.OnHpChanged -= UpdateHPBar;

        // 2. 새 타겟 설정
        currentTarget = newTarget;
        rootVisual.SetActive(true);
        nameText.text = currentTarget.enemyName;

        // 3. 새 타겟 이벤트 구독 및 초기화
        currentTarget.OnHpChanged += UpdateHPBar;
        UpdateHPBar(currentTarget.hp, currentTarget.maxHp);
    }

    private void ClearTarget()
    {
        if (currentTarget != null) currentTarget.OnHpChanged -= UpdateHPBar;
        currentTarget = null;
        if (rootVisual != null) rootVisual.SetActive(false);
    }

    void UpdateHPBar(float currentHp, float maxHp)
    {
        hpBarFill.fillAmount = currentHp / maxHp;
        if (hpText != null)
            hpText.text = $"{currentHp:F0} / {maxHp:F0}";

        if (currentHp <= 0) rootVisual.SetActive(false);
    }
}

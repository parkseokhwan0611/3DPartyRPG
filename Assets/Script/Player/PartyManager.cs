using UnityEngine;
using System.Collections.Generic;
using Cinemachine;

public class PartyManager : MonoBehaviour
{
    // ─────────────────────────────────────────
    // 파티 구성
    // ─────────────────────────────────────────
    public List<PartyMemberScript> partyMembers = new List<PartyMemberScript>();
    public PartyMemberScript currentLeader;

    // ─────────────────────────────────────────
    // 참조
    // ─────────────────────────────────────────
    [Header("참조")]
    public LayerMask groundLayer;
    public CinemachineVirtualCamera virtualCamera;
    public static PartyManager instance;
    [Header("게임 오버 UI")]
    public GameObject gameOverUI;

    private int enemyLayer;
    private Camera mainCamera;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        instance   = this;
        mainCamera = Camera.main;
        enemyLayer = LayerMask.GetMask("Enemy");
    }
    void Start()
    {
        if (partyMembers.Count > 0) ChangeLeader(0);
    }

    void Update()
    {
        if (partyMembers.Count == 0) return;

        // 인벤토리/메뉴 열려있으면 캐릭터 입력 전부 차단
        if (MenuTabUI.IsOpen) return;

        HandleLeaderChangeInput();
        HandleCommandInput();

        var skillManager = currentLeader.GetComponent<SkillManager>();
        if (skillManager != null) skillManager.HandleKeyInput();
    }

    // ─────────────────────────────────────────────────────────────────
    // 입력 처리 (Input만 담당, 로직은 각 메서드에 위임)
    // ─────────────────────────────────────────────────────────────────

    void HandleLeaderChangeInput()
    {
        // 죽은 캐릭터는 ChangeLeader 내부에서 걸러짐
        if (Input.GetKeyDown(KeyCode.Alpha1)) ChangeLeader(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ChangeLeader(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ChangeLeader(2);
    }

    void HandleCommandInput()
    {
        if (currentLeader == null) return;
        if (!Input.GetMouseButtonDown(1)) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // 우선순위 1: 적 클릭 → 공격 명령
        if (TryGetEnemyHit(ray, out RaycastHit enemyHit))
        {
            DispatchAttackCommand(enemyHit);
            return;
        }

        // 우선순위 2: 땅 클릭 → 이동 명령
        if (TryGetGroundHit(ray, out RaycastHit groundHit))
        {
            DispatchMoveCommand(groundHit.point);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 레이캐스트 (결과만 반환, 사이드이펙트 없음)
    // ─────────────────────────────────────────────────────────────────

    bool TryGetEnemyHit(Ray ray, out RaycastHit hit)
    {
        return Physics.Raycast(ray, out hit, Mathf.Infinity, enemyLayer);
    }

    bool TryGetGroundHit(Ray ray, out RaycastHit hit)
    {
        return Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer);
    }

    // ─────────────────────────────────────────────────────────────────
    // 명령 디스패치 (실제 로직 실행)
    // ─────────────────────────────────────────────────────────────────

    void DispatchAttackCommand(RaycastHit hit)
    {
        foreach (var member in partyMembers)
        {
            var attack = member.GetComponent<AttackBase>();
            if (attack != null) attack.SetTarget(hit.transform);
        }

        SpawnMarker("AttackMarker", hit.point);
    }

    void DispatchMoveCommand(Vector3 destination)
    {
        // 모든 멤버의 공격 타겟 해제 및 정지 거리 설정
        foreach (var member in partyMembers)
        {
            var attack = member.GetComponent<AttackBase>();
            if (attack != null) attack.SetTarget(null);

            member.agent.stoppingDistance = (member == currentLeader)
                ? 0.1f
                : member.stopDistance;
        }

        // 리더에게만 목적지 설정 (팔로워는 PartyMemberScript가 자체적으로 따라옴)
        currentLeader.agent.SetDestination(destination);

        SpawnMarker("MoveMarker", destination);
    }

    // ─────────────────────────────────────────────────────────────────
    // 리더 변경
    // ─────────────────────────────────────────────────────────────────

    public void ChangeLeader(int index)
    {
        if (index < 0 || index >= partyMembers.Count) return;

        PartyMemberScript newLeader = partyMembers[index];

        // 죽은 캐릭터로는 교체 불가
        if (newLeader.CurrentState == PartyMemberScript.MemberState.Dead) return;

        currentLeader = newLeader;

        // 카메라 타겟 변경
        if (virtualCamera != null)
        {
            virtualCamera.Follow = newLeader.transform;
            virtualCamera.LookAt = newLeader.transform;
        }

        // 죽은 멤버 제외하고 체인 순서 생성
        List<PartyMemberScript> newOrder = new List<PartyMemberScript> { newLeader };
        foreach (var member in partyMembers)
        {
            if (member != newLeader && member.CurrentState != PartyMemberScript.MemberState.Dead)
                newOrder.Add(member);
        }

        // 생존 멤버에게만 새 순서 통보
        foreach (var member in newOrder)
            member.UpdateChainOrder(newOrder);
    }

    // ─────────────────────────────────────────────────────────────────
    // 사망 처리 (CharacterStat에서 호출)
    // ─────────────────────────────────────────────────────────────────

    public void OnMemberDied(PartyMemberScript deadMember)
    {
        // 전원 사망 체크
        bool allDead = partyMembers.TrueForAll(
            m => m.CurrentState == PartyMemberScript.MemberState.Dead);

        if (allDead)
        {
            TriggerGameOver(deadMember.deathHideDelay);
            return;
        }

        // 죽은 멤버가 리더였으면 → 가장 낮은 인덱스의 생존자로 교체, 아니면 체인 재건
        if (deadMember == currentLeader)
        {
            int idx = partyMembers.FindIndex(
                m => m.CurrentState != PartyMemberScript.MemberState.Dead);
            if (idx >= 0) ChangeLeader(idx);
        }
        else
        {
            // 팔로워 사망 → 현재 리더 기준으로 체인 재건 (ChangeLeader가 dead 제외 처리)
            RebuildChain();
        }
    }

    private void RebuildChain()
    {
        if (currentLeader == null) return;
        ChangeLeader(partyMembers.IndexOf(currentLeader));
    }

    private void TriggerGameOver(float delay)
    {
        if (gameOverUI != null)
            StartCoroutine(ShowGameOverAfterDelay(delay));
        // gameOverUI 미연결 시 아무것도 하지 않음
    }

    private System.Collections.IEnumerator ShowGameOverAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameOverUI.SetActive(true);
        Time.timeScale = 0f;
    }

    // ─────────────────────────────────────────────────────────────────
    // 마커 스폰 (공통 유틸)
    // ─────────────────────────────────────────────────────────────────

    void SpawnMarker(string poolKey, Vector3 position)
    {
        if (ObjectPoolManager.instance == null) return;

        var marker = ObjectPoolManager.instance.GetGo(poolKey);
        if (marker != null)
        {
            marker.transform.position = position;
            marker.transform.rotation = Quaternion.identity;
        }
    }
}
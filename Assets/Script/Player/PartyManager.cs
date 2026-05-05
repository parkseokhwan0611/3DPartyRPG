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
    public static PartyManager instance; // 선언

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        instance = this; // 초기화
    }
    void Start()
    {
        if (partyMembers.Count > 0) ChangeLeader(0);
    }

    void Update()
    {
        if (partyMembers.Count == 0) return;

        HandleLeaderChangeInput();
        HandleCommandInput();
    }

    // ─────────────────────────────────────────────────────────────────
    // 입력 처리 (Input만 담당, 로직은 각 메서드에 위임)
    // ─────────────────────────────────────────────────────────────────

    void HandleLeaderChangeInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) ChangeLeader(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ChangeLeader(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ChangeLeader(2);
    }

    void HandleCommandInput()
    {
        if (currentLeader == null) return;
        if (!Input.GetMouseButtonDown(1)) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

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
        return Physics.Raycast(ray, out hit, Mathf.Infinity, LayerMask.GetMask("Enemy"));
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
        currentLeader = newLeader;

        // 카메라 타겟 변경
        if (virtualCamera != null)
        {
            virtualCamera.Follow = newLeader.transform;
            virtualCamera.LookAt = newLeader.transform;
        }

        // 새 체인 순서 생성: 선택된 리더를 맨 앞으로
        List<PartyMemberScript> newOrder = new List<PartyMemberScript> { newLeader };
        foreach (var member in partyMembers)
        {
            if (member != newLeader) newOrder.Add(member);
        }

        // 모든 멤버에게 새 순서 통보
        foreach (var member in partyMembers)
        {
            member.UpdateChainOrder(newOrder);
        }
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
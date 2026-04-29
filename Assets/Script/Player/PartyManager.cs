using UnityEngine;
using System.Collections.Generic;

public class PartyManager : MonoBehaviour
{
    public List<PartyMemberScript> partyMembers = new List<PartyMemberScript>();
    public PartyMemberScript currentLeader;
    public LayerMask groundLayer;

    void Start()
    {
        // 시작 시 첫 번째 멤버를 리더로 설정
        if (partyMembers.Count > 0) ChangeLeader(0);
    }

    void Update()
    {
        if (partyMembers.Count == 0) return;

        // 1. 리더 변경 입력 (F1, F2, F3)
        if (Input.GetKeyDown(KeyCode.F1)) ChangeLeader(0);
        if (Input.GetKeyDown(KeyCode.F2)) ChangeLeader(1);
        if (Input.GetKeyDown(KeyCode.F3)) ChangeLeader(2);

        // 2. 리더 이동 명령 (우클릭)
        if (currentLeader != null && Input.GetMouseButtonDown(1))
        {
            HandleCommand();
        }
    }

    // 인덱스 번호로 리더를 교체하는 함수
    public void ChangeLeader(int index)
    {
        if (index < 0 || index >= partyMembers.Count) return;
        
        PartyMemberScript newLeader = partyMembers[index];
        currentLeader = newLeader; // 현재 리더 갱신

        // 새로운 체인 순서 리스트 생성
        List<PartyMemberScript> newOrder = new List<PartyMemberScript>();
        
        // 선택된 리더를 0순위(맨 앞)로
        newOrder.Add(newLeader);
        
        // 나머지 멤버들을 원래 partyMembers 순서대로 추가
        foreach (var member in partyMembers)
        {
            if (member != newLeader)
            {
                newOrder.Add(member);
            }
        }

        // 모든 멤버에게 새로운 체인 순서 통보
        foreach (var member in partyMembers)
        {
            member.UpdateChainOrder(newOrder);
        }
    }

    void HandleCommand()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        // 변수 하나만 선언해서 돌려쓰는 것이 가장 안전합니다.
        RaycastHit hit;

        // 1. 적을 클릭했는지 확인 (공격 명령)
        // LayerMask.GetMask를 사용하면 더 정확합니다.
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, LayerMask.GetMask("Enemy")))
        {
            foreach (var member in partyMembers)
            {
                var attack = member.GetComponent<AttackBase>();
                if (attack != null) attack.SetTarget(hit.transform);
            }
            SpawnAttackMarker(hit.point);
        }
        // 2. 땅을 클릭했는지 확인 (이동 명령)
        else if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer))
        {
            foreach (var member in partyMembers)
            {
                var attack = member.GetComponent<AttackBase>();
                if (attack != null) 
                {
                    attack.SetTarget(null); // 타겟 해제
                }

                // [중요] 리더와 멤버의 정지 거리 분리
                if (member == currentLeader)
                {
                    member.agent.stoppingDistance = 0.1f; // 리더는 목적지 끝까지
                }
                else
                {
                    member.agent.stoppingDistance = member.stopDistance; // 멤버는 3.0 유지
                }
            }

            // 목적지 설정
            currentLeader.agent.SetDestination(hit.point);
            SpawnMoveMarker(hit.point);
        }
    }

    void SpawnMoveMarker(Vector3 spawnPosition)
    {
        // 기존에 사용하시던 오브젝트 풀 매니저 호출
        // 프리팹 이름은 "MoveMarker"라고 가정합니다.
        var markerGo = ObjectPoolManager.instance.GetGo("MoveMarker");
        
        if (markerGo != null)
        {
            markerGo.transform.position = spawnPosition;
            
            // 바닥에 붙는 마커이므로 회전값은 기본값(Identity) 혹은 
            // 바닥의 노멀값에 맞게 설정 (보통은 아래처럼 기본 회전 사용)
            markerGo.transform.rotation = Quaternion.identity;
        }
    }
    void SpawnAttackMarker(Vector3 spawnPosition)
    {
        // 기존에 사용하시던 오브젝트 풀 매니저 호출
        // 프리팹 이름은 "MoveMarker"라고 가정합니다.
        var markerGo = ObjectPoolManager.instance.GetGo("AttackMarker");
        
        if (markerGo != null)
        {
            markerGo.transform.position = spawnPosition;
            
            // 바닥에 붙는 마커이므로 회전값은 기본값(Identity) 혹은 
            // 바닥의 노멀값에 맞게 설정 (보통은 아래처럼 기본 회전 사용)
            markerGo.transform.rotation = Quaternion.identity;
        }
    }
}
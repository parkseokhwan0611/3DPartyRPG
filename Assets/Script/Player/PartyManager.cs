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
    public CameraFollowTarget cameraFollowTarget;
    public static PartyManager instance;
    [Header("게임 오버 UI")]
    public GameObject gameOverUI;

    private int enemyLayer;
    private Camera mainCamera;
    private Vector3 _lastMoveDestination;
    private bool _hasPendingDestination = false;

    // 리더가 바뀔 때 발생 — 매 프레임 폴링 대신 이 이벤트를 구독해서 갱신할 것
    public event System.Action<PartyMemberScript> OnLeaderChanged;

    // ─────────────────────────────────────────
    // 자동 스킬 사용 (팔로워 전용, T로 토글)
    // ─────────────────────────────────────────
    public bool AutoSkillEnabled { get; private set; } = true;
    public event System.Action<bool> OnAutoSkillToggled;

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

        // 인벤토리/메뉴/상점/대화 열려있으면 캐릭터 입력 전부 차단
        if (MenuTabUI.IsOpen || ShopUI.IsOpen || DialogueUI.IsOpen) return;

        HandleLeaderChangeInput();
        HandleCommandInput();

        if (Input.GetKeyDown(KeyCode.T))
            ToggleAutoSkill();

        if (currentLeader == null || currentLeader.CurrentState == PartyMemberScript.MemberState.Dead) return;

        var skillManager = currentLeader.GetComponent<SkillManager>();
        if (skillManager != null) skillManager.HandleKeyInput();
    }

    // ─────────────────────────────────────────────────────────────────
    // 입력 처리 (Input만 담당, 로직은 각 메서드에 위임)
    // ─────────────────────────────────────────────────────────────────

    void HandleLeaderChangeInput()
    {
        // 죽은 캐릭터는 ChangeLeader 내부에서 걸러짐
        if (Input.GetKeyDown(KeyCode.A)) ChangeLeader(0);
        if (Input.GetKeyDown(KeyCode.S)) ChangeLeader(1);
        if (Input.GetKeyDown(KeyCode.D)) ChangeLeader(2);
    }

    void HandleCommandInput()
    {
        if (currentLeader == null) return;
        if (currentLeader.CurrentState == PartyMemberScript.MemberState.Dead) return;
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
        _hasPendingDestination = false; // 공격 명령이 이동 목적지를 덮어씀
        foreach (var member in partyMembers)
        {
            if (member.CurrentState == PartyMemberScript.MemberState.Dead) continue;
            var attack = member.GetComponent<AttackBase>();
            if (attack != null) attack.SetTarget(hit.transform);
        }

        SpawnMarker("AttackMarker", hit.point);
    }

    void DispatchMoveCommand(Vector3 destination)
    {
        _lastMoveDestination = destination;
        _hasPendingDestination = true;
        foreach (var member in partyMembers)
        {
            if (member.CurrentState == PartyMemberScript.MemberState.Dead) continue;
            var attack = member.GetComponent<AttackBase>();
            if (attack != null) attack.SetTarget(null);

            member.agent.stoppingDistance = (member == currentLeader)
                ? 0.1f
                : member.stopDistance;
        }

        // 리더에게만 목적지 설정 (팔로워는 PartyMemberScript가 자체적으로 따라옴)
        // 목적지가 자기 반경 안쪽처럼 너무 가까우면 SetDestination 대신 즉시 정지 —
        // NavMeshAgent가 회피 벡터를 반복 재계산하며 제자리에서 빙빙 도는 현상 방지
        float distToLeader = Vector3.Distance(currentLeader.transform.position, destination);
        float minMoveDist  = currentLeader.agent.radius + 0.1f;

        bool blocked = distToLeader <= minMoveDist;

        // 목적지가 다른 파티원(팔로워)의 반경 안쪽인 경우도 동일하게 처리 —
        // 그 자리에 실제로 들어갈 수 없어 서로 회피 벡터만 계속 재계산하며 도는 것을 방지
        if (!blocked)
        {
            foreach (var member in partyMembers)
            {
                if (member == currentLeader) continue;
                if (member.CurrentState == PartyMemberScript.MemberState.Dead) continue;

                float blockDist = currentLeader.agent.radius + member.agent.radius + 0.1f;
                if ((member.transform.position - destination).sqrMagnitude <= blockDist * blockDist)
                {
                    blocked = true;
                    break;
                }
            }
        }

        if (blocked)
        {
            currentLeader.agent.ResetPath();
            currentLeader.agent.velocity = Vector3.zero;
        }
        else
        {
            currentLeader.agent.SetDestination(destination);
        }

        SpawnMarker("MoveMarker", destination);
    }

    // ─────────────────────────────────────────────────────────────────
    // 자동 스킬 토글
    // ─────────────────────────────────────────────────────────────────

    public void ToggleAutoSkill()
    {
        AutoSkillEnabled = !AutoSkillEnabled;
        OnAutoSkillToggled?.Invoke(AutoSkillEnabled);
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

        // 실제로 다른 리더로 바뀔 때만 사운드 재생 (게임 시작 시 최초 지정, 팔로워 사망에 따른
        // 체인 재구성 시 재호출되는 경우는 리더가 그대로라 제외)
        if (currentLeader != null && currentLeader != newLeader)
            AudioManager.instance?.PlaySFX("LeaderChange");

        currentLeader = newLeader;
        OnLeaderChanged?.Invoke(newLeader);

        // 카메라 타겟 변경
        if (cameraFollowTarget != null)
        {
            cameraFollowTarget.SetTarget(newLeader.transform);
            if (virtualCamera != null)
            {
                virtualCamera.Follow = cameraFollowTarget.transform;
                virtualCamera.LookAt = cameraFollowTarget.transform;
            }
        }
        else if (virtualCamera != null)
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

        // 이동 목적지가 있고 새 리더가 아직 도착 전이면 목적지 이어받기
        if (_hasPendingDestination)
        {
            float distSqr      = (newLeader.transform.position - _lastMoveDestination).sqrMagnitude;
            float stopThresh   = newLeader.agent.stoppingDistance + 0.5f;
            if (distSqr > stopThresh * stopThresh)
                newLeader.agent.SetDestination(_lastMoveDestination);
            else
                _hasPendingDestination = false; // 이미 도착 → 기록 초기화
        }
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
        yield return new WaitForSecondsRealtime(delay);
        // yield 도중 gameOverUI가 파괴됐을 경우를 대비해 재확인
        if (gameOverUI == null) yield break;
        gameOverUI.SetActive(true);
        AudioManager.instance?.PlaySFX("GameOver");
        Time.timeScale = 0f;
    }

    /// <summary>게임오버 UI에서 재시작/타이틀 복귀 시 호출 — timeScale 복구</summary>
    public void ResumeGame()
    {
        Time.timeScale = 1f;
        if (gameOverUI != null) gameOverUI.SetActive(false);
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
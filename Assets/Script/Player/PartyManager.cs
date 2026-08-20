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
    // 게임오버 화면이 떠있는 동안인지 — MenuTabUI 등에서 메뉴 열림을 차단하는 데 사용
    public static bool IsGameOver { get; private set; }
    [Header("게임 오버 UI")]
    public GameObject gameOverUI;
    [Tooltip("게임오버 화면의 '타이틀로' 버튼이 이동할 씬 이름")]
    public string titleSceneName = "Title";
    [Header("레벨업 이펙트")]
    [Tooltip("ObjectPoolManager에 등록한 레벨업 이펙트 풀 키. 파티가 레벨업하면 현재 리더 위치에 재생")]
    public string levelUpEffectPoolKey = "LevelUpEffect";

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

    // ─────────────────────────────────────────
    // 파티원 고정 (스페이스바로 토글) — 켜져 있으면 리더만 이동하고 팔로워는 제자리에 고정.
    // 단, 공격 명령(적 클릭)은 고정 중에도 그대로 전달돼서 팔로워도 새 타겟을 공격하러 간다
    // ─────────────────────────────────────────
    public bool PartyHoldEnabled { get; private set; } = false;
    public event System.Action<bool> OnPartyHoldToggled;

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
        if (partyMembers.Count == 0) return;

        // PartyManager는 씬 로컬이라 씬이 바뀌면 항상 0번으로 리더가 초기화되므로,
        // DataManager(DontDestroyOnLoad)에 남아있는 이전 리더 인덱스를 이어받는다
        int startIndex = DataManager.instance != null ? DataManager.instance.currentLeaderIndex : 0;
        if (startIndex < 0 || startIndex >= partyMembers.Count
            || partyMembers[startIndex].CurrentState == PartyMemberScript.MemberState.Dead)
        {
            startIndex = partyMembers.FindIndex(m => m.CurrentState != PartyMemberScript.MemberState.Dead);
        }

        if (startIndex >= 0) ChangeLeader(startIndex);

        if (DataManager.instance != null)
            DataManager.instance.OnLevelUp += PlayLevelUpEffect;
    }

    void OnDestroy()
    {
        if (DataManager.instance != null)
            DataManager.instance.OnLevelUp -= PlayLevelUpEffect;

        // 씬 로컬이라 게임오버 화면이 뜬 채로 씬이 바뀌어도 다음 씬에서 메뉴가 막혀있지 않도록 초기화
        IsGameOver = false;
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

        if (Input.GetKeyDown(KeyCode.Space))
            TogglePartyHold();

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
        if (Input.GetKeyDown(KeyCode.Alpha1)) ChangeLeader(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ChangeLeader(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ChangeLeader(2);
    }

    void HandleCommandInput()
    {
        if (currentLeader == null) return;
        if (currentLeader.CurrentState == PartyMemberScript.MemberState.Dead) return;

        // 스킬 시전 중(버프 등)에는 이동/공격 명령 자체를 받지 않음 — 기본 공격과 동일하게
        // 이펙트가 끝날 때까지(SkillBase.SkillRoutine이 IsCastingSkill을 풀어줄 때까지) 묶어둠
        AttackBase leaderAttack = currentLeader.GetComponent<AttackBase>();
        if (leaderAttack != null && leaderAttack.IsCastingSkill) return;

        // 스턴 중에는 이동/공격 명령 자체를 받지 않음 — 실행이 안 되는 명령을 굳이 받아서
        // 마커를 찍거나 타겟을 갱신할 필요 없음
        var leaderStatus = currentLeader.GetComponent<PartyStatusEffectHandler>();
        if (leaderStatus != null && leaderStatus.HasDebuff(StatusEffectType.Stun)) return;

        // 롤 스타일 A+좌클릭 강제 공격 — 공격 가능한 오브젝트 위에서만 발동
        if (Input.GetKey(KeyCode.A) && Input.GetMouseButtonDown(0))
        {
            Ray attackRay = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (TryGetEnemyHit(attackRay, out RaycastHit forcedHit))
                DispatchAttackCommand(forcedHit);
            return;
        }

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

        // hit.point는 몬스터 콜라이더 표면(몸통 등)에서 맞은 지점이라 지면보다 이미 떠있을 수 있음 —
        // 마커는 타겟의 발밑(루트 위치) 기준으로 찍음
        Vector3 markerPos = hit.transform != null ? hit.transform.position : hit.point;
        SpawnMarker("AttackMarker", markerPos);
    }

    void DispatchMoveCommand(Vector3 destination)
    {
        _lastMoveDestination = destination;
        _hasPendingDestination = true;

        var leaderAttack = currentLeader.GetComponent<AttackBase>();
        if (leaderAttack != null) leaderAttack.SetTarget(null);
        currentLeader.agent.stoppingDistance = 0.1f;

        // 파티원 고정 중엔 팔로워의 타겟/정지거리를 건드리지 않는다 — 하던 공격은 그 자리에서
        // 계속하고, 이동 명령은 리더에게만 적용된다
        if (!PartyHoldEnabled)
        {
            foreach (var member in partyMembers)
            {
                if (member == currentLeader) continue;
                if (member.CurrentState == PartyMemberScript.MemberState.Dead) continue;
                var attack = member.GetComponent<AttackBase>();
                if (attack != null) attack.SetTarget(null);
                member.agent.stoppingDistance = member.stopDistance;
            }
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
        AudioManager.instance?.PlaySFX("AutoSkillToggle");
        OnAutoSkillToggled?.Invoke(AutoSkillEnabled);
    }

    // ─────────────────────────────────────────────────────────────────
    // 파티원 고정 토글
    // ─────────────────────────────────────────────────────────────────

    public void TogglePartyHold()
    {
        PartyHoldEnabled = !PartyHoldEnabled;

        if (PartyHoldEnabled)
        {
            // 켜지는 순간 팔로워를 즉시 제자리에 멈춘다 — 공격 중인 팔로워는 하던 공격을 그대로
            // 계속하게 두고(AttackBase가 별도로 관리), 이동 중이던 팔로워만 멈춰 세운다
            foreach (var member in partyMembers)
            {
                if (member == currentLeader) continue;
                if (member.CurrentState == PartyMemberScript.MemberState.Dead) continue;
                if (member.CurrentState == PartyMemberScript.MemberState.Attacking) continue;

                if (member.agent.enabled)
                {
                    member.agent.ResetPath();
                    member.agent.velocity = Vector3.zero;
                }
                member.ChangeState(PartyMemberScript.MemberState.Idle);
            }
        }

        AudioManager.instance?.PlaySFX("AutoSkillToggle");
        OnPartyHoldToggled?.Invoke(PartyHoldEnabled);
    }

    // ─────────────────────────────────────────────────────────────────
    // 카메라 순간이동 (세이브 로드 등)
    // ─────────────────────────────────────────────────────────────────

    // 캐릭터 위치만 Warp하고 카메라는 그대로 두면, 카메라가 원래 있던 곳에서 로드된
    // 캐릭터 위치까지 SmoothDamp로 서서히 따라오면서 부자연스럽게 화면이 흐른다.
    // 카메라 리그(cameraFollowTarget)도 즉시 옮기고, Cinemachine에도 "타겟이 순간이동했다"고
    // 알려줘야 Transposer의 자체 데미핑으로 인한 잔여 스무스 이동까지 막을 수 있다.
    public void SnapCameraToPosition(Vector3 position)
    {
        if (cameraFollowTarget == null) return;

        Vector3 biasedPosition = cameraFollowTarget.ApplySouthBias(position);
        Vector3 delta = biasedPosition - cameraFollowTarget.transform.position;
        cameraFollowTarget.WarpTo(biasedPosition);

        if (virtualCamera != null)
            virtualCamera.OnTargetObjectWarped(cameraFollowTarget.transform, delta);
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

        // 실제로 다른 리더로 바뀌는지 — 게임 시작 시 최초 지정은 true, 팔로워 사망에 따른
        // 체인 재구성(RebuildChain)에서 재호출되는 경우는 리더가 그대로라 false
        bool leaderActuallyChanged = currentLeader != newLeader;

        if (currentLeader != null && leaderActuallyChanged)
            AudioManager.instance?.PlaySFX("LeaderChange");

        currentLeader = newLeader;

        // 씬 전환 후에도 리더가 유지되도록 DataManager에도 실시간 반영
        if (DataManager.instance != null)
            DataManager.instance.currentLeaderIndex = index;

        if (leaderActuallyChanged)
        {
            OnLeaderChanged?.Invoke(newLeader);

            // 카메라 타겟 변경 — SetTarget은 항상 WarpTo(즉시 스냅)를 호출하므로, 리더가
            // 실제로 바뀔 때만 실행해야 함. 그렇지 않으면 팔로워가 죽을 때마다(RebuildChain)
            // 카메라가 리더 위치로 매번 순간이동하는 시각적 결함이 생김.
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
        IsGameOver = true;
        // 페이드(fade:true)는 Time.deltaTime 기반 코루틴인데 바로 다음 줄에서 timeScale을 0으로
        // 멈춰버리면 페이드가 끝까지 못 가고 볼륨이 어중간하게 멈춘 채 계속 재생됨 — 즉시 정지로 처리
        AudioManager.instance?.StopBGM(fade: false);
        AudioManager.instance?.PlaySFX("GameOver");
        Time.timeScale = 0f;
    }

    /// <summary>게임오버 UI에서 재시작/타이틀 복귀 시 호출 — timeScale 복구</summary>
    public void ResumeGame()
    {
        Time.timeScale = 1f;
        IsGameOver = false;
        if (gameOverUI != null) gameOverUI.SetActive(false);
    }

    /// <summary>게임오버 UI의 '타이틀로' 버튼에서 호출 — timeScale 복구 후 타이틀 씬으로 이동</summary>
    public void GoToTitle()
    {
        Time.timeScale = 1f;
        IsGameOver = false;
        SceneLoader.Load(titleSceneName);
    }

    // ─────────────────────────────────────────────────────────────────
    // 레벨업 이펙트
    // ─────────────────────────────────────────────────────────────────

    // 파티 레벨업 시 현재 리더 위치에 이펙트 재생 — 풀에서 꺼낸 이펙트의 자동 반환은
    // 프리팹 자체가 처리 (GrenadeProjectile의 폭발 VFX와 동일한 방식)
    private void PlayLevelUpEffect()
    {
        if (currentLeader == null || ObjectPoolManager.instance == null) return;
        if (string.IsNullOrEmpty(levelUpEffectPoolKey)) return;

        var effect = ObjectPoolManager.instance.GetGo(levelUpEffectPoolKey);
        if (effect != null)
            effect.transform.SetPositionAndRotation(currentLeader.transform.position, Quaternion.identity);
    }

    // ─────────────────────────────────────────────────────────────────
    // 마커 스폰 (공통 유틸)
    // ─────────────────────────────────────────────────────────────────

    // 마커는 항상 Y=0 평면에 고정 (지형 높이·오프셋과 무관)
    void SpawnMarker(string poolKey, Vector3 position)
    {
        if (ObjectPoolManager.instance == null) return;

        var marker = ObjectPoolManager.instance.GetGo(poolKey);
        if (marker != null)
        {
            marker.transform.position = new Vector3(position.x, 0f, position.z);
            marker.transform.rotation = Quaternion.identity;
        }
    }
}
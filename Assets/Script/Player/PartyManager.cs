using UnityEngine;
using UnityEngine.AI;
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
    [Tooltip("이동 클릭 지점과 가장 가까운 NavMesh 지점의 수평 거리 허용치(m). 벽 쪽 바닥은 NavMesh가 " +
             "에이전트 반경만큼 깎여 있어서, 이보다 작으면 벽 근처·좁은 통로 클릭이 무시됨")]
    public float maxWalkableSnapHorizontal = 0.6f;
    [Tooltip("이동 클릭 지점과 NavMesh의 높이 차 허용치(m). NavMesh는 실제 바닥보다 수십 cm 뜨거나 묻힐 수 있어 " +
             "(경사·울퉁불퉁한 바닥) 너무 작으면 정상 바닥 클릭이 무시되고, 너무 크면 바위 등 장애물 위를 " +
             "클릭했을 때 그 가장자리로 '근처 이동'해버림 — 걸어서 오를 수 없는 높이(Step Height)보다 조금 크게")]
    public float maxWalkableSnapHeight = 0.5f;
    [Tooltip("NPC 대화가 끝난 뒤 파티원 고정(Space) 키를 잠그는 시간(초) — 대사를 넘긴 스페이스바가 고정 키로 처리되는 것 방지")]
    public float partyHoldLockAfterDialogue = 0.3f;
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
    private int _enemyOccluderMask; // 적 클릭 판정 시 적을 가리는 것으로 볼 레이어 (바닥 + Default/Obstacle/Tree)
    private Camera mainCamera;
    private Vector3 _lastMoveDestination;
    private bool _hasPendingDestination = false;

    // 리더가 바뀔 때 발생 — 매 프레임 폴링 대신 이 이벤트를 구독해서 갱신할 것
    public event System.Action<PartyMemberScript> OnLeaderChanged;

    // ─────────────────────────────────────────
    // 자동 스킬 사용 (팔로워 전용, T로 토글)
    // ─────────────────────────────────────────
    // 실제 값은 DataManager가 보관 — 씬 이동·이어하기 후에도 유지 (DataManager가 없는 단독 테스트용으로만 로컬 값 사용)
    public bool AutoSkillEnabled
    {
        get => DataManager.instance != null ? DataManager.instance.autoSkillEnabled : _autoSkillLocal;
        private set { if (DataManager.instance != null) DataManager.instance.autoSkillEnabled = value; else _autoSkillLocal = value; }
    }
    private bool _autoSkillLocal = true;
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
        _enemyOccluderMask = groundLayer | LayerMask.GetMask("Default", "Obstacle", "Tree");
    }
    void Start()
    {
        if (partyMembers.Count == 0) return;

        // 이전 씬에서 쓰러진 채(체력 0) 넘어온 파티원은 리더·체인에서 빼고 숨긴다 (부활 수단이 생기기 전까지 유지).
        // CharacterStat은 아직 바인딩 전일 수 있으므로 DataManager의 상태를 직접 확인
        MarkMembersDeadFromData();

        // PartyManager는 씬 로컬이라 씬이 바뀌면 항상 0번으로 리더가 초기화되므로,
        // DataManager(DontDestroyOnLoad)에 남아있는 이전 리더 인덱스를 이어받는다
        int startIndex = DataManager.instance != null ? DataManager.instance.currentLeaderIndex : 0;
        if (startIndex < 0 || startIndex >= partyMembers.Count
            || partyMembers[startIndex].CurrentState == PartyMemberScript.MemberState.Dead)
        {
            startIndex = partyMembers.FindIndex(m => m.CurrentState != PartyMemberScript.MemberState.Dead);
        }

        if (startIndex >= 0) ChangeLeader(startIndex);
        else
        {
            // 전원 사망 상태로 저장된 세이브(저장 막기 이전 버전) — 조작할 캐릭터가 없어 갇히므로 바로 게임오버
            TriggerGameOver(0f);
            return;
        }

        // 방금 포탈을 타고 이 씬에 들어왔다면, 그 포탈이 지정한 스폰 포탈 위치로 파티 전체를 이동.
        // 카메라도 바로 옮긴다 — 안 그러면 씬에 처음 배치된 리더 위치에서 도착 지점까지 화면이 미끄러지듯 따라감
        if (Portal.TryConsumePendingSpawn(out Vector3 spawnPos))
        {
            RepositionPartyAt(spawnPos);
            if (currentLeader != null) SnapCameraToPosition(currentLeader.transform.position);
        }

        if (DataManager.instance != null)
            DataManager.instance.OnLevelUp += PlayLevelUpEffect;
    }

    private void MarkMembersDeadFromData()
    {
        if (DataManager.instance == null) return;
        var statuses = DataManager.instance.partyStatuses;

        var dead = new List<PartyMemberScript>();
        foreach (var member in partyMembers)
        {
            if (member == null || member.StatComp == null) continue;
            int idx = member.StatComp.partyIndex;
            if (idx < 0 || idx >= statuses.Count || statuses[idx].currentHp > 0f) continue;

            member.MarkDeadOnSceneStart();
            dead.Add(member);
        }

        if (dead.Count > 0) StartCoroutine(HideNextFrame(dead));
    }

    // 다른 컴포넌트들의 Start(데이터 바인딩·UI 구독)가 끝난 다음 프레임에 숨긴다
    private System.Collections.IEnumerator HideNextFrame(List<PartyMemberScript> members)
    {
        yield return null;
        foreach (var m in members)
            if (m != null && m.CurrentState == PartyMemberScript.MemberState.Dead) m.gameObject.SetActive(false);
    }

    // 파티 전체를 targetPos로 이동시키되, 씬에 원래 배치돼 있던 파티원 간 상대 위치(포메이션)는
    // 그대로 유지한다 — 리더 기준 오프셋을 구해서 그대로 옮기는 방식
    private void RepositionPartyAt(Vector3 targetPos)
    {
        if (currentLeader == null) return;

        Vector3 leaderOriginalPos = currentLeader.transform.position;

        foreach (var member in partyMembers)
        {
            if (member == null) continue;

            Vector3 offset = member.transform.position - leaderOriginalPos;
            Vector3 newPos = targetPos + offset;

            // 대열 자리가 벽 너머 등 NavMesh 밖이면 Warp가 실패해 씬에 처음 배치된 자리에 남았다가 맵을 가로질러
            // 달려왔음 — 가까운 NavMesh 위로, 그것도 없으면 도착 지점으로 보정
            if (NavMesh.SamplePosition(newPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                newPos = hit.position;
            else if (NavMesh.SamplePosition(targetPos, out hit, 2f, NavMesh.AllAreas))
                newPos = hit.position;

            if (member.agent != null && member.agent.enabled)
                member.agent.Warp(newPos);
            else
                member.transform.position = newPos;
        }
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

        // 인벤토리/메뉴/상점/대화/강화 창이 열려있으면 캐릭터 입력 전부 차단 (NPC·포탈·물약 단축키와 같은 기준)
        // 로딩 화면 중(포탈 이동 등)에도 이전 씬에서 이동·스킬 입력이 들어가지 않게 차단
        if (MenuTabUI.IsOpen || ShopUI.IsOpen || DialogueUI.IsOpen || EnhancementUI.IsOpen || SceneLoader.IsLoading) return;

        HandleLeaderChangeInput();
        HandleCommandInput();

        if (Input.GetKeyDown(KeyCode.T))
            ToggleAutoSkill();

        // 대사를 스페이스바로 넘겨 대화가 끝난 직후엔 잠깐 잠금 — 같은 입력이 고정 키로도 처리돼 바로 켜지던 문제
        if (Input.GetKeyDown(KeyCode.Space) && Time.unscaledTime - DialogueUI.LastClosedTime > partyHoldLockAfterDialogue)
            TogglePartyHold();

        if (currentLeader == null || currentLeader.CurrentState == PartyMemberScript.MemberState.Dead) return;

        var skillManager = currentLeader.SkillComp; // 캐시된 참조 (매 프레임 GetComponent 방지)
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
        AttackBase leaderAttack = currentLeader.AttackComp;
        if (leaderAttack != null && leaderAttack.IsCastingSkill) return;

        // 스턴 중에는 이동/공격 명령 자체를 받지 않음 — 실행이 안 되는 명령을 굳이 받아서
        // 마커를 찍거나 타겟을 갱신할 필요 없음
        var leaderStatus = currentLeader.StatusHandler;
        if (leaderStatus != null && leaderStatus.HasDebuff(StatusEffectType.Stun)) return;

        // 롤 스타일 A+좌클릭 강제 공격 — 공격 가능한 오브젝트 위에서만 발동
        if (Input.GetKey(KeyCode.A) && Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverScreenUI()) return; // 퀵슬롯·미니맵 등 UI 위 클릭은 월드 명령으로 보내지 않음
            Ray attackRay = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (TryGetEnemyHit(attackRay, out RaycastHit forcedHit))
                DispatchAttackCommand(forcedHit);
            return;
        }

        if (!Input.GetMouseButtonDown(1)) return;
        if (IsPointerOverScreenUI()) return;

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
            // 바위 등 장애물 위(NavMesh가 없는 지점)를 클릭하면 명령 자체를 무시 —
            // 그냥 보내면 NavMeshAgent가 장애물 근처까지 걸어가 버려서 "이동 불가"가 아니라
            // "근처까지 감"처럼 보이는 문제가 있었음
            if (!TryGetWalkablePoint(groundHit.point, out Vector3 walkablePoint)) return;
            DispatchMoveCommand(walkablePoint);
        }
    }

    // 마우스가 "상호작용하는" 화면 UI(버튼·슬롯·슬라이더·스크롤·드래그·호버 툴팁 등) 위에 있는지.
    // 배경 이미지·텍스트·HP 바처럼 이벤트를 받지 않는 장식 UI는 Raycast Target이 켜져 있어도 무시 —
    // 퀘스트 HUD 패널 같은 장식 UI가 그 아래 바닥 클릭을 막아 이동이 안 되던 문제. 씬마다 Raycast Target을 끌 필요 없음
    // 몬스터·캐릭터 머리 위 HP 바 같은 월드 스페이스 UI도 제외 — 그걸 UI로 치면 몬스터 HP 바 위를 우클릭했을 때
    // 공격 명령이 막힌다. 클릭한 프레임에만 호출된다
    private readonly List<UnityEngine.EventSystems.RaycastResult> _uiHits = new List<UnityEngine.EventSystems.RaycastResult>();
    private UnityEngine.EventSystems.PointerEventData _uiPointer;
    private UnityEngine.EventSystems.EventSystem _uiPointerOwner;

    bool IsPointerOverScreenUI()
    {
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem == null) return false;

        if (_uiPointer == null || _uiPointerOwner != eventSystem)
        {
            _uiPointer      = new UnityEngine.EventSystems.PointerEventData(eventSystem);
            _uiPointerOwner = eventSystem;
        }
        _uiPointer.position = Input.mousePosition;

        _uiHits.Clear();
        eventSystem.RaycastAll(_uiPointer, _uiHits);
        foreach (var hit in _uiHits)
        {
            if (hit.gameObject == null) continue;
            Canvas canvas = hit.gameObject.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.WorldSpace) continue;

            // 자기 자신이나 부모에 이벤트 핸들러(Button·Slider·ScrollRect·드래그 슬롯 등)가 있을 때만 UI 클릭으로 본다
            if (UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IEventSystemHandler>(hit.gameObject) != null)
                return true;
        }
        return false;
    }

    // 클릭 지점 근처에 실제로 걸을 수 있는 NavMesh가 있는지 확인. 있으면 그 위의 정확한 지점으로 스냅.
    // 수평·높이 허용치를 따로 봄 — 바위 위를 클릭하면 가장 가까운 NavMesh(바위 가장자리 바닥)와 높이 차가 커서 무시됨
    bool TryGetWalkablePoint(Vector3 rawPoint, out Vector3 walkablePoint)
    {
        walkablePoint = rawPoint;
        float radius = Mathf.Sqrt(maxWalkableSnapHorizontal * maxWalkableSnapHorizontal + maxWalkableSnapHeight * maxWalkableSnapHeight);
        if (!NavMesh.SamplePosition(rawPoint, out NavMeshHit navHit, radius, NavMesh.AllAreas))
            return false;

        Vector3 diff = navHit.position - rawPoint;
        if (Mathf.Abs(diff.y) > maxWalkableSnapHeight) return false;
        if (diff.x * diff.x + diff.z * diff.z > maxWalkableSnapHorizontal * maxWalkableSnapHorizontal) return false;

        walkablePoint = navHit.position;
        return true;
    }

    // ─────────────────────────────────────────────────────────────────
    // 레이캐스트 (결과만 반환, 사이드이펙트 없음)
    // ─────────────────────────────────────────────────────────────────

    // 적 레이어만 보고 쏘면 벽·바닥을 뚫고 그 너머(벽 뒤, 아래층)의 몬스터까지 클릭됨 → 이동 대신 공격 명령이 나가서
    // 마커도 안 뜨고 캐릭터가 엉뚱한 적을 향함. 적보다 앞에 바닥·장애물이 있으면 적 클릭으로 보지 않는다
    // 단, OcclusionFader가 반투명하게 만든 벽은 적이 그대로 보이므로 가림으로 치지 않는다
    private readonly RaycastHit[] _occluderHits = new RaycastHit[16];

    bool TryGetEnemyHit(Ray ray, out RaycastHit hit)
    {
        if (!Physics.Raycast(ray, out hit, Mathf.Infinity, enemyLayer)) return false;
        int count = Physics.RaycastNonAlloc(ray, _occluderHits, hit.distance, _enemyOccluderMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!OcclusionFader.IsFaded(_occluderHits[i].collider)) return false;
        return true;
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
        // 클릭한 콜라이더가 몬스터 자식(무기 등)이어도 본체(EnemyHp)를 대상으로 — 자식을 대상으로 잡으면
        // 사망 감지·대상 HP UI가 안 돼서 시체가 사라질 때까지 계속 때렸음
        EnemyHp enemy = hit.collider.GetComponentInParent<EnemyHp>();
        if (enemy != null && enemy.isDead) return;
        Transform target = enemy != null ? enemy.transform : hit.transform;

        _hasPendingDestination = false; // 공격 명령이 이동 목적지를 덮어씀
        foreach (var member in partyMembers)
        {
            if (member.CurrentState == PartyMemberScript.MemberState.Dead) continue;
            var attack = member.AttackComp;
            if (attack != null) attack.SetTarget(target);
        }

        // hit.point는 몬스터 콜라이더 표면(몸통 등)에서 맞은 지점이라 지면보다 이미 떠있을 수 있음 —
        // 마커는 타겟의 발밑(루트 위치) 기준으로 찍음
        SpawnMarker("AttackMarker", target.position);
    }

    void DispatchMoveCommand(Vector3 destination)
    {
        _lastMoveDestination = destination;
        _hasPendingDestination = true;

        var leaderAttack = currentLeader.AttackComp;
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
                var attack = member.AttackComp;
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

        // 이동 목적지가 있고 새 리더가 아직 도착 전이면 목적지 이어받기 — 단, 새 리더가 승계 시점에
        // 이미 전투 중(Attacking, 팔로워 시절부터 몬스터를 공격하고 있었음)이었다면 이어받지 않는다.
        // 그렇지 않으면(파티원 고정 중 리더가 죽어 팔로워가 승계하는 경우 등) 한창 싸우던 팔로워가
        // 새 리더가 되자마자 죽은 리더의 옛 이동 목적지로 걸어가 버리는, 오늘 고친 것과 동일한
        // 유형의 문제가 생긴다 — UpdateChainOrder가 방금 위에서 CurrentState를 올바르게 세팅해뒀으므로 그대로 신뢰
        if (_hasPendingDestination)
        {
            if (newLeader.CurrentState == PartyMemberScript.MemberState.Attacking)
            {
                _hasPendingDestination = false;
            }
            else
            {
                float distSqr      = (newLeader.transform.position - _lastMoveDestination).sqrMagnitude;
                float stopThresh   = newLeader.agent.stoppingDistance + 0.5f;
                if (distSqr > stopThresh * stopThresh)
                    newLeader.agent.SetDestination(_lastMoveDestination);
                else
                    _hasPendingDestination = false; // 이미 도착 → 기록 초기화
            }
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
        // 씬 시작 직후(전원 사망 세이브)면 로딩이 끝나 SceneLoader가 시간을 되돌린 뒤에 멈춰야 정지가 유지된다
        while (SceneLoader.IsLoading) yield return null;
        // yield 도중 gameOverUI가 파괴됐을 경우를 대비해 재확인
        if (gameOverUI == null) yield break;
        MenuTabUI.CloseAllWindows();
        gameOverUI.SetActive(true);
        IsGameOver = true;
        // 게임오버는 BGM을 즉시 끊는다 (페이드 없이)
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
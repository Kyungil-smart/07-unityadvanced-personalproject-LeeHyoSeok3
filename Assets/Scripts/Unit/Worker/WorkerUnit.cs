using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일꾼 유닛
///
/// 상태 흐름:
///   [자원 채집] Idle → Move(경로 이동) → Harvest → (에너지 소진) → 제거
///   [건물 건설] Idle → Move(경로 이동) → Build → (건설 완료) → Idle
/// </summary>
[RequireComponent(typeof(WorkerEnergy))]
public class WorkerUnit : UnitBase
{
    public ResourceNode          AssignedNode          { get; private set; }
    public WorkerEnergy          Energy                { get; private set; }
    public Cluster       OwnerCluster          { get; set; }

    private BuildingConstruction _assignedConstruction;
    public  BuildingConstruction AssignedConstruction => _assignedConstruction;
    // 수거 대기 중인 DroppedResource 여부 (건설 배정 제외 판단에 사용)
    public  bool                 HasPendingDropped     => _pendingDropped != null;
    private float                _harvestTimer;

    // 자원 회수
    private ResourceType         _carryingType;
    private int                  _carryingAmount;
    private DroppedResource      _pendingDropped;
    private bool                 _castleColliderActivated;
    private bool                 _droppedColliderActivated;
    private bool                 _nodeColliderActivated;

    // 경로 이동
    private List<Vector3>        _path;
    private int                  _pathIndex;
    private Vector3              _finalDestination;
    private const float          WAYPOINT_THRESHOLD = 0.15f;

    // 이동 중 밀어내기
    private float                _defaultMass;
    [SerializeField] private float movingMass                  = 5f;
    [SerializeField] private int   remainingWaypointsThreshold = 3;  // 콜라이더 활성화까지 남은 웨이포인트 수
    [SerializeField] private float huntingAttackCooldown       = 1.5f; // 공격 가능 주기 (초)
    [SerializeField] private float huntingAttackRange       = 0.5f; // 공격 사거리
    [SerializeField] private float huntingPathRefreshInterval = 0.5f; // 추적 경로 갱신 주기 (초)

    private float _huntingAttackTimer;
    private float _huntingPathRefreshTimer;

    // Logging_Idle / Mining_Idle: async 스폰 콜백 미착 시 안전 복귀 타이머
    private float _dropWaitSafetyTimer;
    private const float DROP_WAIT_TIMEOUT = 10f;

    // 정체 감지
    private Vector3              _lastCheckedPos;
    private float                _stuckTimer;
    private const float          STUCK_CHECK_INTERVAL = 2f;
    private const float          STUCK_MOVE_THRESHOLD = 0.05f;

    protected override void Awake()
    {
        base.Awake();
        Energy       = GetComponent<WorkerEnergy>();
        _defaultMass = _rb.mass;
    }

    protected override void Start()
    {
        if (data != null)
            Energy.Initialize(this, data.maxEnergy);
        base.Start();
    }

    void Update()
    {
        if (IsDead) return;
        if (!GameManager.Instance.IsPlaying) return;

        UpdateStairLayer();

        switch (State)
        {
            case UnitState.Build_Move:
            case UnitState.Logging_Move:
            case UnitState.Mining_Move:
            case UnitState.Hunting_Move:
            case UnitState.Grab_Wood_Move:
            case UnitState.Grab_Gold_Move:
            case UnitState.Grab_Meat_Move:
            case UnitState.Move:    UpdateMove();    break;
            case UnitState.Harvest:
            case UnitState.Logging:
            case UnitState.Mining:
            case UnitState.Hunting:
            case UnitState.HuntingIdle: UpdateHarvest(); break;
            case UnitState.Build:       UpdateBuild();   break;
            case UnitState.Logging_Idle:
            case UnitState.Mining_Idle: UpdateDropWait(); break;
        }

        UpdateStairLayer();
    }

    // 계단 위 여부를 매 프레임 체크해서 물리 레이어 전환
    void UpdateStairLayer()
    {
        if (!ServiceLocator.Has<PathfindingGrid>()) return;

        var grid     = ServiceLocator.Get<PathfindingGrid>();
        var cellPos  = grid.WorldToCell(transform.position);
        bool onStair = grid.IsOnStair(cellPos);

        var floorObj = GetComponent<FloorObject>();
        if (floorObj != null && onStair != floorObj.IsOnStair)
            floorObj.SetOnStair(onStair);
    }

    // -------------------------------------------------------
    // 이동 (경로 탐색)
    // -------------------------------------------------------
    void RequestPathTo(Vector3 destination)
    {
        _finalDestination = destination;
        _path             = null;
        _pathIndex        = 0;
        _stuckTimer       = 0f;
        _lastCheckedPos   = transform.position;

        // ServiceLocator 또는 FindFirstObjectByType으로 PathfindingGrid 탐색
        PathfindingGrid grid = ServiceLocator.Has<PathfindingGrid>()
            ? ServiceLocator.Get<PathfindingGrid>()
            : Object.FindFirstObjectByType<PathfindingGrid>();

        if (grid == null)
        {
            Debug.LogWarning("[WorkerUnit] PathfindingGrid 씬에 없음 → 이동 불가");
            return;
        }

        // 찾았으면 ServiceLocator에 등록 (다음 호출부터 빠르게 접근)
        if (!ServiceLocator.Has<PathfindingGrid>())
            ServiceLocator.Register<PathfindingGrid>(grid);

        var path = TilemapPathfinder.FindPath(transform.position, destination, grid);

        if (path != null && path.Count > 0)
        {
            _path = path;
            // path[0]은 현재 격자 셀 중심 - 이미 해당 셀 안에 있으므로 건너뜀
            _pathIndex = path.Count > 1 ? 1 : 0;
        }
        else
        {
            Debug.LogWarning($"[WorkerUnit] {name} 경로 탐색 실패");
        }
    }

    // -------------------------------------------------------
    // 디버그 전용 이동
    // -------------------------------------------------------
    public void DebugMoveTo(System.Collections.Generic.List<Vector3> path)
    {
        ClearAssignment();
        _path      = path;
        _pathIndex = 0;
        StateMachine.ChangeState(UnitState.Move);
    }

    // -------------------------------------------------------
    // 자원 채집 할당 (Logging / Mining / Hunting)
    // -------------------------------------------------------
    public void AssignNode(ResourceNode node, ResourceType resourceType)
    {
        ClearBuildAssignment();

        if (AssignedNode != null) AssignedNode.UnregisterWorker();

        if (!node.TryRegisterWorker(this))
        {
            Debug.LogWarning($"[WorkerUnit] {node.name} 등록 실패");
            return;
        }

        AssignedNode          = node;
        _harvestTimer         = 0f;
        _carryingType         = resourceType;
        _nodeColliderActivated = false; // 새 노드 이동 시작 — 콜라이더 활성화 플래그 리셋

        UnitState moveState = resourceType switch
        {
            ResourceType.Wood => UnitState.Logging_Move,
            ResourceType.Gold => UnitState.Mining_Move,
            ResourceType.Meat => UnitState.Hunting_Move,
            _                 => UnitState.Move
        };

        RequestPathTo(node.transform.position);
        StateMachine.ChangeState(moveState);
        Debug.Log($"[WorkerUnit] {name} → {node.name} 이동 ({moveState})");
    }

    // 구버전 호환
    public void AssignNode(ResourceNode node) => AssignNode(node, ResourceType.Wood);

    // -------------------------------------------------------
    // 건설 할당
    // -------------------------------------------------------
    public void AssignConstruction(BuildingConstruction construction)
    {
        // ClearAssignment() 대신 직접 해제 — SetIdleAndNotify() 호출 금지
        // ClearAssignment()는 내부에서 OnWorkerBecameIdle 이벤트를 발행하는데,
        // 이 시점에 _assignedConstruction이 아직 null이라서 다른 BuildingConstruction이
        // 같은 워커를 중복 배정하는 재귀 버그가 발생함
        if (AssignedNode != null)
        {
            AssignedNode.UnregisterWorker();
            AssignedNode = null;
        }
        ClearBuildAssignment(); // _workerAssigned 리셋은 여기서 처리됨

        _assignedConstruction = construction;

        Vector3 arrivalPos = construction.GetArrivalPosition(transform.position);
        RequestPathTo(arrivalPos);
        StateMachine.ChangeState(UnitState.Build_Move);
        Debug.Log($"[WorkerUnit] {name} → {construction.name} 건설 이동");
    }

    // -------------------------------------------------------
    // 상태별 업데이트
    // -------------------------------------------------------
    void UpdateMove()
    {
        // Hunting_Move: 동물 추적 + 쿨타임 차감 + 사거리 공격 판정
        if (State == UnitState.Hunting_Move && AssignedNode != null && !AssignedNode.IsDeplete)
        {
            // 쿨타임 차감
            if (_huntingAttackTimer > 0f)
                _huntingAttackTimer -= Time.deltaTime;

            // 동물 위치로 경로 주기적 갱신
            _huntingPathRefreshTimer -= Time.deltaTime;
            if (_huntingPathRefreshTimer <= 0f)
            {
                _huntingPathRefreshTimer = huntingPathRefreshInterval;
                RequestPathTo(AssignedNode.transform.position);
            }

            // 사거리 내 → 이동 중단 후 공격 또는 쿨타임 대기
            float dist = Vector3.Distance(transform.position, AssignedNode.transform.position);
            if (dist <= huntingAttackRange)
            {
                StopMove();
                _path = null;
                StateMachine.ChangeState(_huntingAttackTimer <= 0f ? UnitState.Hunting : UnitState.HuntingIdle);
                return;
            }
        }

        // Grab_*_Move 중 남은 웨이포인트 수 기준으로 Collider 활성화 → Castle trigger 감지
        if (!_castleColliderActivated &&
            (State == UnitState.Grab_Wood_Move ||
             State == UnitState.Grab_Gold_Move ||
             State == UnitState.Grab_Meat_Move) &&
            _path != null && (_path.Count - _pathIndex) <= remainingWaypointsThreshold)
        {
            foreach (var col in GetComponents<Collider2D>())
                col.enabled = true;
            _castleColliderActivated = true;
        }

        // *_Move 중 채집물로 이동 시 남은 웨이포인트 수 기준으로 Collider 활성화 → DroppedResource trigger 감지
        if (!_droppedColliderActivated && _pendingDropped != null &&
            (State == UnitState.Logging_Move ||
             State == UnitState.Mining_Move  ||
             State == UnitState.Hunting_Move) &&
            _path != null && (_path.Count - _pathIndex) <= remainingWaypointsThreshold)
        {
            foreach (var col in GetComponents<Collider2D>())
                col.enabled = true;
            _droppedColliderActivated = true;
        }

        // *_Move 중 자원 노드로 이동 시 남은 웨이포인트 수 기준으로 Collider 활성화 → ResourceNode trigger 감지
        if (!_nodeColliderActivated && _pendingDropped == null && AssignedNode != null &&
            (State == UnitState.Logging_Move ||
             State == UnitState.Mining_Move) &&
            _path != null && (_path.Count - _pathIndex) <= remainingWaypointsThreshold)
        {
            foreach (var col in GetComponents<Collider2D>())
                col.enabled = true;
            _nodeColliderActivated = true;
        }

        if (_path == null || _path.Count == 0)
        {
            OnArrived();
            return;
        }

        // 정체 감지 → 2초 이상 같은 위치면 경로 재설정
        _stuckTimer += Time.deltaTime;
        if (_stuckTimer >= STUCK_CHECK_INTERVAL)
        {
            _stuckTimer = 0f;
            if (Vector3.Distance(transform.position, _lastCheckedPos) < STUCK_MOVE_THRESHOLD)
            {
                Debug.LogWarning($"[WorkerUnit] {name} 정체 감지 → 경로 재설정");
                // 사냥 추적 중에는 현재 동물 위치로 경로 재설정 (과거 위치 사용 방지)
                Vector3 target = (State == UnitState.Hunting_Move && AssignedNode != null)
                    ? AssignedNode.transform.position
                    : _finalDestination;
                RequestPathTo(target);
                return;
            }
            _lastCheckedPos = transform.position;
        }

        Vector3 waypoint = _path[_pathIndex];
        waypoint.z = 0f;

        if (Vector3.Distance(transform.position, waypoint) <= WAYPOINT_THRESHOLD)
        {
            _pathIndex++;
            if (_pathIndex >= _path.Count)
            {
                StopMove();
                OnArrived();
                return;
            }

            Debug.Log($"[WorkerUnit] {name} 다음 웨이포인트 [{_pathIndex}/{_path.Count - 1}] → {_path[_pathIndex]:F2}");
        }

        MoveTo(_path[_pathIndex]);
    }

    void OnArrived()
    {
        _path = null;

        // 건설 도착
        if (_assignedConstruction != null)
        {
            if (_assignedConstruction.IsComplete)
            { ClearBuildAssignment(); return; }

            _assignedConstruction.RegisterPendingWorker(this);
            StateMachine.ChangeState(UnitState.Build);
            return;
        }

        // Grab_Move 중 경로 종료 → Castle trigger 도착을 기다림
        // 실제 자원 저장은 Castle.OnTriggerEnter2D → OnArrivedAtCastle() 에서 처리
        if (State == UnitState.Grab_Wood_Move ||
            State == UnitState.Grab_Gold_Move ||
            State == UnitState.Grab_Meat_Move)
        {
            // Collider 다시 활성화해서 Castle trigger 감지 가능하게
            foreach (var col in GetComponents<Collider2D>())
                col.enabled = true;
            return;
        }

        // 채집물 도착 (DroppedResource)
        if (_pendingDropped != null)
        {
            var dropped = _pendingDropped;
            _pendingDropped = null;
            dropped.OnWorkerArrived(this);
            return;
        }

        // 채집 노드 도착
        if (AssignedNode != null && !AssignedNode.IsDeplete)
        {
            // 사냥: 사거리 확인 → 범위 밖이면 재추적
            if (_carryingType == ResourceType.Meat)
            {
                float dist = Vector3.Distance(transform.position, AssignedNode.transform.position);
                if (dist > huntingAttackRange)
                {
                    _huntingPathRefreshTimer = 0f; // 즉시 경로 갱신
                    RequestPathTo(AssignedNode.transform.position);
                    StateMachine.ChangeState(UnitState.Hunting_Move);
                    return;
                }

                if (_huntingAttackTimer <= 0f)
                {
                    StateMachine.ChangeState(UnitState.Hunting);
                    return;
                }

                // 쿨타임 남아있으면 추적 계속
                _huntingPathRefreshTimer = 0f;
                RequestPathTo(AssignedNode.transform.position);
                StateMachine.ChangeState(UnitState.Hunting_Move);
                return;
            }

            UnitState workState = _carryingType switch
            {
                ResourceType.Wood => UnitState.Logging,
                ResourceType.Gold => UnitState.Mining,
                _                 => UnitState.Harvest
            };
            StateMachine.ChangeState(workState);
            return;
        }

        ClearAssignment();
    }

    /// <summary>자원 수집 완료 → 본진으로 이동 시작</summary>
    public void StartGrabMove(ResourceType type, int amount)
    {
        _pendingDropped = null; // 채집물 수거 완료 — 이중 처리 방지
        _carryingType   = type;
        _carryingAmount = amount;

        var castle = ServiceLocator.Has<Castle>()
            ? ServiceLocator.Get<Castle>()
            : Object.FindFirstObjectByType<Castle>();

        if (castle == null)
        {
            Debug.LogWarning("[WorkerUnit] Castle 없음 → 자원 저장 불가");
            ClearAssignment();
            return;
        }

        UnitState grabState = type switch
        {
            ResourceType.Wood => UnitState.Grab_Wood_Move,
            ResourceType.Gold => UnitState.Grab_Gold_Move,
            ResourceType.Meat => UnitState.Grab_Meat_Move,
            _                 => UnitState.Grab_Wood_Move
        };

        _castleColliderActivated = false;
        RequestPathTo(castle.GetPosition());
        StateMachine.ChangeState(grabState);
        Debug.Log($"[WorkerUnit] {name} 자원 {type} x{amount} 들고 본진으로 이동");
    }

    /// <summary>채집물 오브젝트로 이동 (Grab_*_Move 전 단계)</summary>
    public void MoveToDropped(DroppedResource dropped)
    {
        _pendingDropped = dropped;

        // 아직 낙하 중이면 전용 대기 상태로 전환 → 착지 후 OnDroppedResourceLanded() 호출됨
        // Idle 대신 전용 상태를 사용해 OnWorkerBecameIdle 이벤트가 오발하지 않도록 함
        if (!dropped.IsLanded)
        {
            UnitState waitState = _carryingType switch
            {
                ResourceType.Wood => UnitState.Logging_Idle,
                ResourceType.Gold => UnitState.Mining_Idle,
                ResourceType.Meat => UnitState.HuntingIdle, // 사냥은 HuntingIdle 재활용
                _                 => UnitState.Idle
            };
            StateMachine.ChangeState(waitState);
            return;
        }

        MoveToDroppedInternal(dropped);
    }

    /// <summary>DroppedResource 착지 완료 콜백</summary>
    public void OnDroppedResourceLanded(DroppedResource dropped)
    {
        if (_pendingDropped != dropped) return;
        MoveToDroppedInternal(dropped);
    }

    void MoveToDroppedInternal(DroppedResource dropped)
    {
        _droppedColliderActivated = false; // 새 이동 시작 — 콜라이더 활성화 플래그 리셋

        UnitState moveState = dropped.resourceType switch
        {
            ResourceType.Wood => UnitState.Logging_Move,
            ResourceType.Gold => UnitState.Mining_Move,
            ResourceType.Meat => UnitState.Hunting_Move,
            _                 => UnitState.Move
        };

        RequestPathTo(dropped.transform.position);
        StateMachine.ChangeState(moveState);
        Debug.Log($"[WorkerUnit] {name} 채집물 위치로 이동");
    }

    void DepositToCastle()
    {
        var castle = ServiceLocator.Has<Castle>()
            ? ServiceLocator.Get<Castle>()
            : Object.FindFirstObjectByType<Castle>();

        castle?.DepositResource(_carryingType, _carryingAmount);
        _carryingAmount = 0;
        ClearAssignment();
    }

    /// <summary>Castle trigger 진입 콜백 (Castle.OnTriggerEnter2D에서 호출)</summary>
    public void OnArrivedAtCastle(Castle castle)
    {
        bool isGrabbing = State == UnitState.Grab_Wood_Move ||
                          State == UnitState.Grab_Gold_Move ||
                          State == UnitState.Grab_Meat_Move;
        if (!isGrabbing) return;

        _path = null;
        StopMove();
        _castleColliderActivated = false;
        castle.DepositResource(_carryingType, _carryingAmount);
        _carryingAmount = 0;
        ClearAssignment();
    }

    void UpdateHarvest()
    {
        switch (State)
        {
            case UnitState.Hunting:     UpdateHuntingAttack(); break;
            case UnitState.HuntingIdle: UpdateHuntingIdle();   break;
            default:                    UpdateLoggingMining(); break;
        }
    }

    // 사냥 공격 — 상태 전환은 Animation Event(OnHuntingHit / OnHuntingAnimEnd)가 담당
    void UpdateHuntingAttack() { }

    // HuntingIdle — 동물 생사 여부에 따라 두 가지 모드로 분기
    void UpdateHuntingIdle()
    {
        // 동물 사망 → DroppedResource 착지 대기 모드
        if (AssignedNode == null)
        {
            UpdateDropWait();
            return;
        }

        // 동물 생존 → 공격 쿨타임 대기 후 추적 재개
        _huntingAttackTimer -= Time.deltaTime;
        if (_huntingAttackTimer <= 0f)
        {
            _huntingPathRefreshTimer = 0f;
            RequestPathTo(AssignedNode.transform.position);
            StateMachine.ChangeState(UnitState.Hunting_Move);
        }
    }

    // 벌목 / 채광 타이머 처리
    void UpdateLoggingMining()
    {
        if (AssignedNode == null || AssignedNode.IsDeplete)
        { ClearAssignment(); return; }

        _harvestTimer += Time.deltaTime;
        if (_harvestTimer < AssignedNode.harvestDuration) return;

        _harvestTimer = 0f;

        // CompleteHarvest() 호출 전에 전용 대기 상태로 전환
        // → 다음 프레임에 UpdateLoggingMining()이 재진입하지 않도록 차단
        // → AssignedNode = null 이후 ClearAssignment() → SetIdleAndNotify() 오발 방지
        UnitState preWaitState = _carryingType switch
        {
            ResourceType.Wood => UnitState.Logging_Idle,
            ResourceType.Gold => UnitState.Mining_Idle,
            _                 => UnitState.Idle
        };
        StateMachine.ChangeState(preWaitState);

        // CompleteHarvest 내부에서 MoveToDropped() 호출 → _pendingDropped 설정 및 상태 재전환
        AssignedNode.CompleteHarvest(this);
        AssignedNode.UnregisterWorker();
        AssignedNode = null;
    }

    /// <summary>Animation Event - Logging 클립 타격 프레임에 연결</summary>
    public void OnLoggingHit()
    {
        if (State != UnitState.Logging) return;
        AssignedNode?.PlayHarvestParticle();
    }

    /// <summary>Animation Event - Mining 클립 타격 프레임에 연결</summary>
    public void OnMiningHit()
    {
        if (State != UnitState.Mining) return;
        AssignedNode?.PlayHarvestParticle();
    }

    /// <summary>Animation Event - Hunting 클립 3번째 프레임에 연결</summary>
    public void OnHuntingHit()
    {
        if (State != UnitState.Hunting) return;
        if (AssignedNode is AnimalNode animal)
            animal.TakeDamage(this);
    }

    /// <summary>Animation Event - Hunting 클립 마지막 프레임에 연결</summary>
    public void OnHuntingAnimEnd()
    {
        if (State != UnitState.Hunting) return;
        _huntingAttackTimer = huntingAttackCooldown; // 쿨타임 시작

        // 동물이 죽었으면 DroppedResource 착지 대기
        // HuntingIdle을 재활용 — UpdateHarvest()에서 AssignedNode == null 여부로 분기
        // async 스폰의 경우 이 시점에 _pendingDropped가 아직 null일 수 있음
        if (AssignedNode == null || AssignedNode.IsDeplete)
        {
            StateMachine.ChangeState(UnitState.HuntingIdle);
            return;
        }

        // 추적 재개
        _huntingPathRefreshTimer = 0f;
        RequestPathTo(AssignedNode.transform.position);
        StateMachine.ChangeState(UnitState.Hunting_Move);
    }

    void UpdateBuild()
    {
        if (_assignedConstruction == null || _assignedConstruction.IsComplete)
            ClearBuildAssignment();
    }

    // Logging_Idle / Mining_Idle 대기 상태 — DroppedResource 착지 콜백 대기
    void UpdateDropWait()
    {
        if (_pendingDropped != null)
        {
            // 콜백 수신 완료 — 타이머 리셋 (MoveToDropped/OnDroppedResourceLanded 에서 상태 전환됨)
            _dropWaitSafetyTimer = 0f;
            return;
        }

        // async 스폰 콜백이 아직 미착일 수 있으므로 즉시 전환하지 않음
        // 일정 시간(DROP_WAIT_TIMEOUT)이 지나도 콜백이 오지 않으면 안전하게 Idle로 복귀
        _dropWaitSafetyTimer += Time.deltaTime;
        if (_dropWaitSafetyTimer >= DROP_WAIT_TIMEOUT)
        {
            _dropWaitSafetyTimer = 0f;
            Debug.LogWarning($"[WorkerUnit] {name} DroppedResource 콜백 타임아웃 → Idle 복귀");
            SetIdleAndNotify();
        }
    }

    // -------------------------------------------------------
    // 건설 완료 콜백
    // -------------------------------------------------------
    /// <summary>
    /// 건물 trigger 진입 시 호출
    /// 이동 즉시 중단 + 경로 초기화 → OnArrived 호출
    /// </summary>
    public void ForceArrive()
    {
        _path      = null;
        _pathIndex = 0;
        StopMove();
        OnArrived();
    }

    /// <summary>trigger 진입 → 건설 시작 콜백</summary>
    public void OnConstructionTriggerEnter(BuildingConstruction construction)
    {
        if (_assignedConstruction != construction) return;
        construction.StartBuilding(this);
    }

    public void OnConstructionComplete()
    {
        _assignedConstruction = null;
        SetIdleAndNotify();
        Debug.Log($"[WorkerUnit] {name} 건설 완료 → Idle");
    }

    // Animator 파라미터 이름 상수
    private static readonly string[] ALL_ANIM_BOOLS =
    {
        "IsIdle",
        "IsMove",
        "IsBuild",
        "IsLogging",
        "IsLoggingIdle",
        "IsMining",
        "IsMiningIdle",
        "IsHunting",
        "IsHuntingIdle",
        "IsBuild_Move",
        "IsLogging_Move",
        "IsMining_Move",
        "IsHunting_Move",
        "IsGrab_Wood_Move",
        "IsGrab_Gold_Move",
        "IsGrab_Meat_Move",
    };

    void SetAnimState(string activeBool)
    {
        foreach (var b in ALL_ANIM_BOOLS)
            SetAnimBool(b, b == activeBool);
    }

    void SetCollider(bool enabled)
    {
        foreach (var col in GetComponents<Collider2D>())
            col.enabled = enabled;
    }

    // -------------------------------------------------------
    // 상태 전환 훅
    // -------------------------------------------------------
    public override void OnStateEnter(UnitState state)
    {
        switch (state)
        {
            case UnitState.Idle:
                _rb.mass = _defaultMass;
                SetCollider(true);
                StopMove();
                SetAnimState("IsIdle");
                break;

            case UnitState.Move:
            case UnitState.Build_Move:
                _rb.mass = movingMass;
                SetCollider(false);
                SetAnimState(state == UnitState.Move ? "IsMove" : "IsBuild_Move");
                break;

            case UnitState.Logging_Move:
                _rb.mass = movingMass;
                SetCollider(false);
                SetAnimState("IsLogging_Move");
                break;

            case UnitState.Mining_Move:
                _rb.mass = movingMass;
                SetCollider(false);
                SetAnimState("IsMining_Move");
                break;

            case UnitState.Hunting_Move:
                _rb.mass = movingMass;
                SetCollider(false);
                SetAnimState("IsHunting_Move");
                break;

            case UnitState.Grab_Wood_Move:
                _rb.mass = movingMass;
                SetCollider(false);
                SetAnimState("IsGrab_Wood_Move");
                break;

            case UnitState.Grab_Gold_Move:
                _rb.mass = movingMass;
                SetCollider(false);
                SetAnimState("IsGrab_Gold_Move");
                break;

            case UnitState.Grab_Meat_Move:
                _rb.mass = movingMass;
                SetCollider(false);
                SetAnimState("IsGrab_Meat_Move");
                break;

            case UnitState.Harvest:
            case UnitState.Logging:
                SetCollider(true);
                SetAnimState("IsLogging");
                break;

            case UnitState.Mining:
                SetCollider(true);
                SetAnimState("IsMining");
                break;

            case UnitState.Hunting:
                SetCollider(true);
                SetAnimState("IsHunting");
                break;
            case UnitState.HuntingIdle:
                _dropWaitSafetyTimer = 0f; // 안전 타이머 리셋 (drop 대기 모드 진입 시 사용)
                SetCollider(true);
                SetAnimState("IsHuntingIdle");
                break;

            case UnitState.Logging_Idle:
                _rb.mass = _defaultMass;
                _dropWaitSafetyTimer = 0f; // 안전 타이머 리셋
                SetCollider(true);
                StopMove();
                SetAnimState("IsLoggingIdle");
                break;

            case UnitState.Mining_Idle:
                _rb.mass = _defaultMass;
                _dropWaitSafetyTimer = 0f; // 안전 타이머 리셋
                SetCollider(true);
                StopMove();
                SetAnimState("IsMiningIdle");
                break;

            case UnitState.Build:
                SetCollider(true);
                SetAnimState("IsBuild");
                break;

            case UnitState.Dead:
                SetAnimState("");
                SetAnim("Dead");
                break;
        }
    }

    public override void OnStateExit(UnitState state)
    {
        if (state == UnitState.Harvest && AssignedNode != null)
            AssignedNode.UnregisterWorker();

        if (state == UnitState.Build)
        {
            foreach (var col in GetComponents<Collider2D>())
                col.enabled = true;
        }
    }

    // -------------------------------------------------------
    // 할당 해제
    // -------------------------------------------------------
    public void ClearAssignment()
    {
        if (AssignedNode != null)
        {
            AssignedNode.UnregisterWorker();
            AssignedNode = null;
        }
        ClearBuildAssignment();
        SetIdleAndNotify();
    }

    void ClearBuildAssignment()
    {
        // 배정된 건물에 워커 해제 알림 → 건물이 새 워커를 다시 탐색할 수 있도록
        if (_assignedConstruction != null)
        {
            _assignedConstruction.SetWorkerAssigned(false);
            _assignedConstruction = null;
        }
        _path = null;
    }

    // Idle 상태 전환 + UI 갱신 이벤트 발행 (죽은 워커는 제외)
    void SetIdleAndNotify()
    {
        StateMachine.ChangeState(UnitState.Idle);
        if (!IsDead)
            EventBus.Publish(new OnWorkerBecameIdle { worker = this });
    }

    public override void TakeDamage(int damage) { }

    protected override void OnDead()
    {
        ClearAssignment();
        StartCoroutine(ReturnToPoolDelayed(0.5f));
    }

    // ---
    // 풀링 지원
    // ---

    /// <summary>
    /// 에너지 소진 등 외부에서 사망을 강제할 때 호출
    /// WorkerUnit.TakeDamage는 비어있으므로 이 메서드를 사용
    /// </summary>
    public void ForceKill()
    {
        if (IsDead) return;
        StopMove();
        StateMachine.ChangeState(UnitState.Dead);
        EventBus.Publish(new OnUnitDied { unit = this });
        EventBus.Publish(new OnScorePenalty { scoreType = ScoreType.UnitDeath, amount = 1 });
        OnDead();
    }

    /// <summary>
    /// 풀 반납 전 모든 상태를 초기화
    /// Cluster.ResetBuilding 또는 ReturnToPoolDelayed에서 호출
    /// </summary>
    public void ResetForPool()
    {
        // 노드 및 건설 배정 해제
        if (AssignedNode != null)
        {
            AssignedNode.UnregisterWorker();
            AssignedNode = null;
        }
        _assignedConstruction = null;
        _path                 = null;
        _pathIndex            = 0;
        _carryingAmount       = 0;
        _carryingType         = default;
        _pendingDropped       = null;
        _castleColliderActivated  = false;
        _droppedColliderActivated = false;
        _nodeColliderActivated    = false;
        _huntingAttackTimer   = 0f;
        _huntingPathRefreshTimer = 0f;
        _stuckTimer           = 0f;

        // 에너지 초기화
        if (data != null)
            Energy.Initialize(this, data.maxEnergy);

        // HP 초기화
        if (data != null)
        {
            // 부모 UnitBase의 CurrentHp는 private set이므로 TakeDamage로 0 만든 뒤
            // StateMachine.Reset()으로 Dead 상태 해제 후 HP 재설정
        }

        // StateMachine Dead 락 해제 (OnStateEnter/Exit 없이 직접 초기화)
        StateMachine.Reset();

        // HP 복구
        RestoreFullHp();

        StopMove();
    }

    private System.Collections.IEnumerator ReturnToPoolDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);

        // OwnerCluster를 통해 prefab 참조를 얻고 풀에 반납
        if (OwnerCluster != null
            && OwnerCluster.workerPrefab != null
            && ServiceLocator.Has<PoolManager>())
        {
            ResetForPool();
            ServiceLocator.Get<PoolManager>().Despawn(OwnerCluster.workerPrefab, gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
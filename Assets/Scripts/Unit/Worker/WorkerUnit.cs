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
    private float                _harvestTimer;

    // 자원 회수
    private ResourceType         _carryingType;
    private int                  _carryingAmount;
    private DroppedResource      _pendingDropped;
    private bool                 _castleColliderActivated;

    // 경로 이동
    private List<Vector3>        _path;
    private int                  _pathIndex;
    private Vector3              _finalDestination;
    private const float          WAYPOINT_THRESHOLD = 0.15f;

    // 이동 중 밀어내기
    private float                _defaultMass;
    [SerializeField] private float movingMass              = 5f;
    [SerializeField] private float colliderActivationRange  = 2f;
    [SerializeField] private float huntingAttackCooldown    = 1.5f; // 공격 가능 주기 (초)
    [SerializeField] private float huntingAttackRange       = 0.5f; // 공격 사거리
    [SerializeField] private float huntingPathRefreshInterval = 0.5f; // 추적 경로 갱신 주기 (초)

    private float _huntingAttackTimer;
    private float _huntingPathRefreshTimer;

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
            case UnitState.Build:   UpdateBuild();   break;
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

        AssignedNode  = node;
        _harvestTimer = 0f;
        _carryingType = resourceType;

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
        ClearAssignment();
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

        // Grab_*_Move 중 Castle 근접 시 Collider 활성화
        if (!_castleColliderActivated &&
            (State == UnitState.Grab_Wood_Move ||
             State == UnitState.Grab_Gold_Move ||
             State == UnitState.Grab_Meat_Move))
        {
            var castle = ServiceLocator.Has<Castle>()
                ? ServiceLocator.Get<Castle>()
                : Object.FindFirstObjectByType<Castle>();

            if (castle != null &&
                Vector3.Distance(transform.position, castle.GetPosition()) <= colliderActivationRange)
            {
                foreach (var col in GetComponents<Collider2D>())
                    col.enabled = true;
                _castleColliderActivated = true;
            }
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

        // 아직 낙하 중이면 Idle 대기 → 착지 후 OnDroppedResourceLanded() 호출됨
        if (!dropped.IsLanded)
        {
            SetIdleAndNotify();
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
        if (AssignedNode == null || AssignedNode.IsDeplete)
        { ClearAssignment(); return; }

        // Hunting 중: 애니메이션이 끝나면 OnHuntingAnimEnd()가 Hunting_Move로 전환
        if (State == UnitState.Hunting) return;

        // HuntingIdle 안전장치: 혹시 진입했다면 쿨타임 차감 후 추적 재개
        if (State == UnitState.HuntingIdle)
        {
            _huntingAttackTimer -= Time.deltaTime;
            if (_huntingAttackTimer <= 0f)
            {
                _huntingPathRefreshTimer = 0f;
                RequestPathTo(AssignedNode.transform.position);
                StateMachine.ChangeState(UnitState.Hunting_Move);
            }
            return;
        }

        _harvestTimer += Time.deltaTime;
        if (_harvestTimer < AssignedNode.harvestDuration) return;

        _harvestTimer = 0f;

        // 벌목 / 채광: 채집 완료 → 현장에 DroppedResource 스폰
        AssignedNode.CompleteHarvest(this);
        AssignedNode.UnregisterWorker();
        AssignedNode = null;
    }

    /// <summary>
    /// Hunting 애니메이션 3번째 프레임에 Animation Event로 호출
    /// Animator → Hunting 클립 → 3번째 프레임에 이벤트 추가
    ///   Function: OnHuntingHit
    /// </summary>
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

        // 동물이 죽었으면 추적 중단
        if (AssignedNode == null || AssignedNode.IsDeplete) return;

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
        "IsMining",
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
                SetCollider(true);
                SetAnimState("IsHuntingIdle");
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
        _assignedConstruction = null;
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
        _castleColliderActivated = false;
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
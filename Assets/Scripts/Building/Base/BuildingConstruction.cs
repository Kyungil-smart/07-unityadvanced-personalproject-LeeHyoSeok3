using UnityEngine;

/// <summary>
/// 건물 건설 시간 컴포넌트
/// </summary>
public class BuildingConstruction : MonoBehaviour
{
    public float BuildTime  { get; private set; }
    public float Progress   { get; private set; }
    public bool  IsBuilding { get; private set; }
    public bool  IsComplete { get; private set; }

    private BuildingBase     _building;
    private WorkerUnit       _builder;
    private SpriteRenderer[] _renderers;
    private WorkerUnit       _pendingWorker;
    private bool             _workerAssigned; // 워커 배정 완료 플래그

    private static readonly Color COLOR_WAITING  = new Color(1f, 1f, 1f, 0.35f);
    private static readonly Color COLOR_BUILDING = new Color(1f, 1f, 0.5f, 0.6f);

    public int BuildingFloor { get; private set; } = -1;

    public void Init(float buildTime, int floorIndex = -1)
    {
        BuildTime     = buildTime;
        BuildingFloor = floorIndex;
        Progress      = 0f;
        _building     = GetComponent<BuildingBase>();
        _renderers    = GetComponentsInChildren<SpriteRenderer>();

        SetColor(COLOR_WAITING);
        Debug.Log($"[Construction] {gameObject.name} 건설 대기 (워커 필요) 층:{floorIndex}");
    }

    void OnEnable()
    {
        // 워커가 Idle 상태로 전환될 때 즉각 배정 시도
        EventBus.Subscribe<OnWorkerBecameIdle>(OnWorkerBecameIdle);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<OnWorkerBecameIdle>(OnWorkerBecameIdle);
    }

    void OnWorkerBecameIdle(OnWorkerBecameIdle e)
    {
        // 이미 건설 진행 중이거나 완료, 또는 워커가 이미 배정된 경우 무시
        if (IsBuilding || IsComplete) return;
        if (_workerAssigned || _pendingWorker != null) return;

        var worker = e.worker;
        if (worker == null) return;
        // 실제로 Idle 상태인지 명시적으로 확인 (재귀 이벤트 등 오발 방지)
        if (!worker.StateMachine.Is(UnitState.Idle)) return;
        // 수거 대기 중인 자원이 있는 워커는 제외
        if (worker.HasPendingDropped) return;
        if (worker.AssignedConstruction != null) return;

        _workerAssigned = true;
        Debug.Log($"[Construction] {worker.name} Idle 전환 → 즉시 건설 배정");
        worker.AssignConstruction(this);

        // AssignConstruction 이후 워커가 실제로 Build_Move 상태인지 검증
        // 배정이 실패했다면 _workerAssigned 플래그를 리셋해 재탐색 허용
        if (!worker.StateMachine.Is(UnitState.Build_Move))
        {
            _workerAssigned = false;
            Debug.LogWarning($"[Construction] {worker.name} 배정 후 Build_Move 진입 실패 → 재탐색 허용");
        }
    }

    public Vector3 GetArrivalPosition(Vector3 workerPos)
    {
        PathfindingGrid grid = ServiceLocator.Has<PathfindingGrid>()
            ? ServiceLocator.Get<PathfindingGrid>()
            : Object.FindFirstObjectByType<PathfindingGrid>();

        if (grid == null) return transform.position;

        Vector3Int cell = grid.WorldToCell(transform.position);
        return grid.CellToWorld(cell);
    }

    void Update()
    {
        // 워커 대기 중 → 주기적으로 유휴 워커 탐색
        if (!IsBuilding && !IsComplete && _pendingWorker == null)
        {
            _workerSearchTimer += Time.deltaTime;
            if (_workerSearchTimer >= WORKER_SEARCH_INTERVAL)
            {
                _workerSearchTimer = 0f;
                TryAssignIdleWorker();
            }
        }

        if (!IsBuilding || IsComplete) return;
        Progress += Time.deltaTime / BuildTime;
        if (Progress >= 1f) Complete();
    }

    private float _workerSearchTimer = 0f;
    private const float WORKER_SEARCH_INTERVAL = 1f;

    void TryAssignIdleWorker()
    {
        if (!ServiceLocator.Has<WorkerAssigner>()) return;

        // 이미 워커가 배정되어 있으면 탐색 중단
        if (_workerAssigned || _pendingWorker != null || IsBuilding) return;

        var allWorkers = ServiceLocator.Get<WorkerAssigner>().GetAllWorkers();
        WorkerUnit nearest  = null;
        float      minDist  = float.MaxValue;

        foreach (var worker in allWorkers)
        {
            if (worker == null) continue;
            if (!worker.StateMachine.Is(UnitState.Idle)) continue;
            // 이미 다른 건물에 배정된 워커 제외
            if (worker.AssignedConstruction != null) continue;
            // 수거 대기 중인 자원이 있는 워커 제외
            if (worker.HasPendingDropped) continue;

            float dist = Vector3.Distance(worker.transform.position, transform.position);
            if (dist < minDist) { minDist = dist; nearest = worker; }
        }

        if (nearest == null) return;

        _workerAssigned = true;
        Debug.Log($"[Construction] 유휴 워커 발견 → {nearest.name} 배정");
        nearest.AssignConstruction(this);

        // 배정 실패 시 플래그 리셋
        if (!nearest.StateMachine.Is(UnitState.Build_Move))
        {
            _workerAssigned = false;
            Debug.LogWarning($"[Construction] {nearest.name} 주기 배정 후 Build_Move 진입 실패 → 재탐색 허용");
        }
    }

    public void NotifyTriggerEnter(WorkerUnit worker)
    {
        if (worker == null || IsBuilding || IsComplete) return;
        if (!CanBuildFloor(worker)) return;

        Debug.Log($"[Construction] {worker.name} trigger 진입 → 건설 시작");
        worker.OnConstructionTriggerEnter(this);
        _pendingWorker = null;
    }

    public void SetWorkerAssigned(bool value) => _workerAssigned = value;

    public void RegisterPendingWorker(WorkerUnit worker)
    {
        _pendingWorker = worker;
        Debug.Log($"[Construction] {worker.name} 건설 대기 등록");

        // 이미 trigger 범위 안에 있으면 즉시 시작
        var col = GetComponent<Collider2D>();
        if (col == null) return;

        var hits = Physics2D.OverlapBoxAll(col.bounds.center, col.bounds.size, 0f);
        foreach (var hit in hits)
        {
            if (hit.GetComponent<WorkerUnit>() != worker) continue;
            if (!CanBuildFloor(worker)) return;

            Debug.Log($"[Construction] {worker.name} 이미 범위 안 → 즉시 건설 시작");
            worker.OnConstructionTriggerEnter(this);
            _pendingWorker = null;
            return;
        }
    }

    public bool CanBuildFloor(WorkerUnit worker)
    {
        PathfindingGrid grid = ServiceLocator.Has<PathfindingGrid>()
            ? ServiceLocator.Get<PathfindingGrid>()
            : Object.FindFirstObjectByType<PathfindingGrid>();

        if (grid == null) return true;

        Vector3Int buildingCell  = grid.WorldToCell(transform.position);
        Vector3Int workerCell    = grid.WorldToCell(worker.transform.position);
        int        buildingFloor = BuildingFloor >= 0
            ? BuildingFloor
            : grid.GetFloorIndex(buildingCell);
        int        workerFloor   = grid.GetFloorIndex(workerCell);

        if (buildingFloor != workerFloor)
        {
            Debug.Log($"[Construction] 층 불일치: building={buildingFloor} worker={workerFloor}");
            return false;
        }

        return true;
    }

    public void StartBuilding(WorkerUnit worker)
    {
        if (IsBuilding || IsComplete) return;

        _builder   = worker;
        IsBuilding = true;

        SetColor(COLOR_BUILDING);
        Debug.Log($"[Construction] {gameObject.name} 건설 시작 ({BuildTime:F1}초)");
    }

    private void Complete()
    {
        IsComplete = true;
        Progress   = 1f;

        SetColor(Color.white);
        ApplyFloorSortingLayer();

        _building?.CompleteBuilding();
        _builder?.OnConstructionComplete();
        _builder = null;

        _workerAssigned = false;
        Debug.Log($"[Construction] {gameObject.name} 건설 완료!");
        Destroy(this);
    }

    void ApplyFloorSortingLayer()
    {
        PathfindingGrid grid = ServiceLocator.Has<PathfindingGrid>()
            ? ServiceLocator.Get<PathfindingGrid>()
            : Object.FindFirstObjectByType<PathfindingGrid>();

        if (grid == null) return;

        int floorIdx = BuildingFloor >= 0 ? BuildingFloor : grid.GetFloorIndex(grid.WorldToCell(transform.position));

        FloorType floorType = floorIdx switch
        {
            0 => FloorType.Floor3,
            1 => FloorType.Floor2,
            _ => FloorType.Floor1,
        };

        var floorObj = GetComponent<FloorObject>();
        if (floorObj != null)
        {
            floorObj.SetFloor(floorType);
            Debug.Log($"[Construction] {gameObject.name} Sorting Layer → {floorType} (FloorIdx:{floorIdx})");
        }
    }

    void OnGUI()
    {
        if (Camera.main == null) return;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        if (screenPos.z < 0) return;

        float x = screenPos.x - 35f;
        float y = Screen.height - screenPos.y - 55f;

        GUI.color = IsBuilding ? Color.yellow : Color.cyan;
        GUI.Label(new Rect(x, y, 120f, 20f),
            IsBuilding ? $"건설 중 {Progress * 100f:F0}%" : "워커 대기 중");
        GUI.color = Color.white;
    }

    private void SetColor(Color color)
    {
        if (_renderers == null) return;
        foreach (var sr in _renderers)
            if (sr != null) sr.color = color;
    }
}

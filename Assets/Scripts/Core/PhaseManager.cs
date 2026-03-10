using UnityEngine;

/// <summary>
/// 준비 페이즈 ↔ 전투 페이즈 전환 관리
///
/// 준비 페이즈: 자원수집 / 건물건설 / 초기화 가능 / 전투유닛 비활성
/// 전투 페이즈: 웨이브 시작 / 유닛 자동스폰 / 초기화 불가
/// </summary>
public class PhaseManager : MonoBehaviour
{
    public static PhaseManager Instance { get; private set; }

    [SerializeField] private PhaseType _currentPhase = PhaseType.None;
    public PhaseType CurrentPhase => _currentPhase;

    public bool IsPrepare => _currentPhase == PhaseType.Prepare;
    public bool IsCombat  => _currentPhase == PhaseType.Combat;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ServiceLocator.Register<PhaseManager>(this);
    }

    void Start()
    {
        // 웨이브 시작 버튼 이벤트 구독
        EventBus.Subscribe<OnWaveStartRequested>(OnWaveStartRequested);
        EventBus.Subscribe<OnResetRequested>(OnResetRequested);
        EventBus.Subscribe<OnAllWavesCleared>(OnAllWavesCleared);

        // 게임 시작 시 준비 페이즈로 진입
        EnterPrepare();
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<OnWaveStartRequested>(OnWaveStartRequested);
        EventBus.Unsubscribe<OnResetRequested>(OnResetRequested);
        EventBus.Unsubscribe<OnAllWavesCleared>(OnAllWavesCleared);
    }

    // -------------------------------------------------------
    // 페이즈 전환
    // -------------------------------------------------------
    public void EnterPrepare()
    {
        if (_currentPhase == PhaseType.Prepare) return;
        _currentPhase = PhaseType.Prepare;

        Debug.Log("[PhaseManager] 준비 페이즈 시작");

        // 자원 노드 전체 재스폰
        ResetAllResourceNodes();

        EventBus.Publish(new OnPhaseChanged { phase = PhaseType.Prepare });
    }

    public void EnterCombat()
    {
        if (_currentPhase == PhaseType.Combat) return;

        // 게임이 Playing 상태일 때만 전투 진입 허용
        if (!GameManager.Instance.IsPlaying)
        {
            Debug.LogWarning("[PhaseManager] Playing 상태가 아니므로 전투 진입 불가");
            return;
        }

        _currentPhase = PhaseType.Combat;

        // 워커 전원 사망 처리
        KillAllWorkers();

        // 생산 건물 유닛 스폰
        SpawnAllProductionUnits();

        Debug.Log("[PhaseManager] 전투 페이즈 시작");
        EventBus.Publish(new OnPhaseChanged { phase = PhaseType.Combat });
    }

    // -------------------------------------------------------
    // 페이즈 전환 보조 메서드
    // -------------------------------------------------------

    // Combat 진입 시 모든 워커 사망 처리
    private void KillAllWorkers()
    {
        if (!ServiceLocator.Has<WorkerAssigner>()) return;

        // 복사본으로 순회 (ForceKill → OnUnitDied → UnregisterWorker로 목록 변경됨)
        var workersCopy = new System.Collections.Generic.List<WorkerUnit>(
            ServiceLocator.Get<WorkerAssigner>().GetAllWorkers()
        );

        foreach (var worker in workersCopy)
        {
            if (worker != null && !worker.IsDead)
                worker.ForceKill();
        }

        Debug.Log($"[PhaseManager] 워커 {workersCopy.Count}명 전투 페이즈 사망 처리");
    }

    // Combat 진입 시 모든 생산 건물에서 유닛 스폰
    private void SpawnAllProductionUnits()
    {
        var buildings = Object.FindObjectsByType<ProductionBuilding>(FindObjectsSortMode.None);
        foreach (var b in buildings)
            b.SpawnUnits();

        Debug.Log($"[PhaseManager] 생산 건물 {buildings.Length}개 유닛 스폰");
    }

    // Prepare 진입 시 자원 노드 전체 초기화
    private void ResetAllResourceNodes()
    {
        var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        foreach (var node in nodes)
            node.ResetNode();

        Debug.Log($"[PhaseManager] 자원 노드 {nodes.Length}개 초기화");
    }

    // -------------------------------------------------------
    // 이벤트 핸들러
    // -------------------------------------------------------

    // 웨이브 시작 버튼 → 전투 페이즈 전환
    private void OnWaveStartRequested(OnWaveStartRequested e)
    {
        if (!IsPrepare) return;
        EnterCombat();
    }

    // 전략 초기화 → 준비 페이즈에서만 허용
    private void OnResetRequested(OnResetRequested e)
    {
        if (!IsPrepare)
        {
            Debug.LogWarning("[PhaseManager] 전투 중에는 초기화 불가");
            return;
        }

        Debug.Log("[PhaseManager] 전략 초기화 실행");
        // PhaseResetter가 이 이벤트를 받아 실제 초기화 수행
    }

    // 모든 웨이브 클리어 → 다음 웨이브 준비를 위해 Prepare로 복귀
    // (마지막 웨이브라면 GameManager가 Clear 처리)
    private void OnAllWavesCleared(OnAllWavesCleared e) { }
}

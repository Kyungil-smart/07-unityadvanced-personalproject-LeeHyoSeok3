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

        Debug.Log("[PhaseManager] 전투 페이즈 시작");
        EventBus.Publish(new OnPhaseChanged { phase = PhaseType.Combat });
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

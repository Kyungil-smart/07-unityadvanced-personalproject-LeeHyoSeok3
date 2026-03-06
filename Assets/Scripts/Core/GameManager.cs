using UnityEngine;

/// <summary>
/// 게임 전체 흐름 총괄
/// - 게임 상태 관리
/// - 본진 파괴 → GameOver
/// - 전체 웨이브 클리어 → Clear
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("게임 상태")]
    [SerializeField] private GameState _currentState = GameState.Ready;
    public GameState CurrentState => _currentState;
    public bool IsPlaying => _currentState == GameState.Playing;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // EventBus만 초기화 (ServiceLocator는 각 컴포넌트가 Awake에서 직접 등록)
        EventBus.Clear();

        ServiceLocator.Register<GameManager>(this);
    }

    void Start()
    {
        EventBus.Subscribe<OnHeadquartersDestroyed>(OnHeadquartersDestroyed);
        EventBus.Subscribe<OnAllWavesCleared>(OnAllWavesCleared);

        ChangeState(GameState.Playing);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<OnHeadquartersDestroyed>(OnHeadquartersDestroyed);
        EventBus.Unsubscribe<OnAllWavesCleared>(OnAllWavesCleared);
    }

    // -------------------------------------------------------
    // 상태 변경
    // -------------------------------------------------------
    public void ChangeState(GameState newState)
    {
        if (_currentState == newState) return;
        _currentState = newState;

        switch (newState)
        {
            case GameState.Playing:
                Time.timeScale = 1f;
                break;
            case GameState.Paused:
                Time.timeScale = 0f;
                break;
            case GameState.GameOver:
                Time.timeScale = 0f;
                EventBus.Publish(new OnGameOver());
                break;
            case GameState.Clear:
                Time.timeScale = 0f;
                EventBus.Publish(new OnStageClear());
                break;
        }

        EventBus.Publish(new OnGameStateChanged { state = newState });
        Debug.Log($"[GameManager] 상태 변경: {newState}");
    }

    public void TogglePause()
    {
        if (_currentState == GameState.Playing)       ChangeState(GameState.Paused);
        else if (_currentState == GameState.Paused)   ChangeState(GameState.Playing);
    }

    // -------------------------------------------------------
    // 이벤트 핸들러
    // -------------------------------------------------------
    private void OnHeadquartersDestroyed(OnHeadquartersDestroyed e)
    {
        if (_currentState != GameState.Playing) return;
        ChangeState(GameState.GameOver);
    }

    private void OnAllWavesCleared(OnAllWavesCleared e)
    {
        if (_currentState != GameState.Playing) return;
        ChangeState(GameState.Clear);
    }
}

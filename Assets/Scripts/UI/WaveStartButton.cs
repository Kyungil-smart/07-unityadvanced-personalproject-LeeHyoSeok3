using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 우측 하단 웨이브 시작 버튼 UI
///
/// - Prepare 페이즈: 버튼 활성화
/// - Combat  페이즈: 버튼 비활성화
/// - 클릭 시: OnWaveStartRequested 이벤트 발행 → PhaseManager.EnterCombat() 호출됨
/// </summary>
public class WaveStartButton : MonoBehaviour
{
    [Header("UI 참조")]
    [SerializeField] private Button _button;

    [Header("Alpha 설정")]
    [SerializeField] private float _activeAlpha   = 1f;
    [SerializeField] private float _inactiveAlpha = 0.4f;

    private CanvasGroup _canvasGroup;

    // -----------------------------------------------------------

    void Awake()
    {
        // 버튼 참조 자동 설정
        if (_button == null) _button = GetComponent<Button>();

        // CanvasGroup 자동 추가 (없으면 생성)
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _button.onClick.AddListener(OnButtonClick);
    }

    void Start()
    {
        EventBus.Subscribe<OnPhaseChanged>(OnPhaseChanged);

        // 초기 상태 반영 (PhaseManager가 Start에서 EnterPrepare 호출하므로 이벤트 수신 전)
        RefreshState(PhaseManager.Instance != null
            ? PhaseManager.Instance.CurrentPhase
            : PhaseType.Prepare);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<OnPhaseChanged>(OnPhaseChanged);
        if (_button != null) _button.onClick.RemoveListener(OnButtonClick);
    }

    // -----------------------------------------------------------
    // 버튼 클릭
    // -----------------------------------------------------------

    public void OnButtonClick()
    {
        // Prepare 페이즈가 아니면 무시
        if (PhaseManager.Instance == null || !PhaseManager.Instance.IsPrepare) return;

        EventBus.Publish(new OnWaveStartRequested());
    }

    // -----------------------------------------------------------
    // 이벤트 핸들러
    // -----------------------------------------------------------

    private void OnPhaseChanged(OnPhaseChanged e)
    {
        RefreshState(e.phase);
    }

    // -----------------------------------------------------------
    // UI 상태 갱신
    // -----------------------------------------------------------

    private void RefreshState(PhaseType phase)
    {
        bool isPrepare = phase == PhaseType.Prepare;

        // 클릭 가능 여부
        _button.interactable = isPrepare;

        // Alpha 적용 (Combat 페이즈에서 반투명하게 표시)
        _canvasGroup.alpha = isPrepare ? _activeAlpha : _inactiveAlpha;
    }
}

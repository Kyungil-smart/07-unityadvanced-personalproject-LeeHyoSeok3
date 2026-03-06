using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 UI 패널
///
/// MenuManager가 활성/비활성 처리
/// OnEnable: 로고 부유 시작
/// OnDisable: 로고 위치 초기화
///
/// ── Hierarchy ────────────────
/// TitleUI  (CanvasGroup)
///   ├── Logo      (Image)
///   ├── BtnStart  (Button)
///   └── BtnQuit   (Button)
/// ─────────────────────────────
/// </summary>
public class TitleUI : MonoBehaviour
{
    [Header("UI 레퍼런스")]
    public Image  logo;
    public Button btnStart;
    public Button btnQuit;

    [Header("로고 부유 애니메이션")]
    public float floatAmplitude = 8f;
    public float floatSpeed     = 1.2f;

    [Header("카메라")]
    [Tooltip("TitleUI 활성 시 카메라가 이동할 월드 좌표")]
    public Camera  sceneCamera;
    public Vector3 cameraPosition = Vector3.zero;
    public float   cameraSize     = 5f;
    public float   cameraMoveSpeed = 3f;

    private RectTransform _logoRect;
    private Vector2       _logoOrigin;
    private bool          _isActive;
    private Coroutine     _cameraMoveCoroutine;

    void Awake()
    {
        if (logo != null)
        {
            _logoRect   = logo.rectTransform;
            _logoOrigin = _logoRect.anchoredPosition;
        }

        btnStart?.onClick.AddListener(OnClickStart);
        btnQuit?.onClick.AddListener(OnClickQuit);
    }

    // 씬 시작 여부 (첫 활성화는 snap, 이후는 lerp)
    private bool _firstEnable = true;

    void OnEnable()
    {
        _isActive = true;

        // 로고 원위치 복원
        if (_logoRect != null)
            _logoRect.anchoredPosition = _logoOrigin;

        if (sceneCamera != null)
        {
            if (_cameraMoveCoroutine != null) StopCoroutine(_cameraMoveCoroutine);

            if (_firstEnable)
            {
                // 씬 시작 시 즉시 스냅
                _firstEnable = false;
                sceneCamera.transform.position = new Vector3(
                    cameraPosition.x, cameraPosition.y,
                    sceneCamera.transform.position.z);
                sceneCamera.orthographicSize = cameraSize;
            }
            else
            {
                // StageSelectUI에서 복귀 시 lerp
                _cameraMoveCoroutine = StartCoroutine(CameraMoveRoutine());
            }
        }
    }

    void OnDisable()
    {
        _isActive = false;

        if (_cameraMoveCoroutine != null)
        {
            StopCoroutine(_cameraMoveCoroutine);
            _cameraMoveCoroutine = null;
        }
    }

    System.Collections.IEnumerator CameraMoveRoutine()
    {
        Vector3 startPos  = sceneCamera.transform.position;
        float   startSize = sceneCamera.orthographicSize;
        Vector3 targetPos = new Vector3(cameraPosition.x, cameraPosition.y, startPos.z);

        float dist     = Vector3.Distance(startPos, targetPos);
        float duration = Mathf.Clamp(dist / Mathf.Max(cameraMoveSpeed, 0.1f), 0.3f, 1.2f);
        float t        = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float ease = Mathf.SmoothStep(0f, 1f, t / duration);
            sceneCamera.transform.position = Vector3.Lerp(startPos, targetPos, ease);
            sceneCamera.orthographicSize   = Mathf.Lerp(startSize, cameraSize, ease);
            yield return null;
        }

        sceneCamera.transform.position = targetPos;
        sceneCamera.orthographicSize   = cameraSize;
        _cameraMoveCoroutine = null;
    }

    void Update()
    {
        if (!_isActive || _logoRect == null) return;
        float offsetY = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        _logoRect.anchoredPosition = _logoOrigin + new Vector2(0f, offsetY);
    }

    void OnClickStart()
    {
        if (MenuManager.Instance == null) return;
        MenuManager.Instance.GoToStageSelect();
    }

    void OnClickQuit()
    {
        MenuManager.Instance?.QuitGame();
    }
}

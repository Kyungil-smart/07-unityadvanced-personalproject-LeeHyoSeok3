using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 화면 페이드 인/아웃 관리자 (완전 독립형)
///
/// - DontDestroyOnLoad로 씬 전환 후에도 유지
/// - FadePanel 자동 생성 (Hierarchy 배치 불필요)
/// - 씬 로드 완료 시 자동 FadeIn (SceneFader 불필요)
/// - 씬 전환 시 FadeOut만 호출하면 나머지 자동 처리
///
/// ── 사용법 ───────────────────────────────────────
/// 최초 씬에 FadeManager 오브젝트 하나만 배치
///
/// // 씬 전환 전 FadeOut 후 로드
/// yield return FadeManager.Instance.FadeOut();
/// SceneLoader.LoadStage(data);
///
/// // FadeIn은 씬 로드 완료 시 자동 실행 ✅
/// ────────────────────────────────────────────────
/// </summary>
public class FadeManager : MonoBehaviour
{
    public static FadeManager Instance { get; private set; }

    [Header("페이드 설정")]
    public float fadeDuration = 0.4f;
    public Color fadeColor    = Color.black;

    [Header("자동 FadeIn 설정")]
    [Tooltip("씬 로드 완료 후 FadeIn 시작까지 대기 시간")]
    public float autoFadeInDelay = 0f;

    private Canvas    _canvas;
    private Image     _fadePanel;
    private Coroutine _fadeCoroutine;

    // -------------------------------------------------------
    // 초기화
    // -------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateFadePanel();
        SetAlpha(1f); // 시작 시 암전

        // 씬 로드 완료 이벤트 구독
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // -------------------------------------------------------
    // 씬 로드 완료 시 자동 FadeIn
    // -------------------------------------------------------
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopCurrentFade();

        // 코루틴 첫 프레임 이전에 즉시 alpha=1 보장
        // → 1프레임이라도 이전 alpha로 렌더링되는 현상 방지
        SetAlpha(1f);

        if (autoFadeInDelay > 0f)
            StartCoroutine(DelayedFadeIn());
        else
            StartFade(1f, 0f, fadeDuration);
    }

    IEnumerator DelayedFadeIn()
    {
        yield return new WaitForSecondsRealtime(autoFadeInDelay);
        StartFade(GetCurrentAlpha(), 0f, fadeDuration);
    }

    // -------------------------------------------------------
    // 공개 API
    // -------------------------------------------------------

    /// <summary>투명 → 검정</summary>
    public Coroutine FadeOut(float duration = -1f) =>
        StartFade(GetCurrentAlpha(), 1f, duration < 0f ? fadeDuration : duration);

    /// <summary>검정 → 투명</summary>
    public Coroutine FadeIn(float duration = -1f) =>
        StartFade(GetCurrentAlpha(), 0f, duration < 0f ? fadeDuration : duration);

    public void FadeOutImmediate() { StopCurrentFade(); SetAlpha(1f); }
    public void FadeInImmediate()  { StopCurrentFade(); SetAlpha(0f); }

    // -------------------------------------------------------
    // 내부
    // -------------------------------------------------------
    Coroutine StartFade(float from, float to, float duration)
    {
        StopCurrentFade();
        _fadeCoroutine = StartCoroutine(FadeRoutine(from, to, duration));
        return _fadeCoroutine;
    }

    void StopCurrentFade()
    {
        if (_fadeCoroutine == null) return;
        StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = null;
    }

    IEnumerator FadeRoutine(float from, float to, float duration)
    {
        SetAlpha(from);
        if (duration <= 0f) { SetAlpha(to); yield break; }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        SetAlpha(to);
        _fadeCoroutine = null;
    }

    void CreateFadePanel()
    {
        var canvasGo = new GameObject("[FadeCanvas]");
        canvasGo.transform.SetParent(transform);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999;

        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("[FadePanel]");
        panelGo.transform.SetParent(canvasGo.transform, false);

        _fadePanel = panelGo.AddComponent<Image>();
        _fadePanel.color        = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
        _fadePanel.raycastTarget = false;

        var rect = panelGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    float GetCurrentAlpha() => _fadePanel != null ? _fadePanel.color.a : 1f;

    void SetAlpha(float alpha)
    {
        if (_fadePanel == null) return;
        var c = _fadePanel.color;
        _fadePanel.color = new Color(c.r, c.g, c.b, alpha);
    }
}
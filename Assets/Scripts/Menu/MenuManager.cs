using System.Collections;
using UnityEngine;

/// <summary>
/// 메인 메뉴 씬 전체 컨트롤러
///
/// ── Hierarchy ────────────────────────────────────
/// Canvas
///   ├── TitleUI       (CanvasGroup + TitleUI.cs)
///   ├── StageSelectUI (CanvasGroup + StageSelectUI.cs)
///   └── FadePanel     ← 가장 마지막 자식, alpha=255
/// MenuManager
/// FadeManager         ← FadePanel 슬롯 연결
/// ─────────────────────────────────────────────────
/// </summary>
public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }

    [Header("UI 패널")]
    public CanvasGroup titleUI;
    public CanvasGroup stageSelectUI;

    [Header("패널 전환 페이드 시간")]
    public float panelFadeDuration = 0.35f;

    private bool _isSwitching = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // 패널 초기 상태 설정 (FadePanel이 암전 상태이므로 화면에 보이지 않음)
        ShowImmediate(titleUI);
        HideImmediate(stageSelectUI);

        // 씬 시작 페이드 인
        FadeManager.Instance?.FadeIn();
    }

    // -------------------------------------------------------
    // 전환 메서드
    // -------------------------------------------------------
    public void GoToStageSelect()
    {
        if (_isSwitching) return;
        StartCoroutine(SwitchPanel(titleUI, stageSelectUI));
    }

    public void GoToTitle()
    {
        if (_isSwitching) return;
        StartCoroutine(SwitchPanel(stageSelectUI, titleUI));
    }

    public void LoadStage(StageData data)
    {
        if (_isSwitching) return;
        StartCoroutine(LoadStageRoutine(data));
    }

    public void QuitGame() => SceneLoader.QuitGame();

    // -------------------------------------------------------
    // 코루틴
    // -------------------------------------------------------
    IEnumerator SwitchPanel(CanvasGroup from, CanvasGroup to)
    {
        _isSwitching = true;

        // 1. from 입력 즉시 차단
        from.interactable   = false;
        from.blocksRaycasts = false;

        // 2. from fade out
        yield return StartCoroutine(FadeCanvasGroup(from, from.alpha, 0f));
        from.gameObject.SetActive(false);

        // 3. to 활성화 (alpha=0에서 시작, SetActive 전에 alpha 설정)
        to.alpha          = 0f;
        to.interactable   = false;
        to.blocksRaycasts = false;
        to.gameObject.SetActive(true);

        // 4. to fade in
        yield return StartCoroutine(FadeCanvasGroup(to, 0f, 1f));

        // 5. 입력 허용
        to.interactable   = true;
        to.blocksRaycasts = true;

        _isSwitching = false;
    }

    IEnumerator LoadStageRoutine(StageData data)
    {
        _isSwitching = true;

        // 현재 패널 입력 즉시 차단
        var visible = stageSelectUI.gameObject.activeSelf ? stageSelectUI : titleUI;
        visible.interactable   = false;
        visible.blocksRaycasts = false;

        // 천천히 암전 후 씬 로드
        // FadeIn은 씬 로드 완료 시 FadeManager가 자동 실행
        yield return FadeManager.Instance?.FadeOut();
        SceneLoader.LoadStage(data);

        // _isSwitching = false 는 씬이 바뀌므로 불필요
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to)
    {
        float t = 0f;
        cg.alpha = from;
        while (t < panelFadeDuration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / panelFadeDuration));
            yield return null;
        }
        cg.alpha = to;
    }

    // -------------------------------------------------------
    // 헬퍼
    // -------------------------------------------------------
    void ShowImmediate(CanvasGroup cg)
    {
        cg.gameObject.SetActive(true);
        cg.alpha          = 1f;
        cg.interactable   = true;
        cg.blocksRaycasts = true;
    }

    void HideImmediate(CanvasGroup cg)
    {
        cg.alpha          = 0f;
        cg.interactable   = false;
        cg.blocksRaycasts = false;
        cg.gameObject.SetActive(false);
    }
}
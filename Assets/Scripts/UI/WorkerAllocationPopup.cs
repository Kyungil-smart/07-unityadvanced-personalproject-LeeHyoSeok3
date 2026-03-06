using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 워커 할당 팝업 (벌목 / 채굴 / 사냥)
///
/// 계층 구조:
///   WorkerAllocationPopup
///     └─ Background  ← background 연결
///           └─ Viewport (Image + Mask) ← viewport 연결
///                 ├─ DecreaseButton  (<)
///                 ├─ CountText       (3/10)
///                 ├─ IncreaseButton  (>)
///                 └─ ConfirmButton   (확인)
/// </summary>
public class WorkerAllocationPopup : MonoBehaviour
{
    [Header("참조")]
    public Button      decreaseButton;
    public Button      increaseButton;
    public Button      confirmButton;
    public TMP_Text    countText;

    [Header("애니메이션")]
    public RectTransform background;
    public RectTransform viewport;           // Viewport (Image + Mask)
    public float         targetWidth      = 300f;
    public float         targetHeight     = 100f;
    public float         viewTargetWidth  = 280f;
    public float         viewTargetHeight = 80f;
    public float         animDuration     = 0.15f;

    private readonly Vector2 INIT_SIZE  = new Vector2(100f, 100f);
    private readonly Vector2 VIEW_INIT  = new Vector2(0f,   0f);

    private int                _selected  = 1;
    private int                _available = 0;
    private System.Action<int> _onConfirm;
    private Coroutine          _animCoroutine;

    void Start()
    {
        decreaseButton?.onClick.AddListener(OnDecrease);
        increaseButton?.onClick.AddListener(OnIncrease);
        confirmButton?.onClick.AddListener(OnConfirm);

        if (PopupManager.Instance != null)
            PopupManager.Instance.Register(gameObject);

        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (PopupManager.Instance != null)
            PopupManager.Instance.Unregister(gameObject);
    }

    public void Open(int available, System.Action<int> onConfirm)
    {
        _available = Mathf.Max(1, available);
        _selected  = 1;
        _onConfirm = onConfirm;

        RefreshUI();
        gameObject.SetActive(true);

        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimateOpen());
    }

    public void Close()
    {
        if (!gameObject.activeSelf) return;
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimateClose());
    }

    IEnumerator AnimateOpen()
    {
        if (background != null) background.sizeDelta = INIT_SIZE;
        if (viewport       != null) viewport.sizeDelta       = VIEW_INIT;

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);

            if (background != null)
                background.sizeDelta = new Vector2(
                    Mathf.Lerp(INIT_SIZE.x, targetWidth,      t),
                    Mathf.Lerp(INIT_SIZE.y, targetHeight,     t)
                );

            if (viewport != null)
                viewport.sizeDelta = new Vector2(
                    Mathf.Lerp(VIEW_INIT.x, viewTargetWidth,  t),
                    Mathf.Lerp(VIEW_INIT.y, viewTargetHeight, t)
                );

            yield return null;
        }

        if (background != null) background.sizeDelta = new Vector2(targetWidth,     targetHeight);
        if (viewport       != null) viewport.sizeDelta       = new Vector2(viewTargetWidth, viewTargetHeight);
        _animCoroutine = null;
    }

    IEnumerator AnimateClose()
    {
        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);

            if (background != null)
                background.sizeDelta = new Vector2(
                    Mathf.Lerp(targetWidth,      INIT_SIZE.x, t),
                    Mathf.Lerp(targetHeight,     INIT_SIZE.y, t)
                );

            if (viewport != null)
                viewport.sizeDelta = new Vector2(
                    Mathf.Lerp(viewTargetWidth,  VIEW_INIT.x, t),
                    Mathf.Lerp(viewTargetHeight, VIEW_INIT.y, t)
                );

            yield return null;
        }

        if (background != null) background.sizeDelta = INIT_SIZE;
        if (viewport       != null) viewport.sizeDelta       = VIEW_INIT;

        gameObject.SetActive(false);
        _animCoroutine = null;
    }

    void OnDecrease()
    {
        _selected = Mathf.Max(1, _selected - 1);
        RefreshUI();
    }

    void OnIncrease()
    {
        _selected = Mathf.Min(_available, _selected + 1);
        RefreshUI();
    }

    void OnConfirm()
    {
        _onConfirm?.Invoke(_selected);
        Close();
    }

    void RefreshUI()
    {
        if (countText != null)
            countText.text = $"{_selected} / {_available}";

        if (decreaseButton != null)
            decreaseButton.interactable = _selected > 1;

        if (increaseButton != null)
            increaseButton.interactable = _selected < _available;
    }
}

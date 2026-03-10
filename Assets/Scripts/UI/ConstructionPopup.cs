using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 건설 목록 팝업
///
/// 계층 구조:
///   ConstructionPopup (GameObject)
///     └─ BackGround
///           └─ ScrollView
///                 └─ Viewport
///                       └─ Content  ← entryContainer 연결
/// </summary>
public class ConstructionPopup : MonoBehaviour
{
    [Header("참조")]
    public GameObject         entryPrefab;
    public Transform          entryContainer;
    public List<BuildingData> buildingList;

    [Header("애니메이션")]
    public RectTransform background;
    public RectTransform scrollView;

    public float bgTargetWidth   = 480f;
    public float bgTargetHeight  = 360f;
    public float svTargetWidth   = 550f;
    public float svTargetHeight  = 635f;
    public float animDuration    = 0.2f;

    private readonly Vector2 INIT_SIZE = new Vector2(100f, 100f);
    private readonly Vector2 SV_INIT   = new Vector2(0f,   0f);

    private Coroutine _animCoroutine;
    private readonly List<GameObject> _entries = new();

    void Start()
    {
        if (PopupManager.Instance != null)
            PopupManager.Instance.Register(gameObject);
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        // 남아있는 엔트리 오브젝트 정리
        foreach (var e in _entries)
            if (e != null) Destroy(e);
        _entries.Clear();

        if (PopupManager.Instance != null)
            PopupManager.Instance.Unregister(gameObject);
    }

    public void Open()
    {
        gameObject.SetActive(true);
        Refresh();

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
        // 초기 크기 설정
        if (background != null) background.sizeDelta = INIT_SIZE;
        if (scrollView  != null) scrollView.sizeDelta  = SV_INIT;

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);

            if (background != null)
                background.sizeDelta = new Vector2(
                    Mathf.Lerp(INIT_SIZE.x, bgTargetWidth,  t),
                    Mathf.Lerp(INIT_SIZE.y, bgTargetHeight, t)
                );

            if (scrollView != null)
                scrollView.sizeDelta = new Vector2(
                    Mathf.Lerp(SV_INIT.x, svTargetWidth,  t),
                    Mathf.Lerp(SV_INIT.y, svTargetHeight, t)
                );

            yield return null;
        }

        if (background != null) background.sizeDelta = new Vector2(bgTargetWidth,  bgTargetHeight);
        if (scrollView  != null) scrollView.sizeDelta  = new Vector2(svTargetWidth,  svTargetHeight);

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
                    Mathf.Lerp(bgTargetWidth,  INIT_SIZE.x, t),
                    Mathf.Lerp(bgTargetHeight, INIT_SIZE.y, t)
                );

            if (scrollView != null)
                scrollView.sizeDelta = new Vector2(
                    Mathf.Lerp(svTargetWidth,  SV_INIT.x, t),
                    Mathf.Lerp(svTargetHeight, SV_INIT.y, t)
                );

            yield return null;
        }

        if (background != null) background.sizeDelta = INIT_SIZE;
        if (scrollView  != null) scrollView.sizeDelta  = SV_INIT;

        gameObject.SetActive(false);
        _animCoroutine = null;
    }

    void Refresh()
    {
        // 기존 엔트리 비활성화 (Destroy 대신 재사용)
        foreach (var e in _entries)
            if (e != null) e.SetActive(false);

        int index = 0;
        foreach (var data in buildingList)
        {
            if (data == null) continue;

            GameObject go;
            if (index < _entries.Count && _entries[index] != null)
            {
                // 기존 오브젝트 재활성화
                go = _entries[index];
                go.SetActive(true);
            }
            else
            {
                // 부족하면 새로 생성
                go = Instantiate(entryPrefab, entryContainer);
                if (index < _entries.Count)
                    _entries[index] = go;
                else
                    _entries.Add(go);
            }

            var entry = go.GetComponent<BuildingEntryButton>();
            entry?.Init(data, this);
            index++;
        }
    }

    public void SelectBuilding(BuildingData data)
    {
        Close();

        if (!ServiceLocator.Has<BuildingPlacer>())
        {
            Debug.LogWarning("[ConstructionPopup] BuildingPlacer 없음");
            return;
        }

        ServiceLocator.Get<BuildingPlacer>().StartPlacement(data);
    }
}

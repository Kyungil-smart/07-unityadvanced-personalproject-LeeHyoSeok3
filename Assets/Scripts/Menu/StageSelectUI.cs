using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 선택 UI 패널
///
/// OnEnable:  마지막 플레이 스테이지 복원 → 카메라 스냅 → 오브젝트 스폰
/// OnDisable: 스폰된 오브젝트 제거
///
/// ── Hierarchy ────────────────────────────────────
/// StageSelectUI  (CanvasGroup)
///   ├── BtnPrev         ← ◀ 화면 좌측
///   ├── BtnNext         ← ▶ 화면 우측
///   ├── PanelRank       ← StageInfoPanel.cs 부착
///   │     ├── TxtRankLabel  "Rank" (고정)
///   │     └── StarsGroup
///   │           ├── StarImage0
///   │           ├── StarImage1
///   │           └── StarImage2
///   └── PanelStage      ← StagePanel.cs 부착
///         ├── TxtStageNumber  "Stage 1" (우 상단)
///         └── BtnGo      "Go!"
/// ─────────────────────────────────────────────────
///
/// 씬 오브젝트:
/// [Camera]          → sceneCamera
/// [SceneObjectRoot] → sceneObjectRoot (하단 애니 오브젝트 부모)
/// </summary>
public class StageSelectUI : MonoBehaviour
{
    [Header("스테이지 목록")]
    public StageData[] stages;

    [Header("UI 레퍼런스")]
    public Button         btnPrev;
    public Button         btnNext;
    public Button         btnBack;   // 뒤로가기 → TitleUI 복귀
    public StageInfoPanel infoPanel;
    public StagePanel     stagePanel;

    [Header("카메라")]
    public Camera sceneCamera;
    public float  cameraMoveSpeed = 3f;

    [Header("하단 애니 오브젝트")]
    [Tooltip("UI에 가리지 않는 하단 영역의 부모 Transform")]
    public Transform sceneObjectRoot;

    // -------------------------------------------------------
    private int                       _currentIndex;
    private Coroutine                 _cameraMoveCoroutine;
    private readonly List<GameObject> _spawnedObjects = new();

    void Awake()
    {
        btnPrev?.onClick.AddListener(OnClickPrev);
        btnNext?.onClick.AddListener(OnClickNext);
        btnBack?.onClick.AddListener(OnClickBack);

        if (stagePanel != null)
            stagePanel.OnGoClicked = OnClickGo;
    }

    void OnEnable()
    {
        if (stages == null || stages.Length == 0) return;

        // 마지막 플레이 스테이지 복원
        _currentIndex = PlayerPrefs.GetInt("LastStageIndex", 0);
        _currentIndex = Mathf.Clamp(_currentIndex, 0, stages.Length - 1);

        // 카메라 부드럽게 이동 (TitleUI에서 전환 시 lerp)
        StartCameraMove(_currentIndex);
        RefreshAll(_currentIndex);
    }

    void OnDisable()
    {
        // 스폰된 스테이지 오브젝트 제거
        foreach (var obj in _spawnedObjects)
            if (obj != null) Destroy(obj);
        _spawnedObjects.Clear();

        // 카메라 이동 코루틴 중단
        if (_cameraMoveCoroutine != null)
        {
            StopCoroutine(_cameraMoveCoroutine);
            _cameraMoveCoroutine = null;
        }
    }

    // -------------------------------------------------------
    // 화살표 버튼
    // -------------------------------------------------------
    void OnClickPrev()
    {
        if (stages.Length == 0) return;
        ChangeStage((_currentIndex - 1 + stages.Length) % stages.Length);
    }

    void OnClickNext()
    {
        if (stages.Length == 0) return;
        ChangeStage((_currentIndex + 1) % stages.Length);
    }

    void ChangeStage(int newIndex)
    {
        _currentIndex = newIndex;
        RefreshAll(_currentIndex);
        StartCameraMove(_currentIndex);
    }

    // -------------------------------------------------------
    // 뒤로가기 버튼
    // -------------------------------------------------------
    void OnClickBack()
    {
        MenuManager.Instance?.GoToTitle();
    }

    // -------------------------------------------------------
    // Go! 버튼
    // -------------------------------------------------------
    void OnClickGo()
    {
        if (stages.Length == 0) return;
        PlayerPrefs.SetInt("LastStageIndex", _currentIndex);
        MenuManager.Instance?.LoadStage(stages[_currentIndex]);
    }

    // -------------------------------------------------------
    // UI 갱신
    // -------------------------------------------------------
    void RefreshAll(int index)
    {
        if (stages.Length == 0) return;

        StageData data        = stages[index];
        int       stageNumber = index + 1;

        infoPanel?.Refresh(data);
        stagePanel?.Refresh(data, stageNumber);

        bool multi = stages.Length > 1;
        btnPrev?.gameObject.SetActive(multi);
        btnNext?.gameObject.SetActive(multi);

        SpawnSceneObjects(data);
    }

    // -------------------------------------------------------
    // 카메라
    // -------------------------------------------------------
    void SnapCamera(int index)
    {
        if (sceneCamera == null || stages.Length == 0) return;
        var data = stages[index];
        sceneCamera.transform.position = new Vector3(
            data.cameraPosition.x,
            data.cameraPosition.y,
            sceneCamera.transform.position.z);
        sceneCamera.orthographicSize = data.cameraSize;
    }

    void StartCameraMove(int index)
    {
        if (sceneCamera == null || stages.Length == 0) return;
        if (_cameraMoveCoroutine != null) StopCoroutine(_cameraMoveCoroutine);
        _cameraMoveCoroutine = StartCoroutine(CameraMoveRoutine(stages[index]));
    }

    IEnumerator CameraMoveRoutine(StageData data)
    {
        Vector3 startPos  = sceneCamera.transform.position;
        float   startSize = sceneCamera.orthographicSize;
        Vector3 targetPos = new Vector3(
            data.cameraPosition.x,
            data.cameraPosition.y,
            startPos.z);
        float targetSize  = data.cameraSize;

        float dist     = Vector3.Distance(startPos, targetPos);
        float duration = Mathf.Clamp(dist / Mathf.Max(cameraMoveSpeed, 0.1f), 0.3f, 1.2f);
        float t        = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float ease = Mathf.SmoothStep(0f, 1f, t / duration);
            sceneCamera.transform.position = Vector3.Lerp(startPos, targetPos, ease);
            sceneCamera.orthographicSize   = Mathf.Lerp(startSize, targetSize, ease);
            yield return null;
        }

        sceneCamera.transform.position = targetPos;
        sceneCamera.orthographicSize   = targetSize;
        _cameraMoveCoroutine = null;
    }

    // -------------------------------------------------------
    // 하단 애니 오브젝트 스폰
    // -------------------------------------------------------
    void SpawnSceneObjects(StageData data)
    {
        foreach (var obj in _spawnedObjects)
            if (obj != null) Destroy(obj);
        _spawnedObjects.Clear();

        if (data?.sceneObjectPrefabs == null) return;

        Transform root = sceneObjectRoot != null ? sceneObjectRoot : transform;
        foreach (var prefab in data.sceneObjectPrefabs)
        {
            if (prefab == null) continue;
            _spawnedObjects.Add(Instantiate(prefab, root));
        }
    }
}
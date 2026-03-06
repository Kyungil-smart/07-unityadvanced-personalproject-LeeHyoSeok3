using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 자원 버튼 클릭 시 가장 가까운 자원 노드로 카메라 포커스
/// 선택 표시 UI(원형 아이콘 등)를 해당 오브젝트 위에 표시
///
/// 계층 구조:
///   ResourceFocusUI
///     └─ SelectionIndicator  ← selectionIndicator 연결 (Image 등)
/// </summary>
public class ResourceFocusUI : MonoBehaviour
{
    [Header("참조")]
    public GameObject selectionIndicator;
    public Canvas     canvas;              // 루트 Canvas 연결

    [Header("포커스 줌")]
    public float focusZoom = 4f;

    private CameraController _camCtrl;
    private Camera           _cam;
    private Transform        _selectedTarget;
    public  ResourceNode     SelectedNode => _selectedTarget?.GetComponent<ResourceNode>();

    void Awake()
    {
        ServiceLocator.Register<ResourceFocusUI>(this);
    }

    void Start()
    {
        _cam     = Camera.main;
        _camCtrl = Object.FindFirstObjectByType<CameraController>();

        if (selectionIndicator != null)
            selectionIndicator.SetActive(false);
    }

    void LateUpdate()
    {
        if (_selectedTarget == null || selectionIndicator == null) return;

        selectionIndicator.SetActive(true);

        // 스프라이트 center 기준 위치
        Vector3 worldCenter = GetSpriteCenter(_selectedTarget);

        // 위치 갱신
        Vector3 screenPos = _cam.WorldToScreenPoint(worldCenter);
        selectionIndicator.transform.position = screenPos;

        // 크기 갱신 (줌 변화 대응)
        ApplyIndicatorSize(_selectedTarget);
    }

    Vector3 GetSpriteCenter(Transform target)
    {
        var sr = target.GetComponent<SpriteRenderer>();
        if (sr != null) return sr.bounds.center;
        return target.position;
    }

    // -------------------------------------------------------
    // 외부 호출
    // -------------------------------------------------------

    /// <summary>카메라에서 가장 가까운 노드로 포커스</summary>
    public bool FocusNearest<T>(out T result) where T : ResourceNode
    {
        result = null;

        var nodes = Object.FindObjectsByType<T>(FindObjectsSortMode.None)
                          .Where(n => n != null && !n.IsDeplete)
                          .ToArray();

        if (nodes.Length == 0) return false;

        Vector3 camPos = _cam != null ? _cam.transform.position : Vector3.zero;
        result = nodes.OrderBy(n => Vector3.Distance(n.transform.position, camPos))
                      .First();

        Select(result.transform);
        return true;
    }

    public void ClearFocus()
    {
        _selectedTarget = null;
        if (selectionIndicator != null)
            selectionIndicator.SetActive(false);

        _camCtrl?.CancelFocus();
    }

    // -------------------------------------------------------
    // 내부
    // -------------------------------------------------------
    void Select(Transform target)
    {
        _selectedTarget = target;
        Vector3 focusPos = GetSpriteCenter(target);
        _camCtrl?.FocusOn(focusPos, focusZoom);
        ApplyIndicatorSize(target);
    }

    void ApplyIndicatorSize(Transform target)
    {
        if (selectionIndicator == null || canvas == null) return;

        var rt = selectionIndicator.GetComponent<RectTransform>();
        if (rt == null) return;

        var sr = target.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        // 스프라이트 픽셀 크기 → 월드 크기
        float ppu      = sr.sprite.pixelsPerUnit;
        float worldW   = sr.sprite.rect.width  / ppu * target.localScale.x;
        float worldH   = sr.sprite.rect.height / ppu * target.localScale.y;

        // 월드 1유닛 = 스크린 몇 픽셀
        float screenPixelsPerUnit = Screen.height / (_cam.orthographicSize * 2f);

        // 스크린 픽셀 크기
        float screenW = worldW * screenPixelsPerUnit;
        float screenH = worldH * screenPixelsPerUnit;

        // Canvas scaleFactor로 나눠서 Canvas 좌표로 변환
        rt.sizeDelta = new Vector2(screenW / canvas.scaleFactor, screenH / canvas.scaleFactor);
    }
}

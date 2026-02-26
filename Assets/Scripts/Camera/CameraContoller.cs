using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    // -------------------------------------------------------
    // 커서 설정
    // -------------------------------------------------------
    [Header("커서 설정")]
    public Texture2D cursorTexture;
    [Tooltip("커서 클릭 기준점 (스프라이트 내 픽셀 좌표)")]
    public Vector2 cursorHotspot = Vector2.zero;

    [Header("WASD 이동 설정")]
    public float wasdSpeed = 5f;

    // -------------------------------------------------------
    // 엣지 스크롤 설정
    // -------------------------------------------------------
    [Header("엣지 스크롤 설정")]
    [Tooltip("화면 가장자리 감지 두께 (픽셀)")]
    public float edgeThickness = 20f;
    public float scrollSpeed = 5f;

    // -------------------------------------------------------
    // 줌 설정
    // -------------------------------------------------------
    [Header("줌 설정")]
    public float zoomSpeed = 2f;
    public float minZoom = 3f;    // 최대 확대
    public float maxZoom = 10f;   // 최대 축소
    public float zoomSmoothSpeed = 8f;

    private float _targetZoom;

    // -------------------------------------------------------
    // 카메라 범위 설정
    // -------------------------------------------------------
    [Header("카메라 범위 설정")]
    public string bgColorLayerName = "BG Color";
    [Tooltip("카메라가 BG Color 경계 안쪽으로 유지될 여백")]
    public float boundsPadding = 1f;

    // -------------------------------------------------------
    // Private
    // -------------------------------------------------------
    private Tilemap _bgTilemap;
    private float _minX, _maxX, _minY, _maxY; // 카메라 이동 가능 범위
    private Camera _cam;
    private bool _boundsReady = false;

    void Start()
    {
        _cam = Camera.main;
        _targetZoom = _cam.orthographicSize;

        // 커서 적용
        if (cursorTexture != null)
            Cursor.SetCursor(cursorTexture, cursorHotspot, CursorMode.Auto);

        // BG Color 타일맵 탐색
        foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (tm.gameObject.name == bgColorLayerName)
            {
                _bgTilemap = tm;
                break;
            }
        }

        CalculateBounds();
    }

    // -------------------------------------------------------
    // BG Color 범위 기반 카메라 이동 한계 계산
    // -------------------------------------------------------
    void CalculateBounds()
    {
        if (_bgTilemap == null)
        {
            Debug.LogWarning($"[CameraController] '{bgColorLayerName}' 레이어를 찾을 수 없습니다.");
            return;
        }

        _bgTilemap.CompressBounds();
        Vector3 min = _bgTilemap.CellToWorld(_bgTilemap.cellBounds.min);
        Vector3 max = _bgTilemap.CellToWorld(_bgTilemap.cellBounds.max);

        // 카메라 반 크기 계산 (카메라가 경계 밖으로 못 나가도록)
        float camHalfH = _cam.orthographicSize;
        float camHalfW = _cam.orthographicSize * _cam.aspect;

        _minX = min.x + camHalfW + boundsPadding;
        _maxX = max.x - camHalfW - boundsPadding;
        _minY = min.y + camHalfH + boundsPadding;
        _maxY = max.y - camHalfH - boundsPadding;

        _boundsReady = true;
    }

    void Update()
    {
        HandleZoom();
        HandleWASD();
        HandleEdgeScroll();
    }

    // -------------------------------------------------------
    // 줌 처리 (커서 위치 기준)
    // -------------------------------------------------------
    void HandleZoom()
    {
        if (Mouse.current == null) return;

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            _targetZoom -= scroll * zoomSpeed * 0.01f;
            _targetZoom = Mathf.Clamp(_targetZoom, minZoom, maxZoom);
        }

        // orthographicSize가 변하는 경우에만 커서 보정
        if (!Mathf.Approximately(_cam.orthographicSize, _targetZoom))
        {
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

            // Lerp 적용 전 커서 월드 좌표
            Vector3 cursorBefore = _cam.ScreenToWorldPoint(
                new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f)
            );

            // Lerp 적용
            _cam.orthographicSize = Mathf.Lerp(
                _cam.orthographicSize,
                _targetZoom,
                Time.deltaTime * zoomSmoothSpeed
            );

            // Lerp 적용 후 커서 월드 좌표
            Vector3 cursorAfter = _cam.ScreenToWorldPoint(
                new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f)
            );

            // 매 프레임 커서 위치 보정
            Vector3 offset = cursorBefore - cursorAfter;
            transform.position += new Vector3(offset.x, offset.y, 0f);
        }

        // 범위 재계산 및 클램프
        CalculateBounds();

        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, _minX, _maxX);
        pos.y = Mathf.Clamp(pos.y, _minY, _maxY);
        transform.position = pos;
    }

    // -------------------------------------------------------
    // WASD 이동
    // -------------------------------------------------------
    void HandleWASD()
    {
        if (!_boundsReady) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        float dx = 0f;
        float dy = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)  dx = -1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) dx =  1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)  dy = -1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)    dy =  1f;

        if (dx == 0f && dy == 0f) return;

        Vector3 newPos = transform.position + new Vector3(dx, dy, 0f) * wasdSpeed * Time.deltaTime;

        newPos.x = Mathf.Clamp(newPos.x, _minX, _maxX);
        newPos.y = Mathf.Clamp(newPos.y, _minY, _maxY);

        transform.position = newPos;
    }

    // -------------------------------------------------------
    // 엣지 스크롤
    // -------------------------------------------------------
    void HandleEdgeScroll()
    {
        if (!_boundsReady) return;

        // New Input System으로 마우스 위치 가져오기
        if (Mouse.current == null) return;
        Vector2 mousePos = Mouse.current.position.ReadValue();
        float screenW = Screen.width;
        float screenH = Screen.height;

        float dx = 0f;
        float dy = 0f;

        // 좌우 감지
        if (mousePos.x <= edgeThickness)
            dx = -1f;
        else if (mousePos.x >= screenW - edgeThickness)
            dx = 1f;

        // 상하 감지
        if (mousePos.y <= edgeThickness)
            dy = -1f;
        else if (mousePos.y >= screenH - edgeThickness)
            dy = 1f;

        // 이동 속도에 거리 비율 적용 (가장자리에 가까울수록 빠름)
        if (dx < 0) dx *= Mathf.InverseLerp(edgeThickness, 0f, mousePos.x) + 0.3f;
        if (dx > 0) dx *= Mathf.InverseLerp(screenW - edgeThickness, screenW, mousePos.x) + 0.3f;
        if (dy < 0) dy *= Mathf.InverseLerp(edgeThickness, 0f, mousePos.y) + 0.3f;
        if (dy > 0) dy *= Mathf.InverseLerp(screenH - edgeThickness, screenH, mousePos.y) + 0.3f;

        // 카메라 이동
        Vector3 newPos = transform.position + new Vector3(dx, dy, 0f) * (scrollSpeed * Time.deltaTime);

        // BG Color 범위 내로 클램프
        newPos.x = Mathf.Clamp(newPos.x, _minX, _maxX);
        newPos.y = Mathf.Clamp(newPos.y, _minY, _maxY);

        transform.position = newPos;
    }

    // -------------------------------------------------------
    // 에디터 시각화
    // -------------------------------------------------------
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !_boundsReady) return;

        // 카메라 이동 가능 범위
        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        Vector3 center = new Vector3((_minX + _maxX) / 2f, (_minY + _maxY) / 2f, 0f);
        Vector3 size = new Vector3(_maxX - _minX, _maxY - _minY, 0f);
        Gizmos.DrawWireCube(center, size);
    }
}
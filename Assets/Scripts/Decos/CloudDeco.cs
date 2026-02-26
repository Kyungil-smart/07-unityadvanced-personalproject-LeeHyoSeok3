using UnityEngine;
using UnityEngine.Tilemaps;

public class CloudDeco : MonoBehaviour
{
    public enum MoveDirection { Left = -1, Right = 1 }

    [Header("이동 방향 설정")]
    public MoveDirection direction = MoveDirection.Left;
    public float moveSpeed = 0.5f;
    public float bobAmplitude = 0.03f;
    public float bobSpeed = 0.8f;

    [Header("시작 좌표 오프셋")]
    [Tooltip("BG Color X 범위 내 시작 위치 비율 (0 = 왼쪽 끝, 1 = 오른쪽 끝)")]
    [Range(0f, 1f)]
    public float startOffsetX = 0f;
    [Tooltip("게임오브젝트 기본 Y에서 추가로 이동할 Y 오프셋")]
    public float startOffsetY = 0f;

    [Header("투명도 설정")]
    public float transparentAlpha = 0.0f;
    public float normalAlpha = 1.0f;
    public float fadeInSpeed = 3.0f;   // 투명해지는 속도 (빠르게)
    public float fadeOutSpeed = 1.0f;  // 불투명해지는 속도 (느리게)
    [Tooltip("겹침이 끝난 후 불투명해지기까지 대기 시간")]
    public float holdTime = 0.5f;      // 겹침 종료 후 유지 시간

    [Header("타일맵 설정")]
    public string bgColorLayerName = "BG Color";
    public float checkRadius = 0.3f;

    private SpriteRenderer _spriteRenderer;
    private Tilemap _bgTilemap;
    private Tilemap[] _allTilemaps;

    private float _bobOffset;
    private float _minX, _maxX;
    private float _startY;

    // 깜빡임 방지용
    private bool _isOverlapping = false;
    private float _overlapEndTime = 0f;  // 마지막으로 겹침이 감지된 시간
    private float _currentAlpha;

    void Start()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _bobOffset = Random.Range(0f, Mathf.PI * 2f);
        _startY = transform.position.y + startOffsetY;
        _currentAlpha = normalAlpha;

        foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (tm.gameObject.name == bgColorLayerName)
            {
                _bgTilemap = tm;
                break;
            }
        }

        _allTilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

        if (_bgTilemap != null)
        {
            _bgTilemap.CompressBounds();
            Vector3 min = _bgTilemap.CellToWorld(_bgTilemap.cellBounds.min);
            Vector3 max = _bgTilemap.CellToWorld(_bgTilemap.cellBounds.max);
            _minX = min.x;
            _maxX = max.x;

            // 시작 X: BG Color 범위 내 비율로 위치 지정 (0 = 왼쪽 끝, 1 = 오른쪽 끝)
            float startX = Mathf.Lerp(_minX, _maxX, startOffsetX);
            transform.position = new Vector3(startX, _startY, transform.position.z);
        }
    }

    void Update()
    {
        HandleMovement();
        HandleTransparency();
    }

    // -------------------------------------------------------
    // 이동 처리
    // -------------------------------------------------------
    void HandleMovement()
    {
        if (_bgTilemap == null) return;

        float dx = (int)direction * moveSpeed * Time.deltaTime;
        float dy = Mathf.Sin(Time.time * bobSpeed + _bobOffset) * bobAmplitude;

        transform.position += new Vector3(dx, 0f, 0f);
        transform.position = new Vector3(
            transform.position.x,
            _startY + dy,
            transform.position.z
        );

        ClampOrWrap(force: false);
    }

    void ClampOrWrap(bool force)
    {
        float x = transform.position.x;

        if (direction == MoveDirection.Left && (x < _minX || force))
            transform.position = new Vector3(_maxX, _startY, transform.position.z);
        else if (direction == MoveDirection.Right && (x > _maxX || force))
            transform.position = new Vector3(_minX, _startY, transform.position.z);
    }

    // -------------------------------------------------------
    // 투명도 처리 (깜빡임 방지)
    // -------------------------------------------------------
    void HandleTransparency()
    {
        bool detected = CheckTileOverlap();

        if (detected)
        {
            // 겹침 감지 → 마지막 감지 시간 갱신
            _overlapEndTime = Time.time + holdTime;
            _isOverlapping = true;
        }
        else
        {
            // 겹침 미감지 → holdTime이 지났을 때만 해제
            if (Time.time >= _overlapEndTime)
                _isOverlapping = false;
        }

        // 상태에 따라 다른 속도로 알파값 전환
        float targetAlpha = _isOverlapping ? transparentAlpha : normalAlpha;
        float speed = _isOverlapping ? fadeInSpeed : fadeOutSpeed;

        _currentAlpha = Mathf.Lerp(_currentAlpha, targetAlpha, Time.deltaTime * speed);

        // 거의 다 됐으면 정확한 값으로 고정 (미세 떨림 방지)
        if (Mathf.Abs(_currentAlpha - targetAlpha) < 0.01f)
            _currentAlpha = targetAlpha;

        Color color = _spriteRenderer.color;
        color.a = _currentAlpha;
        _spriteRenderer.color = color;
    }

    // -------------------------------------------------------
    // 타일맵과의 겹침 검사 (BG Color 제외)
    // -------------------------------------------------------
    bool CheckTileOverlap()
    {
        foreach (var tilemap in _allTilemaps)
        {
            if (tilemap == null) continue;
            if (tilemap.gameObject.name == bgColorLayerName) continue;

            Vector3Int cellPos = tilemap.WorldToCell(transform.position);

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    Vector3Int checkPos = cellPos + new Vector3Int(x, y, 0);
                    TileBase tile = tilemap.GetTile(checkPos);

                    if (tile != null)
                    {
                        Vector3 tileWorldPos = tilemap.GetCellCenterWorld(checkPos);
                        float distance = Vector2.Distance(transform.position, tileWorldPos);

                        if (distance < checkRadius)
                            return true;
                    }
                }
            }
        }
        return false;
    }

    void OnDrawGizmos()
    {
        // 항상 표시: 겹침 감지 범위 (속이 반투명한 원)
        Color fillColor = new Color(0f, 1f, 1f, 0.15f);
        Color outlineColor = new Color(0f, 1f, 1f, 0.8f);

        Gizmos.color = fillColor;
        Gizmos.DrawSphere(transform.position, checkRadius);

        Gizmos.color = outlineColor;
        Gizmos.DrawWireSphere(transform.position, checkRadius);
    }

    void OnDrawGizmosSelected()
    {
        // 선택했을 때 추가 표시: BG Color 이동 범위
        if (Application.isPlaying && _bgTilemap != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(
                new Vector3(_minX, _startY, 0),
                new Vector3(_maxX, _startY, 0)
            );

            // 이동 범위 양 끝 표시
            Gizmos.DrawWireSphere(new Vector3(_minX, _startY, 0), 0.1f);
            Gizmos.DrawWireSphere(new Vector3(_maxX, _startY, 0), 0.1f);
        }
    }
}
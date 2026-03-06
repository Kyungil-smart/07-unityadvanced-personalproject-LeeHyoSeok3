using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 타일맵을 이름으로 자동 탐색 — Prefab에서도 연결 가능
///
/// Inspector에서 직접 연결하거나,
/// 비워두면 Start()에서 타일맵 이름으로 자동 탐색
/// </summary>
[RequireComponent(typeof(FloorObject))]
public class FloorDetector : MonoBehaviour
{
    [Header("층별 타일맵 (비워두면 이름으로 자동 탐색)")]
    public Tilemap floor2Tilemap;
    public Tilemap floor3Tilemap;
    public Tilemap stairTilemap;

    [Header("타일맵 오브젝트 이름 (자동 탐색용)")]
    public string floor2Name = "Elevated Ground_1";
    public string floor3Name = "Elevated Ground_2";
    public string stairName  = "Stair";

    [Header("발 위치 오프셋")]
    public float additionalOffsetY = 0f;

    private FloorObject    _floorObject;
    private SpriteRenderer _spriteRenderer;
    private bool           _wasOnStair;

    void Awake()
    {
        _floorObject    = GetComponent<FloorObject>();
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Start()
    {
        AutoFindTilemaps();
    }

    // -------------------------------------------------------
    // 타일맵 자동 탐색
    // -------------------------------------------------------
    void AutoFindTilemaps()
    {
        if (floor2Tilemap == null) floor2Tilemap = FindTilemap(floor2Name);
        if (floor3Tilemap == null) floor3Tilemap = FindTilemap(floor3Name);
        if (stairTilemap  == null) stairTilemap  = FindTilemap(stairName);

        // 탐색 결과 로그
        Debug.Log($"[FloorDetector] {name} 타일맵 자동 탐색 완료 " +
                  $"Floor2:{floor2Tilemap?.name ?? "없음"} " +
                  $"Floor3:{floor3Tilemap?.name ?? "없음"} " +
                  $"Stair:{stairTilemap?.name  ?? "없음"}");
    }

    static Tilemap FindTilemap(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return null;

        var go = GameObject.Find(objectName);
        if (go == null)
        {
            Debug.LogWarning($"[FloorDetector] '{objectName}' 오브젝트 없음");
            return null;
        }

        var tilemap = go.GetComponent<Tilemap>();
        if (tilemap == null)
            Debug.LogWarning($"[FloorDetector] '{objectName}'에 Tilemap 컴포넌트 없음");

        return tilemap;
    }

    // -------------------------------------------------------
    // 매 프레임 감지
    // -------------------------------------------------------
    void LateUpdate()
    {
        Vector3 footPos = GetFootPosition();

        bool isOnStair = IsOnTilemap(stairTilemap, footPos);
        if (isOnStair != _wasOnStair)
        {
            _floorObject.SetOnStair(isOnStair);
            _wasOnStair = isOnStair;
        }

        FloorType detected = DetectCurrentFloor();
        if (_floorObject.CurrentFloor != detected)
            _floorObject.SetFloor(detected);
    }

    public FloorType DetectCurrentFloor()
    {
        Vector3 footPos = GetFootPosition();
        if (IsOnTilemap(floor3Tilemap, footPos)) return FloorType.Floor3;
        if (IsOnTilemap(floor2Tilemap, footPos)) return FloorType.Floor2;
        return FloorType.Floor1;
    }

    // -------------------------------------------------------
    // 헬퍼
    // -------------------------------------------------------
    Vector3 GetFootPosition()
    {
        var sr = _spriteRenderer != null
            ? _spriteRenderer
            : GetComponentInChildren<SpriteRenderer>();

        if (sr == null || sr.sprite == null)
            return transform.position;

        Sprite  sprite = sr.sprite;
        Vector2 pivot  = new Vector2(
            sprite.pivot.x / sprite.rect.width,
            sprite.pivot.y / sprite.rect.height);

        Bounds bounds = sr.bounds;
        float  worldX = bounds.min.x + bounds.size.x * pivot.x;
        float  worldY = bounds.min.y + bounds.size.y * pivot.y;

        return new Vector3(worldX, worldY + additionalOffsetY, 0f);
    }

    bool IsOnTilemap(Tilemap tilemap, Vector3 worldPos)
    {
        if (tilemap == null) return false;
        return tilemap.HasTile(tilemap.WorldToCell(worldPos));
    }

    void OnDrawGizmos()
    {
        Vector3 pos = GetFootPosition();
        Gizmos.color = _wasOnStair ? Color.cyan : Color.yellow;
        Gizmos.DrawSphere(pos, 0.08f);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(pos + Vector3.left  * 0.15f, pos + Vector3.right * 0.15f);
        Gizmos.DrawLine(pos + Vector3.up    * 0.15f, pos + Vector3.down  * 0.15f);
    }
}

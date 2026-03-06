using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

/// <summary>
/// 건물 배치 시스템 (자유 배치 + 건설 시간)
/// </summary>
public class BuildingPlacer : MonoBehaviour
{
    [Header("배치 가능 영역 (지형 타일맵)")]
    [Tooltip("배치 허용 타일맵 (Flat Ground 등)")]
    public Tilemap[] buildableTilemaps;

    [Header("배치 불가 영역 (절벽 타일맵)")]
    [Tooltip("이 타일맵 위에는 건물 설치 불가 (절벽, 낭떠러지 등)")]
    public Tilemap[] blockedTilemaps;

    [Header("미리보기 색상")]
    public Color validColor   = new Color(0.5f, 1f, 0.5f, 0.5f);
    public Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.5f);

    private BuildingData   _pendingData;
    private GameObject     _previewGo;
    private SpriteRenderer _previewRenderer;
    private Vector2        _previewSize;
    private bool           _isPlacing;
    public  bool           IsPlacing => _isPlacing;
    private Vector3        _lastPlacePos;
    private bool           _lastCanPlace;

    private readonly List<BuildingBase> _placedBuildings = new();

    void Awake()
    {
        ServiceLocator.Register<BuildingPlacer>(this);
    }

    void Update()
    {
        if (!_isPlacing) return;
        if (!PhaseManager.Instance.IsPrepare) { CancelPlacement(); return; }

        UpdatePreview();
        HandleInput();
    }

    // -------------------------------------------------------
    // 배치 시작
    // -------------------------------------------------------
    public void StartPlacement(BuildingData data)
    {
        if (!PhaseManager.Instance.IsPrepare) return;

        CancelPlacement();
        _pendingData = data;
        _isPlacing   = true;

        _previewGo       = Instantiate(data.prefab);
        _previewRenderer = _previewGo.GetComponentInChildren<SpriteRenderer>();

        // 프리팹 원본 콜라이더에서 크기 읽기 (비활성화 전에 측정)
        // BoxCollider2D → size 직접 읽기 / 그 외 → bounds.size
        _previewSize = GetColliderSize(data.prefab);

        // 미리보기 콜라이더/스크립트 비활성화
        foreach (var col in _previewGo.GetComponentsInChildren<Collider2D>())
            col.enabled = false;
        foreach (var script in _previewGo.GetComponentsInChildren<BuildingBase>())
            script.enabled = false;
        foreach (var script in _previewGo.GetComponentsInChildren<FloorObject>())
            script.enabled = false;
    }

    // -------------------------------------------------------
    // 미리보기 업데이트
    // -------------------------------------------------------
    void UpdatePreview()
    {
        if (_previewGo == null) return;

        Vector3 mouseWorld = GetMouseWorldPos();
        mouseWorld.z = 0f;

        _previewGo.transform.position = mouseWorld;
        _lastPlacePos = mouseWorld;
        _lastCanPlace = CanPlaceAt(mouseWorld);

        SetPreviewColor(_lastCanPlace ? validColor : invalidColor);
    }

    // -------------------------------------------------------
    // 입력 처리
    // -------------------------------------------------------
    void HandleInput()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (_lastCanPlace)
                ConfirmPlacement(_lastPlacePos);
            else
                Debug.Log("[BuildingPlacer] 배치 불가 위치");
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
            CancelPlacement();

        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            CancelPlacement();
    }

    // -------------------------------------------------------
    // 배치 확정
    // -------------------------------------------------------
    void ConfirmPlacement(Vector3 position)
    {
        if (ServiceLocator.Has<ResourceInventory>())
        {
            var inventory = ServiceLocator.Get<ResourceInventory>();
            if (!inventory.CanAffordAll(_pendingData.GetBuildCost()))
            {
                Debug.LogWarning($"[BuildingPlacer] {_pendingData.buildingName} 자원 부족");
                return;
            }
            inventory.SpendAll(_pendingData.GetBuildCost());
        }
        else
        {
            Debug.Log("[BuildingPlacer] ResourceInventory 없음 → 자원 체크 스킵 (디버그)");
        }

        var buildingGo   = Instantiate(_pendingData.prefab, position, Quaternion.identity);
        var building     = buildingGo.GetComponent<BuildingBase>();
        var construction = buildingGo.AddComponent<BuildingConstruction>();

        // 배치 위치의 층 인덱스 계산 (Flat Ground 오염 방지: 상위 타일맵 우선)
        int floorIndex = 0;
        Tilemap topTilemap = GetTopTilemap(position);
        if (topTilemap != null && buildableTilemaps != null)
        {
            for (int i = 0; i < buildableTilemaps.Length; i++)
            {
                if (buildableTilemaps[i] == topTilemap) { floorIndex = i; break; }
            }
        }
        construction.Init(_pendingData.buildTime, floorIndex);

        if (building != null) _placedBuildings.Add(building);

        if (ServiceLocator.Has<ConstructionAssigner>())
            ServiceLocator.Get<ConstructionAssigner>().AssignWorkerTo(construction);
        else
            Debug.LogWarning("[BuildingPlacer] ConstructionAssigner 없음");

        Destroy(_previewGo);
        _previewGo   = null;
        _isPlacing   = false;
        _pendingData = null;
    }

    // -------------------------------------------------------
    // 취소
    // -------------------------------------------------------
    public void CancelPlacement()
    {
        if (_previewGo != null) { Destroy(_previewGo); _previewGo = null; }
        _isPlacing   = false;
        _pendingData = null;
    }

    // -------------------------------------------------------
    // 배치 가능 여부 검사
    // -------------------------------------------------------
    bool CanPlaceAt(Vector3 worldPos)
    {
        float inset = 0.1f;
        float hx    = _previewSize.x * 0.5f - inset;
        float hy    = _previewSize.y * 0.5f - inset;

        Vector3[] samplePoints = new Vector3[]
        {
            worldPos,
            worldPos + new Vector3(-hx, -hy, 0f),
            worldPos + new Vector3( hx, -hy, 0f),
            worldPos + new Vector3(-hx,  hy, 0f),
            worldPos + new Vector3( hx,  hy, 0f),
        };


        // 1. 각 포인트의 실제 층(가장 높은 우선순위 타일맵)이 모두 동일해야 배치 가능
        //    buildableTilemaps 배열을 높은 층 → 낮은 층 순으로 설정해야 함
        //    ex) [0]=Elevated Ground_2, [1]=Elevated Ground_1, [2]=Flat Ground
        //    Flat Ground가 전역으로 깔려있어도 상위 층 타일이 우선 적용됨
        if (buildableTilemaps != null && buildableTilemaps.Length > 0)
        {
            Tilemap firstFloor = GetTopTilemap(samplePoints[0]);
            if (firstFloor == null) return false;  // 어떤 타일맵 위도 아님

            foreach (var point in samplePoints)
            {
                if (GetTopTilemap(point) != firstFloor) return false;
            }
        }


        // 2. 절벽 타일 위인지 확인 (하나라도 걸치면 불가)
        if (blockedTilemaps != null && blockedTilemaps.Length > 0)
        {
            foreach (var point in samplePoints)
                if (IsOnAnyTilemap(blockedTilemaps, point)) return false;
        }

        // 3. 기존 건물과 겹침 확인 (레이어 무관)
        Vector2 checkSize = _previewSize * 0.85f;
        var hits = Physics2D.OverlapBoxAll(worldPos, checkSize, 0f);
        foreach (var hit in hits)
        {
            if (hit.gameObject == _previewGo) continue;
            if (hit.GetComponent<BuildingBase>()         != null) return false;
            if (hit.GetComponent<BuildingConstruction>() != null) return false;
        }

        return true;
    }

    bool IsOnAnyTilemap(Tilemap[] tilemaps, Vector3 worldPos)
    {
        foreach (var tilemap in tilemaps)
        {
            if (tilemap == null) continue;
            if (tilemap.HasTile(tilemap.WorldToCell(worldPos))) return true;
        }
        return false;
    }

    /// <summary>
    /// 해당 위치에서 가장 높은 우선순위(배열 앞쪽) 타일맵 반환
    /// buildableTilemaps는 높은 층 → 낮은 층 순으로 설정
    /// ex) [0]=3층, [1]=2층, [2]=Flat Ground
    /// Flat Ground가 전역으로 깔려있어도 상위 층 타일이 먼저 매칭됨
    /// </summary>
    Tilemap GetTopTilemap(Vector3 worldPos)
    {
        foreach (var tilemap in buildableTilemaps)
        {
            if (tilemap == null) continue;
            if (tilemap.HasTile(tilemap.WorldToCell(worldPos)))
                return tilemap;
        }
        return null;
    }


    // -------------------------------------------------------
    // 전략 초기화
    // -------------------------------------------------------
    public void ResetAllBuildings()
    {
        foreach (var b in _placedBuildings)
            if (b != null) Destroy(b.gameObject);
        _placedBuildings.Clear();
    }

    public IReadOnlyList<BuildingBase> PlacedBuildings => _placedBuildings;

    // -------------------------------------------------------
    // 헬퍼
    // -------------------------------------------------------
    Vector3 GetMouseWorldPos()
    {
        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 world  = Camera.main.ScreenToWorldPoint(screen);
        world.z = 0f;
        return world;
    }

    void SetPreviewColor(Color color)
    {
        if (_previewRenderer != null) _previewRenderer.color = color;
    }

    /// <summary>
    /// 프리팹 원본의 콜라이더 크기를 읽음
    /// BoxCollider2D → size * localScale
    /// 그 외 Collider2D → 인스턴스 생성 후 bounds 측정 후 즉시 제거
    /// </summary>
    Vector2 GetColliderSize(GameObject prefab)
    {
        // BoxCollider2D는 인스턴스 없이 size 직접 읽기 가능
        var box = prefab.GetComponentInChildren<BoxCollider2D>();
        if (box != null)
        {
            Vector3 scale = box.transform.lossyScale;
            return new Vector2(
                box.size.x * Mathf.Abs(scale.x),
                box.size.y * Mathf.Abs(scale.y));
        }

        // 그 외 (PolygonCollider2D 등) → 임시 인스턴스로 bounds 측정
        var temp = Instantiate(prefab);
        temp.SetActive(false);
        var cols = temp.GetComponentsInChildren<Collider2D>();

        Vector2 size = Vector2.one;
        if (cols.Length > 0)
        {
            // SetActive(true) 잠깐 켜서 Physics 초기화
            temp.SetActive(true);
            Physics2D.SyncTransforms();

            Bounds b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++)
                b.Encapsulate(cols[i].bounds);
            size = new Vector2(b.size.x, b.size.y);
        }
        else
        {
            Debug.LogWarning($"[BuildingPlacer] {prefab.name} Collider2D 없음 → 기본 크기 1x1 사용");
        }

        Destroy(temp);
        return size;
    }
}

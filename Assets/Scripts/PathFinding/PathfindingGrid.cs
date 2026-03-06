using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 타일맵 기반 A* 길찾기 설정
///
/// 계단 상단/하단 판별 (타일맵 분리 없이):
///   아래 셀(y-1)도 계단 → 현재 셀은 StairTop    (상위 층과 연결)
///   위  셀(y+1)도 계단 → 현재 셀은 StairBottom  (하위 층과 연결)
///
/// walkableTilemaps 배열 순서: 높은 층 → 낮은 층
///   [0] Elevated Ground_2 (3층)
///   [1] Elevated Ground_1 (2층)
///   [2] Flat Ground       (1층)
/// </summary>
[DefaultExecutionOrder(-50)]
public class PathfindingGrid : MonoBehaviour
{
    [Header("이동 가능 타일맵 (높은 층 → 낮은 층 순서)")]
    public Tilemap[] walkableTilemaps;

    [Header("계단 타일맵")]
    public Tilemap[] stairTilemaps;

    [Header("이동 불가 타일맵 (절벽 등)")]
    public Tilemap[] blockedTilemaps;

    [Header("장애물 레이어 (선택 - 비워두면 BuildingBase로 자동 감지)")]
    public LayerMask obstacleLayer;

    [Header("길찾기 설정")]
    public float cellSize      = 1f;
    public bool  allowDiagonal = false;

    void Awake()
    {
        ServiceLocator.Register<PathfindingGrid>(this);
        Debug.Log("[PathfindingGrid] ServiceLocator 등록 완료");
    }

    // -------------------------------------------------------
    // 좌표 변환
    // -------------------------------------------------------
    public Vector3Int WorldToCell(Vector3 worldPos)
    {
        foreach (var tm in walkableTilemaps)
            if (tm != null) return tm.WorldToCell(worldPos);
        return Vector3Int.zero;
    }

    public Vector3 CellToWorld(Vector3Int cellPos)
    {
        foreach (var tm in walkableTilemaps)
            if (tm != null) return tm.GetCellCenterWorld(cellPos);
        return Vector3.zero;
    }

    // -------------------------------------------------------
    // 계단 판별
    // -------------------------------------------------------
    public bool IsOnStair(Vector3Int cellPos)
    {
        if (stairTilemaps == null) return false;
        foreach (var tm in stairTilemaps)
            if (tm != null && tm.HasTile(cellPos)) return true;
        return false;
    }

    /// <summary>
    /// 아래 셀(y-1)도 계단 → 상단 타일 (상위 층과 연결)
    /// </summary>
    public bool IsStairTop(Vector3Int cellPos)
    {
        if (!IsOnStair(cellPos)) return false;
        return IsOnStair(cellPos + Vector3Int.down);
    }

    /// <summary>
    /// 위 셀(y+1)도 계단 → 하단 타일 (하위 층과 연결)
    /// </summary>
    public bool IsStairBottom(Vector3Int cellPos)
    {
        if (!IsOnStair(cellPos)) return false;
        return IsOnStair(cellPos + Vector3Int.up);
    }

    // -------------------------------------------------------
    // 층 인덱스
    // -------------------------------------------------------
    /// <summary>
    /// 셀의 좌/우 중 Flat Ground(최하위층)를 제외한 상위 타일맵이 있는 방향 반환
    /// +1 = 오른쪽, -1 = 왼쪽, 0 = 못찾음
    /// </summary>
    // -------------------------------------------------------
    // 계단 Physics Shape 기반 진입점 계산
    // -------------------------------------------------------

    /// <summary>
    /// 계단 셀의 삼각형 Physics Shape에서 빗변 두 끝점(진입점/진출점) 반환
    /// hyp0 : 빗변 끝점 A (상위층 쪽)
    /// hyp1 : 빗변 끝점 B (하위층 쪽)
    /// 반환값 false → 해당 셀의 shape를 찾지 못함
    /// </summary>
    public bool GetStairHypotenusePoints(Vector3Int cellPos,
        out Vector3 hyp0, out Vector3 hyp1)
    {
        hyp0 = hyp1 = Vector3.zero;

        if (stairTilemaps == null) return false;

        foreach (var tm in stairTilemaps)
        {
            if (tm == null) continue;
            var composite = tm.GetComponent<CompositeCollider2D>();
            if (composite == null) continue;

            Vector3 cellCenter = tm.GetCellCenterWorld(cellPos);

            // CompositeCollider2D의 모든 path를 순회
            for (int i = 0; i < composite.pathCount; i++)
            {
                int count = composite.GetPathPointCount(i);
                if (count < 3) continue;

                var pts = new Vector2[count];
                composite.GetPath(i, pts);

                // path의 중심이 이 셀 중심과 가까운지 확인
                Vector2 centroid = Vector2.zero;
                foreach (var p in pts) centroid += p;
                centroid /= count;

                if (Vector2.Distance(centroid, cellCenter) > cellSize) continue;

                // 삼각형 꼭짓점에서 직각 찾기
                // 직각이 아닌 두 꼭짓점 = 빗변 양 끝점
                int rightAngleIdx = FindRightAngleVertex(pts);
                if (rightAngleIdx < 0)
                {
                    // 직각 못 찾으면 가장 긴 변의 두 끝점 사용
                    hyp0 = pts[0]; hyp1 = pts[1];
                    float maxLen = 0;
                    for (int a = 0; a < count; a++)
                    for (int b = a + 1; b < count; b++)
                    {
                        float len = Vector2.Distance(pts[a], pts[b]);
                        if (len > maxLen) { maxLen = len; hyp0 = pts[a]; hyp1 = pts[b]; }
                    }
                }
                else
                {
                    // 직각 꼭짓점 제외한 두 점 = 빗변
                    int a = (rightAngleIdx + 1) % count;
                    int b = (rightAngleIdx + 2) % count;
                    hyp0 = pts[a];
                    hyp1 = pts[b];
                }

                // hyp0 = y가 높은 점 (상위층), hyp1 = y가 낮은 점 (하위층)
                if (hyp0.y < hyp1.y) { var tmp = hyp0; hyp0 = hyp1; hyp1 = tmp; }

                // 타일 y 사이즈의 절반만큼 y 조정
                float halfTileY = tm.cellSize.y * 0.5f;
                hyp0 = new Vector3(hyp0.x, hyp0.y - halfTileY, 0f);
                hyp1 = new Vector3(hyp1.x, hyp1.y + halfTileY, 0f);

                return true;
            }
        }

        return false;
    }

    int FindRightAngleVertex(Vector2[] pts)
    {
        for (int i = 0; i < pts.Length; i++)
        {
            Vector2 prev = pts[(i - 1 + pts.Length) % pts.Length];
            Vector2 curr = pts[i];
            Vector2 next = pts[(i + 1) % pts.Length];

            Vector2 v1 = (prev - curr).normalized;
            Vector2 v2 = (next - curr).normalized;

            float dot = Vector2.Dot(v1, v2);
            if (Mathf.Abs(dot) < 0.1f) return i; // 거의 직각
        }
        return -1;
    }

    public int GetFloorIndex(Vector3Int cellPos)
    {
        if (IsOnStair(cellPos)) return -1;

        if (walkableTilemaps != null)
            for (int i = 0; i < walkableTilemaps.Length; i++)
                if (walkableTilemaps[i] != null && walkableTilemaps[i].HasTile(cellPos))
                    return i;

        return int.MaxValue;
    }

    // -------------------------------------------------------
    // 계단이 특정 층에서 진입 가능한지 판별
    //
    // 인접 탐색 방식 대신 계단 타일 쌍(Top/Bottom) 구조로 직접 추론:
    //   StairTop    → 상위 층에서 진입  (더 낮은 FloorIndex)
    //   StairBottom → 하위 층에서 진입  (더 높은 FloorIndex)
    //
    // 연결 층은 계단 쌍 전체(Top+Bottom) 주변 2칸 범위에서 수집
    // → 계단 옆에 지형 타일이 바로 붙어있지 않아도 동작
    // -------------------------------------------------------
    public bool IsStairValidForFloor(Vector3Int stairCell, int floorIndex)
    {
        if (floorIndex < 0) return true;  // 이미 계단 위 → 허용

        bool top    = IsStairTop(stairCell);
        bool bottom = IsStairBottom(stairCell);

        // 계단 쌍의 루트 셀(Top) 찾기
        Vector3Int topCell    = top    ? stairCell : stairCell + Vector3Int.up;
        Vector3Int bottomCell = bottom ? stairCell : stairCell + Vector3Int.down;

        // Top 주변 인접 지형 층 수집 (상위 층 연결)
        int topFloor    = GetAdjacentFloor(topCell,    excludeStair: true, preferLow: true);
        // Bottom 주변 인접 지형 층 수집 (하위 층 연결)
        int bottomFloor = GetAdjacentFloor(bottomCell, excludeStair: true, preferLow: false);

        if (top && !bottom)
            return floorIndex == topFloor;

        if (bottom && !top)
            return floorIndex == bottomFloor;

        // 단독 계단 → 양쪽 허용
        return floorIndex == topFloor || floorIndex == bottomFloor;
    }

    /// <summary>
    /// 계단 쌍이 연결하는 두 층을 반환
    ///
    /// StairTop    주변 직접 인접(1칸) → 상위 층 (낮은 FloorIndex)
    /// StairBottom 주변 직접 인접(1칸) → 하위 층 (낮은 FloorIndex, Flat Ground 제외)
    ///
    /// Flat Ground가 전역으로 깔려있어도 오염되지 않도록
    /// StairTop/Bottom 각각의 직접 인접 셀만 사용
    /// </summary>
    public HashSet<int> GetStairPairFloors(Vector3Int stairCell)
    {
        var result = new HashSet<int>();

        // 계단 쌍의 Top/Bottom 셀 찾기
        Vector3Int topCell    = stairCell;
        Vector3Int bottomCell = stairCell;

        if (IsStairTop(stairCell))
        {
            topCell    = stairCell;
            bottomCell = stairCell + Vector3Int.down;
        }
        else if (IsStairBottom(stairCell))
        {
            bottomCell = stairCell;
            topCell    = stairCell + Vector3Int.up;
        }

        // StairTop 직접 인접 → 상위 층 (가장 낮은 FloorIndex)
        int upperFloor = GetDirectAdjacentFloor(topCell, preferLow: true);

        // StairBottom 직접 인접 → 하위 층
        // Flat Ground(마지막 인덱스) 제외 우선, 없으면 포함
        int lowerFloor = GetDirectAdjacentFloor(bottomCell, preferLow: false, excludeFloor: upperFloor);

        if (upperFloor >= 0 && upperFloor < int.MaxValue) result.Add(upperFloor);
        if (lowerFloor >= 0 && lowerFloor < int.MaxValue) result.Add(lowerFloor);

        return result;
    }

    /// <summary>
    /// 셀의 직접 인접(4방향 1칸)에서 지형 층 인덱스 반환
    /// preferLow=true → 가장 낮은 FloorIndex (상위 층)
    /// preferLow=false → 가장 높은 FloorIndex 중 excludeFloor 제외
    /// </summary>
    /// <summary>
    /// 좌우(x축) 1~2칸 양쪽 모두 탐색
    /// 가장 상위 층(낮은 FloorIndex) 반환
    /// </summary>
    int GetDirectAdjacentFloor(Vector3Int cell, bool preferLow, int excludeFloor = -999)
    {
        int result = int.MaxValue; // 항상 가장 상위 층(낮은 FloorIndex) 반환

        for (int dist = 1; dist <= 2; dist++)
        {
            // 좌우 둘다 무조건 검사
            foreach (var dir in new[] { Vector3Int.right, Vector3Int.left })
            {
                var neighbor = cell + dir * dist;
                if (IsOnStair(neighbor)) continue;
                int fi = GetFloorIndex(neighbor);
                if (fi < 0 || fi == int.MaxValue) continue;
                if (fi == excludeFloor) continue;

                // 더 상위 층(낮은 FloorIndex) 우선
                if (fi < result) result = fi;
            }
        }

        return result;
    }

    void CollectStairPair(Vector3Int cell, HashSet<Vector3Int> visited)
    {
        if (!IsOnStair(cell) || visited.Contains(cell)) return;
        visited.Add(cell);
        CollectStairPair(cell + Vector3Int.up,   visited);
        CollectStairPair(cell + Vector3Int.down, visited);
    }

    /// <summary>
    /// 셀 주변(4방향 + 대각 + 2칸)에서 인접 지형 층 인덱스 반환
    /// preferLow=true → 가장 낮은 인덱스(상위 층)
    /// preferLow=false → 가장 높은 인덱스(하위 층)
    /// </summary>
    int GetAdjacentFloor(Vector3Int cell, bool excludeStair, bool preferLow)
    {
        int result = preferLow ? int.MaxValue : -1;

        // 4방향 + 대각 + 2칸 거리까지 탐색
        for (int dy = -2; dy <= 2; dy++)
        for (int dx = -2; dx <= 2; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            var neighbor = cell + new Vector3Int(dx, dy, 0);
            if (excludeStair && IsOnStair(neighbor)) continue;

            int fi = GetFloorIndex(neighbor);
            if (fi < 0 || fi == int.MaxValue) continue;

            if (preferLow  && fi < result) result = fi;
            if (!preferLow && fi > result) result = fi;
        }

        return result == int.MaxValue || result == -1 ? 0 : result;
    }

    // -------------------------------------------------------
    // 이동 가능 여부
    // -------------------------------------------------------
    public bool IsWalkable(Vector3Int cellPos)
    {
        // 1. 계단은 절벽보다 우선 (계단과 절벽이 겹치면 계단이 이김)
        if (IsOnStair(cellPos)) return true;

        // 2. 절벽 체크
        if (blockedTilemaps != null)
            foreach (var tm in blockedTilemaps)
                if (tm != null && tm.HasTile(cellPos)) return false;

        // 3. 지형 타일 위인지
        bool onGround = false;
        if (walkableTilemaps != null)
            foreach (var tm in walkableTilemaps)
                if (tm != null && tm.HasTile(cellPos)) { onGround = true; break; }

        if (!onGround) return false;

        // 4. 건물 콜라이더 체크
        {
            Vector3 worldPos  = CellToWorld(cellPos);
            float   checkSize = cellSize * 0.8f;
            var     hits      = Physics2D.OverlapBoxAll(worldPos, Vector2.one * checkSize, 0f,
                                    obstacleLayer != 0 ? obstacleLayer : Physics2D.AllLayers);
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                if (hit.GetComponent<TilemapCollider2D>()   != null) continue;
                if (hit.GetComponent<CompositeCollider2D>() != null) continue;
                if (obstacleLayer != 0) return false;
                // BuildingBase/BuildingConstruction은 경로를 막지 않음
            }
        }

        return true;
    }

    // -------------------------------------------------------
    // 인접 셀
    // -------------------------------------------------------
    public List<Vector3Int> GetNeighbors(Vector3Int cell)
    {
        var neighbors = new List<Vector3Int>();

        int  currentFloor   = GetFloorIndex(cell);
        bool currentIsStair = IsOnStair(cell);
        bool currentIsTop   = IsStairTop(cell);
        bool currentIsBot   = IsStairBottom(cell);

        var dirs = new List<Vector3Int>
        {
            new Vector3Int( 1,  0, 0), new Vector3Int(-1,  0, 0),
            new Vector3Int( 0,  1, 0), new Vector3Int( 0, -1, 0),
        };

        // 대각선: allowDiagonal 설정 시에만
        if (allowDiagonal)
        {
            dirs.Add(new Vector3Int( 1,  1, 0));
            dirs.Add(new Vector3Int(-1,  1, 0));
            dirs.Add(new Vector3Int( 1, -1, 0));
            dirs.Add(new Vector3Int(-1, -1, 0));
        }

        // 계단 쌍의 상위/하위 연결 층 미리 계산
        int upperFloor = -1, lowerFloor = -1;
        if (currentIsStair)
        {
            var pair = GetStairPairFloors(cell);
            foreach (var f in pair)
            {
                if (upperFloor < 0 || f < upperFloor) upperFloor = f;
                if (lowerFloor < 0 || f > lowerFloor) lowerFloor = f;
            }
        }

        foreach (var dir in dirs)
        {
            var neighbor = cell + dir;
            if (!IsWalkable(neighbor)) continue;

            // 대각선 모서리 통과 방지
            if (dir.x != 0 && dir.y != 0)
            {
                bool side1 = IsWalkable(cell + new Vector3Int(dir.x, 0, 0));
                bool side2 = IsWalkable(cell + new Vector3Int(0, dir.y, 0));
                if (!side1 && !side2) continue;
            }

            bool neighborIsStair = IsOnStair(neighbor);
            bool neighborIsTop   = IsStairTop(neighbor);
            bool neighborIsBot   = IsStairBottom(neighbor);
            int  neighborFloor   = GetFloorIndex(neighbor);

            // -----------------------------------------------
            // 지형 → 지형: 같은 층만 허용
            // -----------------------------------------------
            if (!currentIsStair && !neighborIsStair)
            {
                if (currentFloor != neighborFloor) continue;
            }

            // -----------------------------------------------
            // 지형 → 계단 진입 규칙
            //   상위층(upperFloor) → StairTop으로만 진입
            //   하위층(lowerFloor) → StairBottom으로만 진입
            //   수평(x축)만 허용
            // -----------------------------------------------
            if (!currentIsStair && neighborIsStair)
            {
                // 수직 방향 진입 차단
                if (dir.x == 0) continue;

                var pair     = GetStairPairFloors(neighbor);
                int upFloor  = int.MaxValue, dnFloor = -1;
                foreach (var f in pair)
                {
                    if (f < upFloor) upFloor = f;
                    if (f > dnFloor) dnFloor = f;
                }

                // 현재 층과 연결된 계단인지 확인
                if (!pair.Contains(currentFloor)) continue;

                // 상위층 → StairTop으로만, 하위층 → StairBottom으로만
                if (currentFloor == upFloor && !neighborIsTop) continue;
                if (currentFloor == dnFloor && !neighborIsBot) continue;
            }

            // -----------------------------------------------
            // 계단 → 계단: Bottom→Top 또는 Top→Bottom만 허용
            // -----------------------------------------------
            if (currentIsStair && neighborIsStair)
            {
                // 수평 이동 차단
                if (dir.x != 0) continue;

                // Bottom→Top 또는 Top→Bottom만 허용
                bool validUp   = currentIsBot && !currentIsTop && neighborIsTop && !neighborIsBot;
                bool validDown = currentIsTop && !currentIsBot && neighborIsBot && !neighborIsTop;
                if (!validUp && !validDown) continue;
            }

            // -----------------------------------------------
            // 계단 → 지형 진출 규칙
            //   StairTop   → 상위층으로만 수평 진출
            //   StairBottom → 하위층으로만 수평 진출
            // -----------------------------------------------
            if (currentIsStair && !neighborIsStair)
            {
                // 수직 방향 차단
                if (dir.x == 0) continue;

                var pair    = GetStairPairFloors(cell);
                int upFloor = int.MaxValue, dnFloor = -1;
                foreach (var f in pair)
                {
                    if (f < upFloor) upFloor = f;
                    if (f > dnFloor) dnFloor = f;
                }

                if (!pair.Contains(neighborFloor)) continue;

                // StairTop   → 상위층으로만
                // StairBottom → 하위층으로만
                if (currentIsTop && !currentIsBot && neighborFloor != upFloor) continue;
                if (currentIsBot && !currentIsTop && neighborFloor != dnFloor) continue;
            }

            neighbors.Add(neighbor);
        }

        return neighbors;
    }
}

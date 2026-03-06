using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PathfindingGrid 디버그 시각화
///
/// Scene 뷰에서 각 셀의 상태를 색상으로 표시
///   초록  → Walkable (이동 가능)
///   파랑  → Stair    (계단)
///   빨강  → Blocked  (이동 불가)
///   노랑  → Obstacle (건물 등)
///
/// G 키 → 워커 주변 셀 상태 콘솔 출력
/// </summary>
public class PathfindingDebugVisualizer : MonoBehaviour
{
    [Header("시각화 범위")]
    public Vector2Int rangeMin = new Vector2Int(-20, -20);
    public Vector2Int rangeMax = new Vector2Int( 20,  20);

    [Header("시각화 옵션")]
    public bool  showWalkable   = true;
    public bool  showBlocked    = true;
    public bool  showStairs     = true;
    public bool  showObstacles  = true;
    public float alpha          = 0.35f;

    [Header("셀 좌표 표시")]
    public bool showCellCoords = true;
    public bool showFloorIndex = true;

    private PathfindingGrid _grid;
    private GUIStyle        _coordStyle;

    void Start()
    {
        _grid = ServiceLocator.Has<PathfindingGrid>()
            ? ServiceLocator.Get<PathfindingGrid>()
            : FindFirstObjectByType<PathfindingGrid>();
    }

    // H 키 → 워커 현재 위치 기준 계단 진입 가능 여부 상세 출력
    void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current?.gKey.wasPressedThisFrame == true)
            LogWorkerCells();
        if (UnityEngine.InputSystem.Keyboard.current?.hKey.wasPressedThisFrame == true)
            LogStairAccess();
        if (UnityEngine.InputSystem.Keyboard.current?.jKey.wasPressedThisFrame == true)
            LogFloorIndex();
    }

    // J 키 → 워커 위치 + 주변 셀의 FloorIndex 출력
    void LogFloorIndex()
    {
        if (_grid == null) return;
        var workers = FindObjectsByType<WorkerUnit>(FindObjectsSortMode.None);
        foreach (var w in workers)
        {
            if (w == null) continue;
            Vector3Int cell = _grid.WorldToCell(w.transform.position);
            Debug.Log($"[FloorIdx] {w.name}  셀:{cell}  FloorIdx:{_grid.GetFloorIndex(cell)}");

            // 5x5 범위 FloorIndex 출력
            for (int dy = -3; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
            {
                var c  = cell + new Vector3Int(dx, dy, 0);
                int fi = _grid.GetFloorIndex(c);
                bool stair = _grid.IsOnStair(c);
                bool top   = _grid.IsStairTop(c);
                bool bot   = _grid.IsStairBottom(c);
                bool walk  = _grid.IsWalkable(c);

                if (!walk && !stair) continue; // 완전 불가 셀 스킵

                string stairStr = stair ? $"STAIR(top:{top},bot:{bot})" : "";
                Debug.Log($"  셀:{c}  FloorIdx:{(fi == int.MaxValue ? "NONE" : fi.ToString())}  walk:{walk}  {stairStr}");
            }
        }
    }

    void LogStairAccess()
    {
        if (_grid == null) { Debug.LogWarning("[PathViz] PathfindingGrid 없음"); return; }

        var workers = FindObjectsByType<WorkerUnit>(FindObjectsSortMode.None);
        foreach (var w in workers)
        {
            if (w == null) continue;
            Vector3Int cell      = _grid.WorldToCell(w.transform.position);
            int        floorIdx  = _grid.GetFloorIndex(cell);
            Debug.Log($"[StairDebug] {w.name}  셀:{cell}  층:{floorIdx}  world:{w.transform.position:F2}");

            // 넓은 범위에서 계단 탐색
            for (int dy = -8; dy <= 8; dy++)
            for (int dx = -8; dx <= 8; dx++)
            {
                var c = cell + new Vector3Int(dx, dy, 0);
                if (!_grid.IsOnStair(c)) continue;

                bool isTop    = _grid.IsStairTop(c);
                bool isBottom = _grid.IsStairBottom(c);
                bool valid    = _grid.IsStairValidForFloor(c, floorIdx);

                // 이 계단이 GetNeighbors에서 걸러지는지 시뮬레이션
                bool blockedByRule = false;
                string blockReason = "";

                if (isBottom)
                {
                    // StairBottom → 현재 셀이 StairTop이어야 진입 가능
                    // 하지만 여기선 지형에서 계단으로 가는 경우
                    blockReason = "StairBottom: 지형→직접진입 차단";
                    blockedByRule = true;
                }
                else if (isTop && !valid)
                {
                    blockReason = $"StairTop: 층{floorIdx} 유효하지 않음";
                    blockedByRule = true;
                }

                Debug.Log($"  계단:{c} Top:{isTop} Bottom:{isBottom} Valid:{valid} " +
                          $"Blocked:{blockedByRule} ({blockReason})  world:{_grid.CellToWorld(c):F1}");
            }
        }
    }

    void LogWorkerCells()
    {
        if (_grid == null) { Debug.LogWarning("[PathViz] PathfindingGrid 없음"); return; }

        var workers = FindObjectsByType<WorkerUnit>(FindObjectsSortMode.None);
        foreach (var w in workers)
        {
            if (w == null) continue;
            Vector3Int cell      = _grid.WorldToCell(w.transform.position);
            int        floorIdx  = _grid.GetFloorIndex(cell);
            Debug.Log($"[PathViz] {w.name} 위치 셀:{cell}  층인덱스:{floorIdx}  world:{w.transform.position:F2}");

            // 넓은 범위에서 계단 셀 탐색 및 인접 층 출력
            int searchRange = 10;
            bool foundStair = false;
            for (int dy = -searchRange; dy <= searchRange; dy++)
            for (int dx = -searchRange; dx <= searchRange; dx++)
            {
                var c = cell + new Vector3Int(dx, dy, 0);
                if (!_grid.IsOnStair(c)) continue;

                foundStair = true;

                // 인접 4방향의 층 인덱스 출력
                string adj = "";
                foreach (var dir in new[]{ Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down })
                {
                    int fi = _grid.GetFloorIndex(c + dir);
                    adj += $" {dir}→{(fi == -1 ? "STAIR" : fi == int.MaxValue ? "NONE" : fi.ToString())}";
                }

                // 현재 층과 인접한 계단인지
                bool adjacent = IsStairAdjacentToFloor(c, floorIdx);
                Debug.Log($"  계단:{c} world:{_grid.CellToWorld(c):F1}  현재층인접:{adjacent}  인접:{adj}");
            }

            if (!foundStair)
                Debug.Log($"  [!] 범위 {searchRange} 내 계단 셀 없음");
        }
    }

    bool IsStairAdjacentToFloor(Vector3Int stairCell, int floorIndex)
    {
        if (floorIndex < 0) return true;
        return _grid.IsStairValidForFloor(stairCell, floorIndex);
    }

    void DrawStairEntryPoints()
    {
        if (_grid == null) return;

        var drawnStairs = new HashSet<Vector3Int>();

        for (int x = rangeMin.x; x <= rangeMax.x; x++)
        for (int y = rangeMin.y; y <= rangeMax.y; y++)
        {
            var cellPos = new Vector3Int(x, y, 0);
            if (!_grid.IsOnStair(cellPos)) continue;
            if (drawnStairs.Contains(cellPos)) continue;

            var stairCells = new HashSet<Vector3Int>();
            CollectStairCells(cellPos, stairCells);
            foreach (var sc in stairCells) drawnStairs.Add(sc);

            float maxY = float.MinValue, minY = float.MaxValue;
            Vector3 topWorld = Vector3.zero, botWorld = Vector3.zero;
            foreach (var sc in stairCells)
            {
                var w = _grid.CellToWorld(sc);
                if (w.y > maxY) { maxY = w.y; topWorld = w; }
                if (w.y < minY) { minY = w.y; botWorld = w; }
            }

            // Physics Shape에서 빗변 읽기
            Vector3 upperEntry, lowerEntry;
            if (!_grid.GetStairHypotenusePoints(cellPos, out upperEntry, out lowerEntry))
            {
                // fallback
                float stairX = _grid.CellToWorld(cellPos).x;
                upperEntry = new Vector3(stairX + 0.5f, topWorld.y, 0f);
                lowerEntry = new Vector3(stairX - 0.5f, botWorld.y, 0f);
            }

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(upperEntry, 0.12f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(lowerEntry, 0.12f);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(upperEntry, lowerEntry);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(upperEntry + Vector3.right * 0.1f, "Upper",
                new GUIStyle { fontSize = 8, normal = { textColor = Color.green } });
            UnityEditor.Handles.Label(lowerEntry + Vector3.left  * 0.3f, "Lower",
                new GUIStyle { fontSize = 8, normal = { textColor = Color.yellow } });
#endif
        }
    }

    void CollectStairCells(Vector3Int cell, HashSet<Vector3Int> visited)
    {
        if (!_grid.IsOnStair(cell) || visited.Contains(cell)) return;
        visited.Add(cell);
        CollectStairCells(cell + Vector3Int.up,   visited);
        CollectStairCells(cell + Vector3Int.down, visited);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        _grid ??= FindFirstObjectByType<PathfindingGrid>();
        if (_grid == null) return;

        for (int x = rangeMin.x; x <= rangeMax.x; x++)
        for (int y = rangeMin.y; y <= rangeMax.y; y++)
        {
            var cellPos  = new Vector3Int(x, y, 0);
            var worldPos = _grid.CellToWorld(cellPos);
            if (worldPos == Vector3.zero) continue;

            bool isStair   = _grid.IsOnStair(cellPos);
            bool isWalkable = _grid.IsWalkable(cellPos);

            // 색상 결정
            Color color;
            bool  draw = false;

            if (isStair)
            {
                bool top = _grid.IsStairTop(cellPos);
                bool bot = _grid.IsStairBottom(cellPos);
                color = top && !bot ? new Color(0f, 0.8f, 1f, alpha)   // 상단: 하늘색
                      : bot && !top ? new Color(0f, 0.3f, 1f, alpha)   // 하단: 진파랑
                      : new Color(0.5f, 0f, 1f, alpha);                // 단독: 보라
                draw = showStairs;
            }
            else if (isWalkable)
            {
                int fi = _grid.GetFloorIndex(cellPos);
                color = fi == 0 ? new Color(0f,   1f,   0f,   alpha)  // 3층: 초록
                      : fi == 1 ? new Color(0.5f, 1f,   0f,   alpha)  // 2층: 연초록
                      :           new Color(0.8f, 1f,   0.2f, alpha); // 1층: 황록
                draw = showWalkable;
            }
            else
            {
                color = new Color(1f, 0f, 0f, alpha);
                draw  = showBlocked;
            }

            if (!draw) continue;

            // 타일 색상 표시
            Gizmos.color = color;
            float s = _grid.cellSize * 0.85f;
            Gizmos.DrawCube(worldPos, new Vector3(s, s, 0.01f));

            // 셀 좌표 텍스트 표시
            if (showCellCoords)
            {
                string floorStr = isStair ? "S" : _grid.GetFloorIndex(cellPos).ToString();
                string label = showFloorIndex
                    ? $"{x},{y}" + "\nF:" + floorStr
                    : $"{x},{y}";

#if UNITY_EDITOR
                UnityEditor.Handles.color = Color.white;
                UnityEditor.Handles.Label(worldPos + Vector3.up * 0.1f, label,
                    _coordStyle ??= new GUIStyle
                    {
                        fontSize  = 8,
                        alignment = TextAnchor.MiddleCenter,
                        normal    = { textColor = Color.white }
                    });
#endif
            }
        }

        DrawStairEntryPoints();

    }
}

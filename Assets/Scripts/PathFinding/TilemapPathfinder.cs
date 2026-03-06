using System.Collections.Generic;
using UnityEngine;

public static class TilemapPathfinder
{
    public static List<Vector3> FindPath(Vector3 startWorld, Vector3 goalWorld, PathfindingGrid grid)
    {
        if (grid == null) return null;

        Vector3Int startCell = grid.WorldToCell(startWorld);
        Vector3Int goalCell  = grid.WorldToCell(goalWorld);

        if (startCell == goalCell) return new List<Vector3> { goalWorld };

        if (!grid.IsWalkable(goalCell))
            goalCell = FindNearestWalkable(goalCell, grid, 5);

        if (goalCell == default) return null;

        var openSet  = new SortedList<float, Vector3Int>(new DuplicateKeyComparer());
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var gScore   = new Dictionary<Vector3Int, float>();
        var inOpen   = new HashSet<Vector3Int>();
        var closed   = new HashSet<Vector3Int>();

        int startFloor = grid.GetFloorIndex(startCell);

        gScore[startCell] = 0f;
        openSet.Add(Heuristic(startCell, goalCell), startCell);
        inOpen.Add(startCell);

        int maxIter = 2000, iter = 0;

        while (openSet.Count > 0 && iter++ < maxIter)
        {
            var current = openSet.Values[0];
            openSet.RemoveAt(0);
            inOpen.Remove(current);

            if (current == goalCell)
                return ReconstructPath(cameFrom, current, grid);

            closed.Add(current);

            foreach (var neighbor in grid.GetNeighbors(current))
            {
                if (closed.Contains(neighbor)) continue;

                float tentativeG = gScore.GetValueOrDefault(current, float.MaxValue)
                                 + Cost(current, neighbor, grid, startFloor);

                if (tentativeG < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor]   = tentativeG;
                    float f = tentativeG + Heuristic(neighbor, goalCell);
                    if (!inOpen.Contains(neighbor))
                    {
                        openSet.Add(f, neighbor);
                        inOpen.Add(neighbor);
                    }
                }
            }
        }

        return null;
    }

    static List<Vector3> ReconstructPath(
        Dictionary<Vector3Int, Vector3Int> cameFrom,
        Vector3Int current,
        PathfindingGrid grid)
    {
        var cellPath = new List<Vector3Int>();
        while (cameFrom.ContainsKey(current))
        {
            cellPath.Add(current);
            current = cameFrom[current];
        }
        cellPath.Add(current);
        cellPath.Reverse();

        var path = new List<Vector3>();

        for (int i = 0; i < cellPath.Count; i++)
        {
            var  cell     = cellPath[i];
            var  worldPos = grid.CellToWorld(cell);
            bool isStair  = grid.IsOnStair(cell);

            // 지형 → 계단 진입점 삽입
            if (i + 1 < cellPath.Count)
            {
                var  nextCell    = cellPath[i + 1];
                bool nextIsStair = grid.IsOnStair(nextCell);

                if (!isStair && nextIsStair)
                {
                    GetStairEntryPoints(nextCell, grid, out Vector3 upperEntry, out Vector3 lowerEntry);

                    int curFloor = grid.GetFloorIndex(cell);
                    var pair     = grid.GetStairPairFloors(nextCell);
                    int upFloor  = int.MaxValue;
                    foreach (var f in pair) if (f < upFloor) upFloor = f;

                    Vector3 entryPoint = (curFloor == upFloor) ? upperEntry : lowerEntry;

                    Debug.Log($"[Path] 지형→계단 진입: cell={cell} nextStair={nextCell} curFloor={curFloor} upFloor={upFloor} entry={entryPoint}");

                    path.Add(new Vector3(worldPos.x, worldPos.y, 0f));
                    path.Add(entryPoint);
                    continue;
                }
            }

            // 계단 → 지형 진출점 삽입
            if (i > 0)
            {
                var  prevCell    = cellPath[i - 1];
                bool prevIsStair = grid.IsOnStair(prevCell);

                if (prevIsStair && !isStair)
                {
                    GetStairEntryPoints(prevCell, grid, out Vector3 upperEntry, out Vector3 lowerEntry);

                    int curFloor = grid.GetFloorIndex(cell);
                    var pair     = grid.GetStairPairFloors(prevCell);
                    int upFloor  = int.MaxValue;
                    foreach (var f in pair) if (f < upFloor) upFloor = f;

                    Vector3 exitPoint = (curFloor == upFloor) ? upperEntry : lowerEntry;

                    Debug.Log($"[Path] 계단→지형 진출: cell={cell} prevStair={prevCell} curFloor={curFloor} upFloor={upFloor} exit={exitPoint}");

                    path.Add(exitPoint);
                    path.Add(new Vector3(worldPos.x, worldPos.y, 0f));
                    continue;
                }
            }

            // 계단 셀 자체는 경로에서 제외
            if (isStair)
            {
                Debug.Log($"[Path] 계단 셀 스킵: {cell}");
                continue;
            }

            path.Add(new Vector3(worldPos.x, worldPos.y, 0f));
        }

        return path;
    }

    /// <summary>
    /// 계단 Physics Shape(삼각형 빗변)에서 진입/진출점 반환
    ///   upperEntry : 빗변 상단 끝점 (상위층 진입)
    ///   lowerEntry : 빗변 하단 끝점 (하위층 진입)
    /// </summary>
    static void GetStairEntryPoints(Vector3Int stairCell, PathfindingGrid grid,
        out Vector3 upperEntry, out Vector3 lowerEntry)
    {
        // CompositeCollider2D Physics Shape에서 빗변 읽기
        if (grid.GetStairHypotenusePoints(stairCell, out upperEntry, out lowerEntry))
            return;

        // fallback: shape를 못 찾으면 기존 방식 사용
        var stairCells = new HashSet<Vector3Int>();
        CollectStairCells(stairCell, grid, stairCells);

        float maxY = float.MinValue, minY = float.MaxValue;
        Vector3 topWorld = Vector3.zero, botWorld = Vector3.zero;
        foreach (var sc in stairCells)
        {
            var w = grid.CellToWorld(sc);
            if (w.y > maxY) { maxY = w.y; topWorld = w; }
            if (w.y < minY) { minY = w.y; botWorld = w; }
        }

        float stairX = grid.CellToWorld(stairCell).x;
        upperEntry = new Vector3(stairX + 0.5f, topWorld.y, 0f);
        lowerEntry = new Vector3(stairX - 0.5f, botWorld.y, 0f);
    }

    static void CollectStairCells(Vector3Int cell, PathfindingGrid grid, HashSet<Vector3Int> visited)
    {
        if (!grid.IsOnStair(cell) || visited.Contains(cell)) return;
        visited.Add(cell);
        CollectStairCells(cell + Vector3Int.up,   grid, visited);
        CollectStairCells(cell + Vector3Int.down, grid, visited);
    }

    static float Heuristic(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    static float Cost(Vector3Int from, Vector3Int to, PathfindingGrid grid, int startFloor)
    {
        float baseCost = (from.x != to.x && from.y != to.y) ? Mathf.Sqrt(2f) : 1f;

        int toFloor = grid.GetFloorIndex(to);

        if (toFloor == -1)
        {
            var connectedFloors = grid.GetStairPairFloors(to);
            bool valid = connectedFloors.Contains(startFloor);
            return baseCost + (valid ? 0f : 50f);
        }

        int fromFloor = grid.GetFloorIndex(from);
        if (fromFloor == -1) return baseCost;
        if (fromFloor == toFloor) return baseCost;

        return baseCost + 100f;
    }

    static Vector3Int FindNearestWalkable(Vector3Int cell, PathfindingGrid grid, int radius)
    {
        for (int r = 1; r <= radius; r++)
        for (int x = -r; x <= r; x++)
        for (int y = -r; y <= r; y++)
        {
            if (Mathf.Abs(x) != r && Mathf.Abs(y) != r) continue;
            var c = cell + new Vector3Int(x, y, 0);
            if (grid.IsWalkable(c)) return c;
        }
        return default;
    }

    class DuplicateKeyComparer : IComparer<float>
    {
        public int Compare(float x, float y)
        {
            int r = x.CompareTo(y);
            return r == 0 ? 1 : r;
        }
    }
}

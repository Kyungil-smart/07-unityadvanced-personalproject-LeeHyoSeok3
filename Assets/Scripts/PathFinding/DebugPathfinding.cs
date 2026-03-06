using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 디버그용 길찾기 테스트
/// 우클릭 → 가장 가까운 WorkerUnit이 해당 위치로 길찾기
/// </summary>
public class DebugPathfinding : MonoBehaviour
{
    [Header("테스트 설정")]
    [Tooltip("비워두면 씬에서 가장 가까운 워커 자동 선택")]
    public WorkerUnit targetWorker;

    void Update()
    {
        if (Mouse.current == null) return;
        if (!Mouse.current.rightButton.wasPressedThisFrame) return;

        // BuildingPlacer가 배치 모드 중이면 무시
        if (ServiceLocator.Has<BuildingPlacer>() &&
            ServiceLocator.Get<BuildingPlacer>().IsPlacing) return;

        Vector3 mouseWorld = GetMouseWorldPos();
        WorkerUnit worker  = GetWorker(mouseWorld);

        if (worker == null)
        {
            Debug.LogWarning("[DebugPathfinding] 씬에 WorkerUnit 없음");
            return;
        }

        Debug.Log($"[DebugPathfinding] {worker.name} → 목표: {mouseWorld}");
        MoveWorkerTo(worker, mouseWorld);
    }

    void MoveWorkerTo(WorkerUnit worker, Vector3 destination)
    {
        if (!ServiceLocator.Has<PathfindingGrid>())
        {
            Debug.LogWarning("[DebugPathfinding] PathfindingGrid 없음");
            return;
        }

        var grid = ServiceLocator.Get<PathfindingGrid>();
        var path = TilemapPathfinder.FindPath(worker.transform.position, destination, grid);

        if (path == null || path.Count == 0)
        {
            Debug.LogWarning($"[DebugPathfinding] 경로 없음 ({worker.transform.position} → {destination})");
            return;
        }

        LogPath(worker.name, path);
        worker.DebugMoveTo(path);
    }

    void LogPath(string workerName, List<Vector3> path)
    {
        Debug.Log($"[DebugPathfinding] {workerName} 경로 {path.Count}개 웨이포인트:");
        for (int i = 0; i < path.Count; i++)
            Debug.Log($"  [{i}] {path[i]:F2}");
    }

    WorkerUnit GetWorker(Vector3 mouseWorld)
    {
        if (targetWorker != null) return targetWorker;

        // 씬 전체에서 가장 가까운 워커 탐색
        var allWorkers = Object.FindObjectsByType<WorkerUnit>(FindObjectsSortMode.None);
        WorkerUnit nearest  = null;
        float      minDist  = float.MaxValue;

        foreach (var w in allWorkers)
        {
            if (w == null) continue;
            float dist = Vector3.Distance(w.transform.position, mouseWorld);
            if (dist < minDist) { minDist = dist; nearest = w; }
        }

        return nearest;
    }

    Vector3 GetMouseWorldPos()
    {
        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 world  = Camera.main.ScreenToWorldPoint(screen);
        world.z = 0f;
        return world;
    }
}

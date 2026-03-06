using UnityEngine;

/// <summary>
/// 건물 배치 직후 가장 가까운 유휴 워커를 건설 현장에 배정
/// BuildingPlacer에서 직접 호출
/// </summary>
public class ConstructionAssigner : MonoBehaviour
{
    void Awake()
    {
        ServiceLocator.Register<ConstructionAssigner>(this);
    }

    public void AssignWorkerTo(BuildingConstruction construction)
    {
        if (!ServiceLocator.Has<WorkerAssigner>())
        {
            Debug.LogWarning("[ConstructionAssigner] WorkerAssigner 없음");
            return;
        }

        var allWorkers = ServiceLocator.Get<WorkerAssigner>().GetAllWorkers();
        Vector3 targetPos = construction.transform.position;

        WorkerUnit nearest = null;
        float minDist = float.MaxValue;

        foreach (var worker in allWorkers)
        {
            if (worker == null) continue;
            if (!worker.StateMachine.Is(UnitState.Idle)) continue;
            if (worker.AssignedConstruction != null) continue;

            float dist = Vector3.Distance(worker.transform.position, targetPos);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = worker;
            }
        }

        if (nearest == null)
        {
            Debug.LogWarning($"[ConstructionAssigner] {construction.name} → 유휴 워커 없음 (대기 중)");
            return; // BuildingConstruction.Update에서 주기적으로 재탐색
        }

        construction.SetWorkerAssigned(true);
        nearest.AssignConstruction(construction);
        Debug.Log($"[ConstructionAssigner] {nearest.name} → {construction.name} 건설 배정");
    }
}
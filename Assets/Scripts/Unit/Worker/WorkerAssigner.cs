using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자원 노드 클릭 → 가장 가까운 유휴 일꾼 자동 할당
///
/// 동작:
///   1. OnResourceNodeClicked 이벤트 수신
///   2. 씬의 모든 WorkerUnit 중 Idle 상태인 유닛 탐색
///   3. 클릭한 노드와 가장 가까운 일꾼에게 AssignNode 호출
///
/// 씬에 단 하나만 존재 (ServiceLocator 등록)
/// </summary>
public class WorkerAssigner : MonoBehaviour
{
    // 씬에 존재하는 모든 일꾼 목록
    private readonly List<WorkerUnit> _allWorkers = new List<WorkerUnit>();

    void Awake()
    {
        ServiceLocator.Register<WorkerAssigner>(this);
    }

    void OnEnable()
    {
        EventBus.Subscribe<OnResourceNodeClicked>(OnResourceNodeClicked);
        EventBus.Subscribe<OnUnitSpawned>(OnUnitSpawned);
        EventBus.Subscribe<OnUnitDied>(OnUnitDied);
        EventBus.Subscribe<OnWorkerEnergyDepleted>(OnWorkerEnergyDepleted);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<OnResourceNodeClicked>(OnResourceNodeClicked);
        EventBus.Unsubscribe<OnUnitSpawned>(OnUnitSpawned);
        EventBus.Unsubscribe<OnUnitDied>(OnUnitDied);
        EventBus.Unsubscribe<OnWorkerEnergyDepleted>(OnWorkerEnergyDepleted);
    }

    // -------------------------------------------------------
    // 이벤트 핸들러
    // -------------------------------------------------------

    // 자원 노드 클릭 → 가장 가까운 유휴 일꾼 할당
    private void OnResourceNodeClicked(OnResourceNodeClicked e)
    {
        WorkerUnit worker = FindNearestIdleWorker(e.node.transform.position);

        if (worker == null)
        {
            Debug.Log("[WorkerAssigner] 유휴 상태의 일꾼이 없습니다.");
            return;
        }

        worker.AssignNode(e.node);

        EventBus.Publish(new OnWorkerAssigned
        {
            worker = worker,
            node   = e.node
        });

        Debug.Log($"[WorkerAssigner] {worker.name} → {e.node.name} 할당");
    }

    // 유닛 스폰 시 WorkerUnit이면 목록에 추가
    private void OnUnitSpawned(OnUnitSpawned e)
    {
        if (e.unit is WorkerUnit worker)
            RegisterWorker(worker);
    }

    // 유닛 사망 시 목록에서 제거
    private void OnUnitDied(OnUnitDied e)
    {
        if (e.unit is WorkerUnit worker)
            UnregisterWorker(worker);
    }

    // 에너지 소진 시 목록에서 제거
    private void OnWorkerEnergyDepleted(OnWorkerEnergyDepleted e)
    {
        UnregisterWorker(e.worker);
    }

    // -------------------------------------------------------
    // 일꾼 목록 관리
    // -------------------------------------------------------
    public void RegisterWorker(WorkerUnit worker)
    {
        if (!_allWorkers.Contains(worker))
            _allWorkers.Add(worker);
    }

    public void UnregisterWorker(WorkerUnit worker)
    {
        _allWorkers.Remove(worker);
    }

    // -------------------------------------------------------
    // 가장 가까운 유휴 일꾼 탐색
    // -------------------------------------------------------
    private WorkerUnit FindNearestIdleWorker(Vector3 targetPos)
    {
        WorkerUnit nearest  = null;
        float      minDist  = float.MaxValue;

        foreach (var worker in _allWorkers)
        {
            if (worker == null) continue;
            if (!worker.StateMachine.Is(UnitState.Idle)) continue;

            float dist = Vector3.Distance(worker.transform.position, targetPos);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = worker;
            }
        }

        return nearest;
    }

    // -------------------------------------------------------
    // 전체 일꾼 목록 조회 (외부 접근용)
    // -------------------------------------------------------
    public IReadOnlyList<WorkerUnit> GetAllWorkers() => _allWorkers;

    public int IdleWorkerCount
    {
        get
        {
            int count = 0;
            foreach (var w in _allWorkers)
                if (w != null && w.StateMachine.Is(UnitState.Idle)) count++;
            return count;
        }
    }
}

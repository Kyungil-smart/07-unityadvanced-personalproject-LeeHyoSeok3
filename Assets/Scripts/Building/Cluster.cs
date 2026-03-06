using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 집락 건물
///
/// - 씬에 미리 배치된 초기 건물 (파괴 불가)
/// - 자동으로 워커 spawnCount명 스폰
/// - 전략 초기화 시 워커 회수 후 재스폰
/// </summary>
public class Cluster : BuildingBase
{
    [Header("집락 설정")]
    public GameObject workerPrefab;
    [Tooltip("스폰할 워커 수 (기본 4)")]
    public int workerCount = 4;
    [Tooltip("워커 스폰 간격 (초)")]
    public float spawnInterval = 0.3f;

    [Header("스폰 위치 오프셋")]
    public float spawnRadius = 0.5f;

    private readonly List<WorkerUnit> _workers = new();

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void OnBuilt()
    {
        StartCoroutine(SpawnWorkers());
    }

    // -------------------------------------------------------
    // 워커 스폰
    // -------------------------------------------------------
    private IEnumerator SpawnWorkers()
    {
        for (int i = 0; i < workerCount; i++)
        {
            SpawnOneWorker();
            yield return new WaitForSeconds(spawnInterval);
        }

        Debug.Log($"[Cluster] {name} 워커 {workerCount}명 스폰 완료");
    }

    private void SpawnOneWorker()
    {
        if (workerPrefab == null)
        {
            Debug.LogError($"[Cluster] {name} workerPrefab 없음!");
            return;
        }

        Vector3 spawnPos = GetRandomSpawnPosition();

        var go     = Instantiate(workerPrefab, spawnPos, Quaternion.identity);
        var worker = go.GetComponent<WorkerUnit>();

        if (worker == null)
        {
            Debug.LogError($"[Cluster] workerPrefab에 WorkerUnit 없음!");
            Destroy(go);
            return;
        }

        worker.OwnerCluster = this;
        _workers.Add(worker);

        if (ServiceLocator.Has<WorkerAssigner>())
            ServiceLocator.Get<WorkerAssigner>().RegisterWorker(worker);

        Debug.Log($"[Cluster] {name} → 워커 스폰 ({_workers.Count}/{workerCount}) at {spawnPos}");
    }

    private Vector3 GetRandomSpawnPosition()
    {
        PathfindingGrid grid = ServiceLocator.Has<PathfindingGrid>()
            ? ServiceLocator.Get<PathfindingGrid>()
            : Object.FindFirstObjectByType<PathfindingGrid>();

        if (grid == null)
            return transform.position;

        Vector3Int centerCell  = grid.WorldToCell(transform.position);
        int        centerFloor = grid.GetFloorIndex(centerCell);
        int        radius      = Mathf.CeilToInt(spawnRadius);

        // 조건 충족 셀 수집
        var candidates = new System.Collections.Generic.List<Vector3Int>();
        for (int x = -radius; x <= radius; x++)
        for (int y = -radius; y <= radius; y++)
        {
            var c = centerCell + new Vector3Int(x, y, 0);
            if (!grid.IsWalkable(c)) continue;
            if (grid.GetFloorIndex(c) != centerFloor) continue;
            candidates.Add(c);
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"[Cluster] 스폰 가능 셀 없음 → 중심 위치 사용");
            return grid.CellToWorld(centerCell);
        }

        var picked = candidates[Random.Range(0, candidates.Count)];
        Vector3 cellWorld = grid.CellToWorld(picked);

        // 같은 셀에 겹쳐도 밀어낼 수 있도록 랜덤 오프셋
        float offset = grid.cellSize * 0.3f;
        cellWorld.x += Random.Range(-offset, offset);
        cellWorld.y += Random.Range(-offset, offset);

        return cellWorld;
    }

    // -------------------------------------------------------
    // 전략 초기화
    // -------------------------------------------------------
    public override void ResetBuilding()
    {
        // 기존 워커 전체 제거
        foreach (var worker in _workers)
        {
            if (worker == null) continue;

            if (ServiceLocator.Has<WorkerAssigner>())
                ServiceLocator.Get<WorkerAssigner>().UnregisterWorker(worker);

            Destroy(worker.gameObject);
        }
        _workers.Clear();

        // 재스폰
        StartCoroutine(SpawnWorkers());
    }

    // 집락은 파괴 불가
    public override void TakeDamage(int damage) { }

    // -------------------------------------------------------
    // 현재 워커 목록 (외부 접근용)
    // -------------------------------------------------------
    public IReadOnlyList<WorkerUnit> Workers => _workers;
    public int ActiveWorkerCount => _workers.Count;
}

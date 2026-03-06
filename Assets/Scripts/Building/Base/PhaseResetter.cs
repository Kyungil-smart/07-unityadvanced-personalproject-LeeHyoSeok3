using UnityEngine;

/// <summary>
/// 준비 페이즈 전략 초기화
///
/// 초기화 대상:
///   - 자원 인벤토리 (스냅샷으로 복원)
///   - 배치된 모든 건물 제거
///   - 모든 자원 노드 복원
///   - 집락 일꾼 재스폰
///
/// OnResetRequested 이벤트를 수신해서 자동 실행
/// PhaseManager가 준비 페이즈 여부를 먼저 검증
/// </summary>
public class PhaseResetter : MonoBehaviour
{
    void Awake()
    {
        ServiceLocator.Register<PhaseResetter>(this);
    }

    void OnEnable()
    {
        EventBus.Subscribe<OnResetRequested>(OnResetRequested);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<OnResetRequested>(OnResetRequested);
    }

    // -------------------------------------------------------
    // 초기화 실행
    // -------------------------------------------------------
    private void OnResetRequested(OnResetRequested e)
    {
        // PhaseManager가 준비 페이즈 여부를 보장하지만 이중 검증
        if (!PhaseManager.Instance.IsPrepare)
        {
            Debug.LogWarning("[PhaseResetter] 준비 페이즈가 아니므로 초기화 불가");
            return;
        }

        Debug.Log("[PhaseResetter] 전략 초기화 시작");

        ResetResources();
        ResetBuildings();
        ResetResourceNodes();
        ResetClusters();

        Debug.Log("[PhaseResetter] 전략 초기화 완료");
    }

    // -------------------------------------------------------
    // 1. 자원 인벤토리 복원
    // -------------------------------------------------------
    private void ResetResources()
    {
        if (ServiceLocator.Has<ResourceInventory>())
            ServiceLocator.Get<ResourceInventory>().RestoreSnapshot();
    }

    // -------------------------------------------------------
    // 2. 배치된 건물 전체 제거
    // -------------------------------------------------------
    private void ResetBuildings()
    {
        if (ServiceLocator.Has<BuildingPlacer>())
            ServiceLocator.Get<BuildingPlacer>().ResetAllBuildings();
    }

    // -------------------------------------------------------
    // 3. 자원 노드 복원
    // -------------------------------------------------------
    private void ResetResourceNodes()
    {
        var nodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        foreach (var node in nodes)
            node.ResetNode();

        Debug.Log($"[PhaseResetter] 자원 노드 {nodes.Length}개 초기화");
    }

    // -------------------------------------------------------
    // 4. 집락 일꾼 재스폰
    // -------------------------------------------------------
    private void ResetClusters()
    {
        var clusters = FindObjectsByType<Cluster>(FindObjectsSortMode.None);
        foreach (var cluster in clusters)
            cluster.ResetBuilding();

        Debug.Log($"[PhaseResetter] 집락 {clusters.Length}개 초기화");
    }
}

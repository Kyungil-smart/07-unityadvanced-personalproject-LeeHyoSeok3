using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 웨이브별 건물 유지비 관리
///
/// 웨이브 시작 전:
///   - 플레이어가 각 건물의 유지비 활성화/비활성화 선택
///   - 자원이 부족하면 자동 비활성화
///
/// 웨이브 시작 시 (WaveManager가 호출):
///   - 활성화된 건물 유지비 일괄 차감
///   - 차감 성공한 건물만 유닛 스폰
/// </summary>
public class BuildingMaintenance : MonoBehaviour
{
    // 유지비가 있는 생산 건물 목록
    private readonly List<ProductionBuilding> _productionBuildings
        = new List<ProductionBuilding>();

    void Awake()
    {
        ServiceLocator.Register<BuildingMaintenance>(this);
    }

    // -------------------------------------------------------
    // 건물 등록 / 해제 (ProductionBuilding이 자동 호출)
    // -------------------------------------------------------
    public void RegisterBuilding(ProductionBuilding building)
    {
        if (!_productionBuildings.Contains(building))
            _productionBuildings.Add(building);
    }

    public void UnregisterBuilding(ProductionBuilding building)
    {
        _productionBuildings.Remove(building);
    }

    // -------------------------------------------------------
    // 유지비 일괄 차감 + 유닛 스폰
    // WaveManager가 OnWaveStarted 시 호출
    // -------------------------------------------------------
    public void ProcessWaveStart()
    {
        foreach (var building in _productionBuildings)
        {
            if (building == null || building.IsDestroyed) continue;

            // 유지비 차감 (자원 부족 시 자동 비활성화)
            building.PayMaintenance();

            // 활성화된 건물만 유닛 스폰
            if (building.MaintenanceActive)
                building.SpawnUnits();
        }

        Debug.Log("[BuildingMaintenance] 웨이브 유지비 처리 완료");
    }

    // -------------------------------------------------------
    // 자원 부족 건물 미리 확인 (UI 표시용)
    // -------------------------------------------------------
    public List<ProductionBuilding> GetAffordableBuildings()
    {
        var inventory  = ServiceLocator.Get<ResourceInventory>();
        var affordable = new List<ProductionBuilding>();

        foreach (var building in _productionBuildings)
        {
            if (building == null) continue;
            if (inventory.CanAffordAll(building.data.GetMaintenanceCost()))
                affordable.Add(building);
        }
        return affordable;
    }

    public IReadOnlyList<ProductionBuilding> AllProductionBuildings
        => _productionBuildings;
}

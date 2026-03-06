using UnityEngine;

/// <summary>
/// 유닛 생산 건물 기반 클래스
/// WarriorBarracks / ArcherRange 등이 상속
///
/// 웨이브 시작 시 WaveManager가 SpawnUnits() 호출
/// → 유지비가 활성화된 건물만 유닛 스폰
/// </summary>
public class ProductionBuilding : BuildingBase
{
    [Header("스폰 위치 (비어있으면 건물 위치 사용)")]
    public Transform spawnPoint;

    [Header("방어 지점 자동 이동")]
    [Tooltip("스폰 후 DefensePointManager의 지정 위치로 자동 이동")]
    public bool autoMoveToDefensePoint = true;

    protected override void OnBuilt()
    {
        // BuildingMaintenance에 자신을 등록
        if (ServiceLocator.Has<BuildingMaintenance>())
            ServiceLocator.Get<BuildingMaintenance>().RegisterBuilding(this);
    }

    protected override void OnDestroyed()
    {
        if (ServiceLocator.Has<BuildingMaintenance>())
            ServiceLocator.Get<BuildingMaintenance>().UnregisterBuilding(this);
    }

    // -------------------------------------------------------
    // 유닛 스폰 (WaveManager가 Combat 페이즈 시작 시 호출)
    // -------------------------------------------------------
    public void SpawnUnits()
    {
        if (IsDestroyed) return;
        if (!MaintenanceActive) return;
        if (data == null || data.unitPrefab == null) return;

        Vector3 spawnPos = spawnPoint != null
            ? spawnPoint.position
            : transform.position;

        for (int i = 0; i < data.spawnCountPerWave; i++)
        {
            // 약간 랜덤 오프셋으로 겹침 방지
            Vector3 offset = new Vector3(
                Random.Range(-0.3f, 0.3f),
                Random.Range(-0.3f, 0.3f),
                0f
            );

            var unitGo = Instantiate(data.unitPrefab, spawnPos + offset, Quaternion.identity);
            var unit   = unitGo.GetComponent<UnitBase>();

            if (unit == null) continue;

            // 방어 지점으로 자동 이동 // todo 나중에 유닛 생성 쪽 작성하면 주석해제하기
            // if (autoMoveToDefensePoint && ServiceLocator.Has<DefensePointManager>())
            // {
            //     var defPoint = ServiceLocator.Get<DefensePointManager>().CurrentDefensePoint;
            //     if (unit is CombatUnit combatUnit)
            //         combatUnit.SetDefensePoint(defPoint);
            // }

            Debug.Log($"[ProductionBuilding] {data.buildingName} → {data.unitPrefab.name} 스폰");
        }
    }
}

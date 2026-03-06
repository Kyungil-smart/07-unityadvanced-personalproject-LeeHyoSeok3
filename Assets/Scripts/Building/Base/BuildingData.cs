using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Data/BuildingData", fileName = "NewBuildingData")]
public class BuildingData : ScriptableObject
{
    [Header("기본 정보")]
    public string buildingName = "Building";
    public Sprite buildingIcon; // 건물 아이콘
    public Sprite avatarIcon;   // 아바타 아이콘
    public Sprite nameIcon;     // 이름 아이콘
    public GameObject prefab;

    [Header("건설 비용")]
    public int woodCost = 0;
    public int meatCost = 0;
    public int goldCost = 0;

    [Header("건설 시간")]
    [Tooltip("워커가 건설하는 데 걸리는 시간 (초)")]
    public float buildTime = 3f;

    [Header("유지비 (웨이브당)")]
    public int maintenanceGold = 0;
    public int maintenanceMeat = 0;

    [Header("생산 유닛 설정 (생산 건물 전용)")]
    public GameObject unitPrefab;
    public int spawnCountPerWave = 0;

    [Header("내구도")]
    public int maxHp = 200;

    public Dictionary<ResourceType, int> GetBuildCost()
    {
        var cost = new Dictionary<ResourceType, int>();
        if (woodCost > 0) cost[ResourceType.Wood] = woodCost;
        if (meatCost > 0) cost[ResourceType.Meat] = meatCost;
        if (goldCost > 0) cost[ResourceType.Gold] = goldCost;
        return cost;
    }

    public Dictionary<ResourceType, int> GetMaintenanceCost()
    {
        var cost = new Dictionary<ResourceType, int>();
        if (maintenanceGold > 0) cost[ResourceType.Gold] = maintenanceGold;
        if (maintenanceMeat > 0) cost[ResourceType.Meat] = maintenanceMeat;
        return cost;
    }

    public bool HasMaintenance => maintenanceGold > 0 || maintenanceMeat > 0;
}
using UnityEngine;

/// <summary>
/// 유닛 스탯 데이터 (ScriptableObject)
/// Assets/Data/Units/ 에 저장
///
/// 사용법: [CreateAssetMenu] → Project 창 우클릭 → Create/Data/UnitData
/// </summary>
[CreateAssetMenu(menuName = "Data/UnitData", fileName = "NewUnitData")]
public class UnitData : ScriptableObject
{
    [Header("기본 정보")]
    public string unitName = "Unit";
    public Sprite icon;

    [Header("스탯")]
    public int   maxHp       = 100;
    public float moveSpeed   = 3f;

    [Header("일꾼 전용")]
    public int   maxEnergy         = 100; // 총 보유 에너지
    public float harvestCooldown   = 1.5f; // 채집 쿨타임 (초)

    [Header("건물 생산 비용 (이 유닛을 생산할 때 소모되는 자원)")]
    public int woodCost = 0;
    public int meatCost = 0;
    public int goldCost = 0;
}
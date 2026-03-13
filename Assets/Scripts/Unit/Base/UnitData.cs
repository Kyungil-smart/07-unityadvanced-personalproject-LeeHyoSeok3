using UnityEngine;

/// <summary>
/// 유닛 스탯 데이터 (ScriptableObject) — 공통 기반
/// Assets/Data/Units/ 에 저장
///
/// 하위 타입:
///   WorkerData     → 일꾼 전용 필드 (maxEnergy, harvestCooldown)
///   CombatUnitData → 전투 유닛 전용 필드 (attackDamage, attackRange, attackCooldown)
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
    public int   maxHp     = 100;
    public float moveSpeed = 3f;

    [Header("건물 생산 비용 (이 유닛을 생산할 때 소모되는 자원)")]
    public int woodCost = 0;
    public int meatCost = 0;
    public int goldCost = 0;
}
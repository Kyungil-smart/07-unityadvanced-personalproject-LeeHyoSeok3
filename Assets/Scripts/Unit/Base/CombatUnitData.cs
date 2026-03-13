using UnityEngine;

/// <summary>
/// 전투 유닛 전용 스탯 데이터
/// UnitData를 상속하여 전투 고유 필드 추가
///
/// 적용 대상: SpearMan / Warrior / Archer / Monk
/// </summary>
[CreateAssetMenu(menuName = "Data/CombatUnitData", fileName = "NewCombatUnitData")]
public class CombatUnitData : UnitData
{
    [Header("전투 전용")]
    public int   attackDamage   = 10;  // 1회 공격 데미지
    public float attackRange    = 1.5f; // 공격 사거리 (탐지 + 공격 통합)
    public float attackCooldown = 1.0f; // 공격 쿨타임 (초)
}

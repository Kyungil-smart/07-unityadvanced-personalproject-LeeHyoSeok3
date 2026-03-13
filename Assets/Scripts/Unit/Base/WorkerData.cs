using UnityEngine;

/// <summary>
/// 일꾼 유닛 전용 스탯 데이터
/// UnitData를 상속하여 일꾼 고유 필드 추가
/// </summary>
[CreateAssetMenu(menuName = "Data/WorkerData", fileName = "NewWorkerData")]
public class WorkerData : UnitData
{
    [Header("일꾼 전용")]
    public int   maxEnergy       = 100;  // 총 보유 에너지
    public float harvestCooldown = 1.5f; // 채집 쿨타임 (초, 현재 미사용 — 확장용)
}

using UnityEngine;

/// <summary>
/// 동물 자원 노드 밸런싱 데이터
/// ResourceNodeData 를 상속해 공통 채집 수치 + 동물 전용 수치를 함께 보유한다.
/// </summary>
[CreateAssetMenu(menuName = "Data/AnimalNodeData", fileName = "NewAnimalNodeData")]
public class AnimalNodeData : ResourceNodeData
{
    [Header("배회")]
    public float wanderRadius      = 3f;
    public float wanderIntervalMin = 3f;
    public float wanderIntervalMax = 6f;
    public float moveSpeed         = 1f;
    public float arriveThreshold   = 0.08f;

    [Header("풀 뜯기 주기")]
    public float grassIntervalMin  = 6f;
    public float grassIntervalMax  = 12f;
    public float grassAnimDuration = 1.2f;

    [Header("전투")]
    [Tooltip("워커가 주는 데미지 (1이면 1회 공격으로 사망)")]
    public int   huntDamage           = 1;
    public int   maxHp                = 1;
    [Tooltip("워커 감지 후 도망치는 속도 배율")]
    public float fleeSpeedMultiplier  = 2.5f;
    [Tooltip("워커 감지 거리")]
    public float detectionRange       = 3f;
}

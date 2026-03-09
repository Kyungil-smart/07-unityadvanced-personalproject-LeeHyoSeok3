using UnityEngine;

/// <summary>
/// 자원 노드 공통 밸런싱 데이터 (TreeNode / GoldNode 에서 사용)
/// AnimalNodeData 가 이 클래스를 상속해 동물 전용 수치를 추가한다.
/// </summary>
[CreateAssetMenu(menuName = "Data/ResourceNodeData", fileName = "NewResourceNodeData")]
public class ResourceNodeData : ScriptableObject
{
    [Header("자원량")]
    [Tooltip("노드 최대 보유량")]
    public int maxAmount = 100;

    [Header("채집")]
    [Tooltip("1회 채집 시 획득량")]
    public int   harvestAmountPerAction = 10;
    [Tooltip("채집 1회당 소모되는 일꾼 에너지")]
    public int   energyCostPerHarvest   = 10;
    [Tooltip("채집 완료까지 걸리는 시간 (초)")]
    public float harvestDuration        = 3f;
}

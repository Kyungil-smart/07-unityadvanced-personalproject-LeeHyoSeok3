using UnityEngine;

/// <summary>
/// 자원 인벤토리 밸런싱 데이터
/// ResourceInventory가 참조해 초기값·적재 한도를 읽는다.
/// </summary>
[CreateAssetMenu(menuName = "Data/ResourceInventoryData", fileName = "NewResourceInventoryData")]
public class ResourceInventoryData : ScriptableObject
{
    [Header("초기 자원량")]
    public int initialWood = 0;
    public int initialGold = 0;
    public int initialMeat = 0;

    [Header("고기 최대 적재량")]
    public int meatCapacity = 50;
}

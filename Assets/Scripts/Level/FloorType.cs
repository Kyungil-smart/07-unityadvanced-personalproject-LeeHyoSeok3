/// <summary>
/// 층 정의
/// Unity Sorting Layer 이름과 1:1 매핑
///
/// Project Settings → Tags and Layers → Sorting Layers 에
/// 아래 이름으로 순서대로 등록 필요:
///   Floor1, Floor2, Floor3
/// </summary>
public enum FloorType
{
    Floor1 = 1,
    Floor2 = 2,
    Floor3 = 3,
}

public static class FloorTypeExtension
{
    /// <summary>Sorting Layer 이름 반환</summary>
    public static string ToSortingLayer(this FloorType floor)
    {
        return floor switch
        {
            FloorType.Floor1 => "Floor1",
            FloorType.Floor2 => "Floor2",
            FloorType.Floor3 => "Floor3",
            _                => "Floor1",
        };
    }

    /// <summary>Order in Layer 기본값 (층이 높을수록 앞에 렌더링)</summary>
    public static int ToBaseOrder(this FloorType floor) => (int)floor * 100;
}

using UnityEngine;

public class FloorObject : MonoBehaviour
{
    [Header("초기 층")]
    public FloorType initialFloor = FloorType.Floor1;

    public FloorType CurrentFloor  { get; private set; }
    public bool      IsOnStair     { get; private set; }

    private SpriteRenderer[] _renderers;

    private const string LAYER_UNIT_FLOOR1    = "Unit_Floor1";
    private const string LAYER_UNIT_FLOOR2    = "Unit_Floor2";
    private const string LAYER_UNIT_FLOOR3    = "Unit_Floor3";
    private const string LAYER_STAIR_PASSING  = "StairPassing";

    void Start()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (_renderers.Length == 0)
            Debug.LogWarning($"[FloorObject] {gameObject.name} SpriteRenderer 없음");

        var detector = GetComponent<FloorDetector>();
        FloorType startFloor = detector != null
            ? detector.DetectCurrentFloor()
            : initialFloor;

        SetFloor(startFloor);
    }

    public void SetOnStair(bool onStair)
    {
        IsOnStair = onStair;

        if (onStair)
        {
            int layer = LayerMask.NameToLayer(LAYER_STAIR_PASSING);
            if (layer == -1) { Debug.LogError("[FloorObject] 'StairPassing' Layer 없음!"); return; }
            gameObject.layer = layer;
        }
        else
        {
            ApplyCurrentPhysicsLayer();
        }
    }

    public void SetFloor(FloorType floor)
    {
        CurrentFloor = floor;

        if (_renderers == null)
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);

        ApplySortingLayer();

        if (!IsOnStair)
            ApplyCurrentPhysicsLayer();

        EventBus.Publish(new OnFloorChanged { obj = gameObject, floor = floor });
    }

    public void ApplyCurrentPhysicsLayer()
    {
        string layerName = CurrentFloor switch
        {
            FloorType.Floor1 => LAYER_UNIT_FLOOR1,
            FloorType.Floor2 => LAYER_UNIT_FLOOR2,
            FloorType.Floor3 => LAYER_UNIT_FLOOR3,
            _                => LAYER_UNIT_FLOOR1,
        };

        int layerIndex = LayerMask.NameToLayer(layerName);
        if (layerIndex == -1) { Debug.LogError($"[FloorObject] Physics Layer '{layerName}' 없음!"); return; }
        gameObject.layer = layerIndex;
    }

    void ApplySortingLayer()
    {
        string layerName = CurrentFloor.ToSortingLayer();

        if (!SortingLayerExists(layerName))
        {
            Debug.LogError($"[FloorObject] Sorting Layer '{layerName}' 없음!");
            return;
        }

        foreach (var sr in _renderers)
        {
            if (sr == null) continue;
            sr.sortingLayerName = layerName;
        }
    }

    static bool SortingLayerExists(string layerName)
    {
        foreach (var layer in SortingLayer.layers)
            if (layer.name == layerName) return true;
        return false;
    }
}

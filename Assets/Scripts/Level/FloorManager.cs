using System.Collections.Generic;
using UnityEngine;

public class FloorManager : MonoBehaviour
{
    public static FloorManager Instance { get; private set; }

    private readonly Dictionary<FloorType, HashSet<FloorObject>> _floorMap = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        foreach (FloorType f in System.Enum.GetValues(typeof(FloorType)))
            _floorMap[f] = new HashSet<FloorObject>();
    }

    void OnEnable()
    {
        EventBus.Subscribe<OnFloorChanged>(HandleFloorChanged);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<OnFloorChanged>(HandleFloorChanged);
    }

    void HandleFloorChanged(OnFloorChanged e)
    {
        var floorObj = e.obj.GetComponent<FloorObject>();
        if (floorObj == null) return;

        foreach (var set in _floorMap.Values)
            set.Remove(floorObj);

        _floorMap[e.floor].Add(floorObj);
    }

    public IReadOnlyCollection<FloorObject> GetObjectsOnFloor(FloorType floor)
        => _floorMap.TryGetValue(floor, out var set) ? set : null;

    public FloorType GetFloor(GameObject obj)
        => obj.TryGetComponent<FloorObject>(out var fo) ? fo.CurrentFloor : FloorType.Floor1;

    public bool IsSameFloor(GameObject a, GameObject b)
        => GetFloor(a) == GetFloor(b);
}
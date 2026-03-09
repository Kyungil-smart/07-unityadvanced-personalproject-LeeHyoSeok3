using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어가 보유한 자원량 관리
///
/// Wood, Gold : 소모형 - 상한 없음
/// Meat       : 적재형 - meatCapacity 한도까지만 보유 가능
///
/// ServiceLocator로 접근:
///   ServiceLocator.Get<ResourceInventory>()
/// </summary>
public class ResourceInventory : MonoBehaviour
{
    [Header("밸런싱 데이터")]
    [SerializeField] private ResourceInventoryData _data;

    // 실제 보유량
    private Dictionary<ResourceType, int> _resources;

    // 초기화 스냅샷 (전략 초기화용)
    private Dictionary<ResourceType, int> _initialSnapshot;

    public int meatCapacity => _data != null ? _data.meatCapacity : 50;

    void Awake()
    {
        _resources = new Dictionary<ResourceType, int>
        {
            { ResourceType.Wood, _data != null ? _data.initialWood : 0 },
            { ResourceType.Meat, _data != null ? _data.initialMeat : 0 },
            { ResourceType.Gold, _data != null ? _data.initialGold : 0 }
        };

        SaveSnapshot();
        ServiceLocator.Register<ResourceInventory>(this);
    }

    // -------------------------------------------------------
    // 조회
    // -------------------------------------------------------
    public int Get(ResourceType type)
    {
        return _resources.TryGetValue(type, out int val) ? val : 0;
    }

    public bool CanAfford(ResourceType type, int amount)
    {
        return Get(type) >= amount;
    }

    /// <summary>여러 자원을 동시에 지불 가능한지 확인</summary>
    public bool CanAffordAll(Dictionary<ResourceType, int> costs)
    {
        foreach (var cost in costs)
        {
            if (!CanAfford(cost.Key, cost.Value)) return false;
        }
        return true;
    }

    // -------------------------------------------------------
    // 추가
    // -------------------------------------------------------
    public void Add(ResourceType type, int amount)
    {
        if (amount <= 0) return;

        _resources[type] += amount;

        // Meat는 최대 적재량 초과 불가
        if (type == ResourceType.Meat)
            _resources[type] = Mathf.Min(_resources[type], meatCapacity);

        PublishChanged(type, amount);
        Debug.Log($"[ResourceInventory] {type} +{amount} → 현재: {_resources[type]}");
    }

    // -------------------------------------------------------
    // 소모
    // -------------------------------------------------------
    /// <summary>자원 소모. 부족하면 false 반환</summary>
    public bool Spend(ResourceType type, int amount)
    {
        if (!CanAfford(type, amount))
        {
            Debug.LogWarning($"[ResourceInventory] {type} 부족. 필요: {amount}, 보유: {Get(type)}");
            return false;
        }

        _resources[type] -= amount;
        PublishChanged(type, -amount);
        Debug.Log($"[ResourceInventory] {type} -{amount} → 현재: {_resources[type]}");
        return true;
    }

    /// <summary>여러 자원을 동시에 소모. 하나라도 부족하면 전체 실패</summary>
    public bool SpendAll(Dictionary<ResourceType, int> costs)
    {
        if (!CanAffordAll(costs)) return false;

        foreach (var cost in costs)
            Spend(cost.Key, cost.Value);

        return true;
    }

    // -------------------------------------------------------
    // 전략 초기화 (PreparePhase에서만 허용)
    // -------------------------------------------------------
    public void SaveSnapshot()
    {
        _initialSnapshot = new Dictionary<ResourceType, int>(_resources);
    }

    public void RestoreSnapshot()
    {
        foreach (var key in _initialSnapshot.Keys)
        {
            _resources[key] = _initialSnapshot[key];
            PublishChanged(key, 0);
        }
        Debug.Log("[ResourceInventory] 자원 초기화 완료");
    }

    // -------------------------------------------------------
    // 내부
    // -------------------------------------------------------
    private void PublishChanged(ResourceType type, int delta)
    {
        EventBus.Publish(new OnResourceChanged
        {
            type    = type,
            current = Get(type),
            delta   = delta
        });
    }
}

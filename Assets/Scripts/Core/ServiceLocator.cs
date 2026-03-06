using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 각 시스템을 중앙에서 접근하는 Service Locator
///
/// 사용법:
///   등록: ServiceLocator.Register<ResourceInventory>(this);
///   접근: ServiceLocator.Get<ResourceInventory>().Add(...);
/// </summary>
[DefaultExecutionOrder(-50)]
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services
        = new Dictionary<Type, object>();

    public static void Register<T>(T service)
    {
        var type = typeof(T);
        if (_services.ContainsKey(type))
            Debug.LogWarning($"[ServiceLocator] {type.Name} 이미 등록됨. 덮어씁니다.");
        _services[type] = service;
    }

    public static T Get<T>()
    {
        if (_services.TryGetValue(typeof(T), out var service))
            return (T)service;

        Debug.LogError($"[ServiceLocator] {typeof(T).Name} 등록되지 않음.");
        return default;
    }

    public static bool Has<T>() => _services.ContainsKey(typeof(T));

    public static void Clear() => _services.Clear();
}
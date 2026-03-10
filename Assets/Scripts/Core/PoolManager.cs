using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 게임 전체 오브젝트 풀 중앙 관리자
///
/// 사용법:
///   스폰: ServiceLocator.Get&lt;PoolManager&gt;().Spawn(prefab, pos, rot)
///   반납: ServiceLocator.Get&lt;PoolManager&gt;().Despawn(prefab, instance)
///
/// 씬에 하나만 배치 (GameManager와 동일 오브젝트에 추가 가능)
/// </summary>
[DefaultExecutionOrder(-40)] // ServiceLocator(-50) 다음, 일반 MonoBehaviour 보다 먼저
public class PoolManager : MonoBehaviour
{
    [Header("사전 워밍 목록 (시작 시 미리 인스턴스 생성)")]
    [SerializeField] private PoolEntry[] _warmupEntries;

    [System.Serializable]
    public struct PoolEntry
    {
        public GameObject prefab;
        [Tooltip("준비 시 미리 만들 개수")]
        public int        initialCount;
        [Tooltip("풀 최대 크기 (초과 시 Destroy)")]
        public int        maxCount;
    }

    // ---
    // 내부
    // ---
    private readonly Dictionary<GameObject, GameObjectPool> _pools    = new();
    private Transform _poolRoot;

    // ---

    void Awake()
    {
        ServiceLocator.Register<PoolManager>(this);

        // 비활성 오브젝트를 숨길 루트 생성
        var rootGo = new GameObject("[Pool Root]");
        rootGo.transform.SetParent(transform);
        _poolRoot = rootGo.transform;
    }

    void Start()
    {
        // 사전 워밍: 풀 생성 + initialCount 개수만큼 미리 인스턴스화
        foreach (var entry in _warmupEntries)
        {
            if (entry.prefab == null) continue;

            var pool   = GetOrCreatePool(entry.prefab, entry.initialCount, entry.maxCount);
            var buffer = new GameObject[entry.initialCount];

            for (int i = 0; i < entry.initialCount; i++)
                buffer[i] = pool.Get();

            foreach (var go in buffer)
                pool.Return(go);

            Debug.Log($"[PoolManager] '{entry.prefab.name}' 풀 워밍 완료 ({entry.initialCount}개)");
        }
    }

    // ---
    // 공개 API
    // ---

    /// <summary>
    /// 풀에서 오브젝트를 꺼내 위치/회전을 적용하고 반환
    /// prefab이 처음 요청되면 풀을 자동 생성
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("[PoolManager] Spawn: prefab이 null입니다.");
            return null;
        }

        var pool = GetOrCreatePool(prefab);
        var go   = pool.Get();

        go.transform.position = position;
        go.transform.rotation = rotation;

        return go;
    }

    /// <summary>
    /// 오브젝트를 풀에 반납
    /// prefab에 해당하는 풀이 없으면 Destroy로 폴백
    /// </summary>
    public void Despawn(GameObject prefab, GameObject instance)
    {
        if (instance == null) return;

        if (prefab != null && _pools.TryGetValue(prefab, out var pool))
        {
            pool.Return(instance);
        }
        else
        {
            Debug.LogWarning($"[PoolManager] Despawn: '{instance.name}' 풀 없음 → Destroy 폴백");
            Destroy(instance);
        }
    }

    /// <summary>
    /// 어드레서블 비동기 로드 후 풀에서 스폰
    /// 최초 호출 시 에셋을 로드하고, 이후 동일 AssetReference는 캐시된 prefab 사용
    /// </summary>
    public void SpawnAsync(
        AssetReferenceGameObject assetRef,
        Vector3                  position,
        Quaternion               rotation,
        Action<GameObject>       onComplete)
    {
        if (assetRef == null || !assetRef.RuntimeKeyIsValid())
        {
            Debug.LogError("[PoolManager] SpawnAsync: assetRef가 null이거나 유효하지 않습니다.");
            onComplete?.Invoke(null);
            return;
        }

        string key = assetRef.RuntimeKey.ToString();

        // 이미 로드된 prefab이 있으면 즉시 스폰
        if (_assetRefToPrefab.TryGetValue(key, out var cachedPrefab))
        {
            var go = Spawn(cachedPrefab, position, rotation);
            onComplete?.Invoke(go);
            return;
        }

        // 최초 로드 (비동기)
        var handle = Addressables.LoadAssetAsync<GameObject>(assetRef);
        handle.Completed += (op) =>
        {
            if (op.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[PoolManager] 어드레서블 로드 실패: {key}");
                onComplete?.Invoke(null);
                return;
            }

            var prefab = op.Result;
            _assetRefToPrefab[key] = prefab;

            var go = Spawn(prefab, position, rotation);
            onComplete?.Invoke(go);
        };
    }

    /// <summary>어드레서블 키로 풀에 반납</summary>
    public void DespawnByKey(string assetKey, GameObject instance)
    {
        if (_assetRefToPrefab.TryGetValue(assetKey, out var prefab))
            Despawn(prefab, instance);
        else
        {
            Debug.LogWarning($"[PoolManager] DespawnByKey: 키 '{assetKey}' 없음 → Destroy 폴백");
            Destroy(instance);
        }
    }

    // ---
    // 내부
    // ---

    // 어드레서블 RuntimeKey → 로드된 prefab 캐시
    private readonly Dictionary<string, GameObject> _assetRefToPrefab = new();

    private GameObjectPool GetOrCreatePool(
        GameObject prefab,
        int        defaultCapacity = 10,
        int        maxSize         = 50)
    {
        if (!_pools.TryGetValue(prefab, out var pool))
        {
            pool = new GameObjectPool(prefab, _poolRoot, defaultCapacity, maxSize);
            _pools[prefab] = pool;
        }
        return pool;
    }
}

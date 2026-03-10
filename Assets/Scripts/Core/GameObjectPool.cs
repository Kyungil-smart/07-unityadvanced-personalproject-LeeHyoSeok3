using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 단일 프리팹에 대한 오브젝트 풀 래퍼
/// UnityEngine.Pool.ObjectPool&lt;GameObject&gt;를 내부적으로 사용
/// </summary>
public class GameObjectPool
{
    private readonly GameObject _prefab;
    private readonly Transform  _poolRoot;
    private readonly ObjectPool<GameObject> _pool;

    // ---

    public GameObjectPool(
        GameObject prefab,
        Transform  poolRoot,
        int        defaultCapacity = 10,
        int        maxSize         = 50)
    {
        _prefab   = prefab;
        _poolRoot = poolRoot;

        _pool = new ObjectPool<GameObject>(
            createFunc:      CreateInstance,
            actionOnGet:     OnGet,
            actionOnRelease: OnRelease,
            actionOnDestroy: OnPoolDestroy,
            // 에디터에서만 이중 반납 검사 (성능 영향 없음)
            collectionCheck: Application.isEditor,
            defaultCapacity: defaultCapacity,
            maxSize:         maxSize
        );
    }

    // ---

    /// <summary>풀에서 오브젝트 꺼내기</summary>
    public GameObject Get() => _pool.Get();

    /// <summary>풀에 오브젝트 반납</summary>
    public void Return(GameObject go)
    {
        if (go == null) return;
        _pool.Release(go);
    }

    public int CountInactive => _pool.CountInactive;
    public int CountActive   => _pool.CountActive;

    // ---
    // 내부 콜백
    // ---

    private GameObject CreateInstance()
    {
        var go = Object.Instantiate(_prefab, _poolRoot);
        go.SetActive(false);
        return go;
    }

    private void OnGet(GameObject go)
    {
        go.SetActive(true);
    }

    private void OnRelease(GameObject go)
    {
        go.SetActive(false);
        go.transform.SetParent(_poolRoot);
    }

    private void OnPoolDestroy(GameObject go)
    {
        Object.Destroy(go);
    }
}

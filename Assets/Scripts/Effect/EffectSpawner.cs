using UnityEngine;

/// <summary>
/// 이펙트 산포 생성 정적 유틸리티
///
/// UnitBase / ResourceNode 등 서로 다른 상속 계층에서 동일한 방식으로 호출
///
/// 사용 예시:
///   EffectSpawner.SpawnScattered(prefab, spriteRenderer, count, radius);
///   EffectSpawner.SpawnScattered(prefab, center, count, radius);
/// </summary>
public static class EffectSpawner
{
    /// <summary>
    /// SpriteRenderer의 bounds.center를 기준으로 이펙트를 산포 생성
    /// spriteRenderer가 null이면 fallbackCenter 사용
    /// </summary>
    public static void SpawnScattered(
        GameObject     prefab,
        SpriteRenderer spriteRenderer,
        Vector3        fallbackCenter,
        int            count,
        float          radius,
        float          scaleMin = 1f,
        float          scaleMax = 1f)
    {
        if (prefab == null) return;

        Vector3 center = (spriteRenderer != null)
            ? spriteRenderer.bounds.center
            : fallbackCenter;

        SpawnScattered(prefab, center, count, radius, scaleMin, scaleMax);
    }

    /// <summary>
    /// 지정한 center 기준으로 이펙트를 산포 생성
    /// </summary>
    public static void SpawnScattered(
        GameObject prefab,
        Vector3    center,
        int        count,
        float      radius,
        float      scaleMin = 1f,
        float      scaleMax = 1f)
    {
        if (prefab == null) return;

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 pos    = center + new Vector3(offset.x, offset.y, 0f);
            float   scale  = Random.Range(scaleMin, scaleMax);
            SpawnSingle(prefab, pos, scale);
        }
    }

    // ---

    private static void SpawnSingle(GameObject prefab, Vector3 position, float scale)
    {
        GameObject fx = ServiceLocator.Has<PoolManager>()
            ? ServiceLocator.Get<PoolManager>().Spawn(prefab, position, Quaternion.identity)
            : Object.Instantiate(prefab, position, Quaternion.identity);

        fx.transform.localScale = Vector3.one * scale;

        var effect = fx.GetComponent<PooledEffect>();
        if (effect != null)
            effect.sourcePrefab = prefab;
        else
            Object.Destroy(fx, 2f);
    }
}

using System.Collections;
using UnityEngine;

/// <summary>
/// 전역 이펙트 생성 서비스
///
/// 모든 이펙트 코루틴의 실행 주체 — 요청 오브젝트가 파괴돼도 코루틴 유지
///
/// ServiceLocator 등록 후 사용:
///   ServiceLocator.Get&lt;EffectManager&gt;().Play(request)
///
/// 초기화 순서: -100 (다른 스크립트 Awake 이전 등록 보장)
/// </summary>
[DefaultExecutionOrder(-100)]
public class EffectManager : MonoBehaviour
{
    void Awake()
    {
        ServiceLocator.Register<EffectManager>(this);
    }

    // -------------------------------------------------------
    // 공개 API
    // -------------------------------------------------------

    /// <summary>
    /// 이펙트 요청 실행. EffectHandle을 반환하며 Continuous/Sequential 취소에 사용
    /// Burst는 즉시 완료된 비활성 핸들 반환
    /// </summary>
    public EffectHandle Play(EffectRequest request)
    {
        if (request.prefab == null)
        {
            Debug.LogWarning("[EffectManager] prefab이 null인 요청 무시");
            var empty = new EffectHandle();
            empty.Complete();
            return empty;
        }

        return request.mode switch
        {
            EffectSpawnMode.Burst      => RunBurst(request),
            EffectSpawnMode.Sequential => RunSequential(request),
            EffectSpawnMode.Continuous => RunContinuous(request),
            _                         => RunBurst(request),
        };
    }

    // -------------------------------------------------------
    // 모드별 실행
    // -------------------------------------------------------

    EffectHandle RunBurst(EffectRequest r)
    {
        SpawnBatch(r);
        var handle = new EffectHandle();
        handle.Complete();
        return handle;
    }

    EffectHandle RunSequential(EffectRequest r)
    {
        var handle = new EffectHandle();
        Coroutine co = StartCoroutine(SequentialRoutine(r, handle));
        handle.OnStop = () => StopCoroutine(co);
        return handle;
    }

    EffectHandle RunContinuous(EffectRequest r)
    {
        var handle = new EffectHandle();
        Coroutine co = StartCoroutine(ContinuousRoutine(r, handle));
        handle.OnStop = () => StopCoroutine(co);
        return handle;
    }

    // -------------------------------------------------------
    // 코루틴
    // -------------------------------------------------------

    /// <summary>interval 간격으로 1개씩 count번 생성 후 자연 완료</summary>
    IEnumerator SequentialRoutine(EffectRequest r, EffectHandle handle)
    {
        for (int i = 0; i < r.count; i++)
        {
            SpawnOne(r);
            yield return new WaitForSeconds(r.interval);
        }
        handle.Complete();
    }

    /// <summary>handle이 살아있는 동안 interval마다 count개씩 반복 생성</summary>
    IEnumerator ContinuousRoutine(EffectRequest r, EffectHandle handle)
    {
        while (handle.IsActive)
        {
            SpawnBatch(r);
            yield return new WaitForSeconds(r.interval);
        }
    }

    // -------------------------------------------------------
    // 생성 헬퍼
    // -------------------------------------------------------

    void SpawnBatch(EffectRequest r)
    {
        EffectSpawner.SpawnScattered(
            r.prefab, GetCenter(r), r.count, r.radius, r.scaleMin, r.scaleMax);
    }

    void SpawnOne(EffectRequest r)
    {
        EffectSpawner.SpawnScattered(
            r.prefab, GetCenter(r), 1, r.radius, r.scaleMin, r.scaleMax);
    }

    /// <summary>
    /// 생성 중심 위치 결정
    /// source 살아있음 → SpriteRenderer.bounds.center 또는 source.position
    /// source 파괴됨  → capturedPosition 폴백
    /// </summary>
    Vector3 GetCenter(in EffectRequest r)
    {
        if (r.source != null)
        {
            return r.sourceRenderer != null
                ? r.sourceRenderer.bounds.center
                : r.source.position;
        }
        return r.capturedPosition;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이펙트 생성 모드
/// </summary>
public enum EffectSpawnMode
{
    /// <summary>일반 생성 — count개를 즉시 한꺼번에 생성 후 종료</summary>
    Burst,
    /// <summary>순차 생성 — interval 간격으로 count개를 1개씩 생성 후 종료 (소스 파괴 후에도 완주)</summary>
    Sequential,
    /// <summary>연속 생성 — 소스 오브젝트가 파괴되기 전까지 interval 간격으로 count개씩 반복 생성</summary>
    Continuous,
}

/// <summary>
/// 이펙트 설정 컨테이너 + 트리거 컴포넌트
///
/// 코루틴 실행은 EffectManager에 위임 — 소스 파괴와 이펙트 생명주기 분리
///
/// 모드별 동작:
///   Burst      : Play() 시점에 즉시 생성 후 완료
///   Sequential : EffectManager에서 완주 (소스 파괴 후에도 계속)
///   Continuous : 소스 OnDestroy 시 자동 중단
/// </summary>
public class EffectSpawnController : MonoBehaviour
{
    [Header("이펙트 프리팹 목록")]
    [Tooltip("생성할 이펙트 프리팹 리스트 — Play() 호출 시 랜덤으로 1개 선택 (PooledEffect 컴포넌트 권장)")]
    public List<GameObject> effectPrefabs = new List<GameObject>();

    [Header("생성 모드")]
    [Tooltip("Burst: 즉시 count개 / Sequential: interval 간격으로 count개 / Continuous: 파괴 전까지 반복")]
    public EffectSpawnMode mode = EffectSpawnMode.Burst;

    [Header("생성 옵션")]
    [Tooltip("Burst: 한 번에 생성할 개수\nSequential: 총 생성 개수\nContinuous: 한 틱마다 생성할 개수")]
    public int count = 3;

    [Tooltip("Sequential / Continuous 전용 — 생성 간격 (초)")]
    public float interval = 0.5f;

    [Header("산포 설정")]
    [Tooltip("이펙트 산포 반지름")]
    public float radius = 0.3f;

    [Tooltip("이펙트 랜덤 스케일 최솟값")]
    public float scaleMin = 1f;

    [Tooltip("이펙트 랜덤 스케일 최댓값")]
    public float scaleMax = 1f;

    [Header("옵션")]
    [Tooltip("Start()에서 자동으로 Play() 호출")]
    public bool playOnStart = false;

    // ---

    private SpriteRenderer _spriteRenderer;

    // Continuous 핸들만 보관 — OnDestroy 시 자동 중단
    // Sequential은 EffectManager가 완주를 책임지므로 저장 불필요
    private EffectHandle _continuousHandle;

    // -------------------------------------------------------
    // Unity 생명주기
    // -------------------------------------------------------

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        if (playOnStart) Play();
    }

    void OnDestroy()
    {
        // Continuous 모드만 중단 — Sequential은 EffectManager에서 완주
        _continuousHandle?.Stop();
    }

    void OnDrawGizmosSelected()
    {
        var sr = GetComponent<SpriteRenderer>();
        Vector3 center = (sr != null) ? sr.bounds.center : transform.position;
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(center, radius);
    }

    // -------------------------------------------------------
    // 공개 API
    // -------------------------------------------------------

    /// <summary>
    /// 설정된 모드로 이펙트 생성 시작. EffectHandle 반환
    /// Continuous → 반환된 핸들로 외부에서 조기 중단 가능
    /// </summary>
    public EffectHandle Play()
    {
        var selectedPrefab = PickRandomPrefab();
        if (selectedPrefab == null)
        {
            var empty = new EffectHandle();
            empty.Complete();
            return empty;
        }

        if (!ServiceLocator.Has<EffectManager>())
        {
            Debug.LogWarning("[EffectSpawnController] EffectManager가 ServiceLocator에 없음");
            var empty = new EffectHandle();
            empty.Complete();
            return empty;
        }

        // 이전 Continuous 중단 후 새 요청 전송
        _continuousHandle?.Stop();

        var request = BuildRequest(selectedPrefab);
        var handle  = ServiceLocator.Get<EffectManager>().Play(request);

        if (mode == EffectSpawnMode.Continuous)
            _continuousHandle = handle;

        return handle;
    }

    /// <summary>
    /// 지정한 월드 좌표에서 이펙트 생성.
    /// SpriteRenderer bounds 대신 명시적 위치를 사용하므로 위치 오류 없음.
    /// </summary>
    public EffectHandle PlayAt(Vector3 worldPosition)
    {
        var selectedPrefab = PickRandomPrefab();
        if (selectedPrefab == null)
        {
            var empty = new EffectHandle();
            empty.Complete();
            return empty;
        }

        if (!ServiceLocator.Has<EffectManager>())
        {
            Debug.LogWarning("[EffectSpawnController] EffectManager가 ServiceLocator에 없음");
            var empty = new EffectHandle();
            empty.Complete();
            return empty;
        }

        _continuousHandle?.Stop();

        // source/sourceRenderer를 null로 두어 capturedPosition만 사용하도록 함
        var request = new EffectRequest
        {
            prefab           = selectedPrefab,
            mode             = mode,
            count            = count,
            interval         = interval,
            radius           = radius,
            scaleMin         = scaleMin,
            scaleMax         = scaleMax,
            source           = null,
            sourceRenderer   = null,
            capturedPosition = worldPosition,
        };

        var handle = ServiceLocator.Get<EffectManager>().Play(request);

        if (mode == EffectSpawnMode.Continuous)
            _continuousHandle = handle;

        return handle;
    }

    /// <summary>진행 중인 Continuous 생성을 명시적으로 중단</summary>
    public void StopSpawn()
    {
        _continuousHandle?.Stop();
        _continuousHandle = null;
    }

    // -------------------------------------------------------
    // 내부
    // -------------------------------------------------------

    /// <summary>effectPrefabs 리스트에서 랜덤으로 1개 선택. 유효한 항목이 없으면 null 반환</summary>
    GameObject PickRandomPrefab()
    {
        if (effectPrefabs == null || effectPrefabs.Count == 0)
        {
            Debug.LogWarning($"[EffectSpawnController] {name}: effectPrefabs 리스트가 비어있음");
            return null;
        }

        // null 항목을 제외한 유효한 프리팹만 추려서 랜덤 선택
        var valid = effectPrefabs.FindAll(p => p != null);
        if (valid.Count == 0)
        {
            Debug.LogWarning($"[EffectSpawnController] {name}: effectPrefabs에 유효한 프리팹이 없음");
            return null;
        }

        return valid[Random.Range(0, valid.Count)];
    }

    EffectRequest BuildRequest(GameObject prefab) => new EffectRequest
    {
        prefab           = prefab,
        mode             = mode,
        count            = count,
        interval         = interval,
        radius           = radius,
        scaleMin         = scaleMin,
        scaleMax         = scaleMax,
        source           = transform,
        sourceRenderer   = _spriteRenderer,
        capturedPosition = _spriteRenderer != null
            ? _spriteRenderer.bounds.center
            : transform.position,
    };
}

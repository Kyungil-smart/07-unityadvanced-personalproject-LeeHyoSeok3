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
    [Header("이펙트 프리팹")]
    [Tooltip("생성할 이펙트 프리팹 (PooledEffect 컴포넌트 권장)")]
    public GameObject effectPrefab;

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
        if (effectPrefab == null)
        {
            Debug.LogWarning($"[EffectSpawnController] {name}: effectPrefab 미설정");
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

        var request = BuildRequest();
        var handle  = ServiceLocator.Get<EffectManager>().Play(request);

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

    EffectRequest BuildRequest() => new EffectRequest
    {
        prefab           = effectPrefab,
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

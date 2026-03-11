using UnityEngine;

/// <summary>
/// EffectManager에 전달하는 이펙트 생성 요청 데이터
///
/// source가 살아있는 동안은 source.position / SpriteRenderer.bounds.center를 기준으로 생성
/// source가 파괴된 후에는 capturedPosition을 폴백으로 사용
/// </summary>
public struct EffectRequest
{
    public GameObject      prefab;
    public EffectSpawnMode mode;
    public int             count;
    public float           interval;
    public float           radius;
    public float           scaleMin;
    public float           scaleMax;

    /// <summary>생성 위치 추적용 Transform (파괴 후 capturedPosition으로 폴백)</summary>
    public Transform       source;

    /// <summary>source 위치 보정용 — bounds.center 계산에 사용 (선택)</summary>
    public SpriteRenderer  sourceRenderer;

    /// <summary>source 파괴 후 사용할 고정 위치</summary>
    public Vector3         capturedPosition;
}

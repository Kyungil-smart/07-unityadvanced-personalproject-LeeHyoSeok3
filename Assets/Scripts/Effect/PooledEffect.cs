using UnityEngine;

/// <summary>
/// 풀링 가능한 이펙트 프리팹에 부착하는 범용 컴포넌트
///
/// 사망 먼지, 히트 스파크, 스폰 이펙트 등 모든 일회성 이펙트 프리팹에 사용
///
/// 반납 타이밍: 애니메이션 마지막 프레임에 Animation Event로 OnEffectEnd() 호출
///
/// 설정 방법:
///   1. 이 스크립트를 이펙트 프리팹 루트에 부착
///   2. Animator의 마지막 프레임에 Animation Event 추가
///      - Function: OnEffectEnd
/// </summary>
public class PooledEffect : MonoBehaviour
{
    // 원본 프리팹 참조 (PoolManager 반납에 사용)
    // 스폰 측에서 주입
    [HideInInspector] public GameObject sourcePrefab;

    void OnEnable()
    {
        // 활성화될 때마다 애니메이션 처음부터 재생
        var animator = GetComponent<Animator>();
        if (animator != null)
            animator.Play(0, -1, 0f);
    }

    /// <summary>
    /// 애니메이션 마지막 프레임의 Animation Event에서 호출
    /// </summary>
    public void OnEffectEnd()
    {
        if (sourcePrefab != null && ServiceLocator.Has<PoolManager>())
        {
            ServiceLocator.Get<PoolManager>().Despawn(sourcePrefab, gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}

using System;

/// <summary>
/// EffectManager가 반환하는 이펙트 생명주기 핸들
///
/// Sequential : 완주 전 조기 취소 가능
/// Continuous : 소유자 OnDestroy에서 Stop() 호출
/// Burst      : 이미 완료된 비활성 핸들 반환 (Stop 불필요)
/// </summary>
public class EffectHandle
{
    public bool IsActive { get; private set; } = true;

    // EffectManager가 코루틴 참조를 주입
    internal Action OnStop;

    /// <summary>이펙트 생성 중단. 이미 중단됐거나 완료된 경우 무시</summary>
    public void Stop()
    {
        if (!IsActive) return;
        IsActive = false;
        OnStop?.Invoke();
        OnStop = null;
    }

    /// <summary>시퀀스 자연 완료 시 EffectManager 내부에서 호출</summary>
    internal void Complete()
    {
        IsActive = false;
        OnStop   = null;
    }
}

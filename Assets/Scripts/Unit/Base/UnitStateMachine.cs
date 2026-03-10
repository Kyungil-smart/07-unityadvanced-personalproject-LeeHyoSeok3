/// <summary>
/// 유닛 상태 열거형
/// </summary>
public enum UnitState
{
    Idle,           // 대기
    Move,           // 이동 (일반)
    Harvest,        // 채집 (구버전 호환용)
    Build,          // 건설
    Attack,         // 공격 (전투 유닛 전용)
    Dead,           // 사망

    // 워커 전용 이동 상태
    Build_Move,     // 건설 위치로 이동
    Logging_Move,   // 벌목 위치로 이동
    Mining_Move,    // 채광 위치로 이동
    Hunting_Move,   // 사냥 위치로 이동

    // 워커 전용 작업 상태
    Logging,        // 벌목 중
    Mining,         // 채광 중
    Hunting,        // 사냥 중
    HuntingIdle,    // 사냥 쿨타임 대기
    Logging_Idle,   // 벌목 채집물 착지 대기 (DroppedResource 착지 전 임시 대기)
    Mining_Idle,    // 채광 채집물 착지 대기 (DroppedResource 착지 전 임시 대기)

    // 자원 회수 이동
    Grab_Wood_Move, // 나무 들고 본진으로
    Grab_Gold_Move, // 금 들고 본진으로
    Grab_Meat_Move, // 고기 들고 본진으로
}

/// <summary>
/// 유닛 상태 머신
/// 상태 전환 시 Enter/Exit 훅 호출
/// </summary>
public class UnitStateMachine
{
    public UnitState Current  { get; private set; } = UnitState.Idle;
    public UnitState Previous { get; private set; } = UnitState.Idle;

    private readonly UnitBase _owner;

    public UnitStateMachine(UnitBase owner) { _owner = owner; }

    public void ChangeState(UnitState newState)
    {
        if (Current == newState) return;
        if (Current == UnitState.Dead) return;

        Previous = Current;
        Current  = newState;

        _owner.OnStateExit(Previous);
        _owner.OnStateEnter(newState);
    }

    public bool Is(UnitState state)     => Current == state;
    public bool WasIn(UnitState state)  => Previous == state;

    /// <summary>
    /// 풀 반납 전 상태 초기화용
    /// Dead 락을 우회하여 상태를 Idle로 직접 리셋
    /// OnStateEnter/Exit 훅은 호출하지 않음
    /// </summary>
    public void Reset()
    {
        Previous = UnitState.Idle;
        Current  = UnitState.Idle;
    }
}
/// <summary>
/// 전체 게임 상태
/// </summary>
public enum GameState
{
    Ready,      // 씬 로드 직후 초기화 중
    Playing,    // 게임 진행 중
    Paused,     // 일시정지
    GameOver,   // 본진(Castle) 파괴
    Clear       // 모든 웨이브 방어 성공
}

/// <summary>
/// 게임 내 페이즈 (Playing 상태 안에서 세분화)
/// </summary>
public enum PhaseType
{
    None,
    Prepare,    // 침공 전 - 자원수집/건물건설/초기화 가능
    Combat      // 침공 중 - 유닛 자동스폰/적 침공
}

/// <summary>
/// 점수 페널티 항목
/// </summary>
public enum ScoreType
{
    ResourceHarvest,    // 자원 수집
    UnitDeath,          // 유닛 사망
    BuildingPlaced,     // 건물 건설
    BuildingDestroyed   // 건물 파괴
}
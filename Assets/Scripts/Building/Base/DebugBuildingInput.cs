using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 디버그용 건물 배치 입력
///
/// Q → ClusterBuilding 배치 모드 시작
/// 이후 BuildingPlacer가 미리보기 + 스냅 + 클릭 배치 처리
/// </summary>
public class DebugBuildingInput : MonoBehaviour
{
    [Header("디버그 배치 데이터")]
    public BuildingData clusterData;   // Inspector에 ClusterBuilding Data 연결

    private BuildingPlacer _placer;

    void Start()
    {
        _placer = ServiceLocator.Get<BuildingPlacer>();
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        // Q 누르면 배치 시작 (토글)
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            if (_placer == null)
            {
                Debug.LogError("[DebugBuildingInput] BuildingPlacer 없음");
                return;
            }

            if (clusterData == null)
            {
                Debug.LogError("[DebugBuildingInput] clusterData 없음 - Inspector에 연결 필요");
                return;
            }

            // 이미 배치 모드 중이면 취소, 아니면 시작
            if (_placer.IsPlacing)
                _placer.CancelPlacement();
            else
                _placer.StartPlacement(clusterData);
        }
    }
}
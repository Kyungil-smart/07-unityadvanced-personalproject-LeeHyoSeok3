using UnityEngine;

/// <summary>
/// 스테이지 정보 데이터 (ScriptableObject)
/// Create → Data → StageData
/// </summary>
[CreateAssetMenu(menuName = "Data/StageData", fileName = "Stage_01")]
public class StageData : ScriptableObject
{
    [Header("기본 정보")]
    public string stageName = "Stage 1";
    public string sceneName = "GameScene_01";

    [Header("스테이지 선택 씬 - 카메라")]
    [Tooltip("이 스테이지 선택 시 카메라가 이동할 월드 좌표")]
    public Vector3 cameraPosition = Vector3.zero;
    [Tooltip("카메라 Orthographic Size (줌 레벨)")]
    public float   cameraSize     = 5f;

    [Header("스테이지 선택 씬 - 하단 애니 오브젝트")]
    [Tooltip("UI 하단 여백에 배치할 스테이지 전용 프리팹들 (건물, 유닛 등)")]
    public GameObject[] sceneObjectPrefabs;

    [Header("클리어 기록 (자동 저장)")]
    [HideInInspector] public bool isClear   = false;
    [HideInInspector] public int  bestStars = 0;   // 0 ~ 3
}
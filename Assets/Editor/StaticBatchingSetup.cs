#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 씬 배치 요소의 Static Batching을 일괄 설정하는 에디터 도구
///
/// 메뉴 위치: Tools/최적화/
///
/// 주의:
///   - Castle은 FloorObject.SetFloor()가 gameObject.layer를 변경하므로 Static 제외
///   - RuntimeBuild 오브젝트(건물, 워커 등)는 Static 제외
///   - 설정 후 반드시 씬을 저장할 것
/// </summary>
public static class StaticBatchingSetup
{
    // ---
    // 타일맵 Static 설정
    // ---

    [MenuItem("Tools/최적화/타일맵 BatchingStatic 설정")]
    public static void SetTilemapStatic()
    {
        var tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        int count = 0;

        foreach (var tilemap in tilemaps)
        {
            var go    = tilemap.gameObject;
            var flags = GameObjectUtility.GetStaticEditorFlags(go);

            // BatchingStatic 추가 (기존 플래그 유지)
            flags |= StaticEditorFlags.BatchingStatic;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            count++;

            Debug.Log($"[StaticSetup] '{go.name}' → BatchingStatic 설정");
        }

        MarkScenesDirty();
        Debug.Log($"[StaticSetup] 타일맵 {count}개 BatchingStatic 설정 완료. 씬을 저장하세요.");
    }

    // ---
    // Cluster Static 설정
    // ---

    [MenuItem("Tools/최적화/Cluster BatchingStatic 설정")]
    public static void SetClusterStatic()
    {
        var clusters = Object.FindObjectsByType<Cluster>(FindObjectsSortMode.None);
        int count = 0;

        foreach (var cluster in clusters)
        {
            var go    = cluster.gameObject;
            var flags = GameObjectUtility.GetStaticEditorFlags(go);
            flags |= StaticEditorFlags.BatchingStatic;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            count++;

            Debug.Log($"[StaticSetup] Cluster '{go.name}' → BatchingStatic 설정");
        }

        MarkScenesDirty();
        Debug.Log($"[StaticSetup] Cluster {count}개 BatchingStatic 설정 완료. 씬을 저장하세요.");
    }

    // ---
    // Static 충돌 검사
    // ---

    [MenuItem("Tools/최적화/Static 충돌 검사")]
    public static void CheckStaticConflicts()
    {
        Debug.Log("[StaticCheck] Static 충돌 검사 시작...");
        bool foundIssue = false;

        // FloorObject가 있는데 BatchingStatic으로 설정된 경우 경고
        var floorObjects = Object.FindObjectsByType<FloorObject>(FindObjectsSortMode.None);
        foreach (var fo in floorObjects)
        {
            var flags = GameObjectUtility.GetStaticEditorFlags(fo.gameObject);
            if ((flags & StaticEditorFlags.BatchingStatic) != 0)
            {
                Debug.LogWarning(
                    $"[StaticCheck] '{fo.gameObject.name}': FloorObject 컴포넌트가 있음에도 " +
                    $"BatchingStatic 설정 → gameObject.layer 런타임 변경으로 문제 발생 가능!",
                    fo.gameObject
                );
                foundIssue = true;
            }
        }

        // ResourceNode 계열이 BatchingStatic인 경우 경고
        var resourceNodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        foreach (var node in resourceNodes)
        {
            var flags = GameObjectUtility.GetStaticEditorFlags(node.gameObject);
            if ((flags & StaticEditorFlags.BatchingStatic) != 0)
            {
                Debug.LogWarning(
                    $"[StaticCheck] '{node.gameObject.name}': ResourceNode가 있음에도 " +
                    $"BatchingStatic 설정 → currentAmount 변경, SetActive 등으로 문제 발생 가능!",
                    node.gameObject
                );
                foundIssue = true;
            }
        }

        if (!foundIssue)
            Debug.Log("[StaticCheck] 충돌 없음. Static 설정이 안전합니다.");
        else
            Debug.LogWarning("[StaticCheck] 위 경고를 확인하고 해당 오브젝트의 BatchingStatic을 해제하세요.");
    }

    // ---
    // 전체 실행 (타일맵 + Cluster 일괄 처리)
    // ---

    [MenuItem("Tools/최적화/전체 Static 최적화 실행")]
    public static void RunAll()
    {
        SetTilemapStatic();
        SetClusterStatic();
        CheckStaticConflicts();
        Debug.Log("[StaticSetup] 전체 Static 최적화 완료. 씬을 저장(Ctrl+S)하세요.");
    }

    // ---
    // 내부
    // ---

    private static void MarkScenesDirty()
    {
        EditorSceneManager.MarkAllScenesDirty();
    }
}
#endif

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환 중앙 관리자
///
/// 사용법:
///   SceneLoader.LoadTitle();
///   SceneLoader.LoadStageSelect();
///   SceneLoader.LoadStage(stageData);
///   SceneLoader.QuitGame();
///
/// 씬 이름은 아래 상수와 정확히 일치해야 함
/// (File → Build Settings에 씬 등록 필수)
/// </summary>
public static class SceneLoader
{
    // -------------------------------------------------------
    // 씬 이름 상수
    // -------------------------------------------------------
    public const string SCENE_TITLE        = "TitleScene";
    public const string SCENE_STAGE_SELECT = "StageSelectScene";

    // 게임 씬은 StageData에서 sceneName을 읽어 동적 로드

    // 현재 로드 중인지 여부 (중복 호출 방지)
    public static bool IsLoading { get; private set; }

    // 마지막으로 선택한 StageData (게임 씬에서 참조)
    public static StageData SelectedStage { get; private set; }

    // -------------------------------------------------------
    // 씬 전환 메서드
    // -------------------------------------------------------
    public static void LoadTitle()
    {
        Load(SCENE_TITLE);
    }

    public static void LoadStageSelect()
    {
        Load(SCENE_STAGE_SELECT);
    }

    /// <summary>
    /// 스테이지 선택 후 게임 씬 로드
    /// StageData.sceneName에 지정된 씬으로 이동
    /// </summary>
    public static void LoadStage(StageData stage)
    {
        if (stage == null)
        {
            Debug.LogError("[SceneLoader] StageData가 null입니다.");
            return;
        }
        SelectedStage = stage;
        Load(stage.sceneName);
    }

    public static void QuitGame()
    {
        Debug.Log("[SceneLoader] 게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // -------------------------------------------------------
    // 내부 로드
    // -------------------------------------------------------
    private static void Load(string sceneName)
    {
        if (IsLoading)
        {
            Debug.LogWarning("[SceneLoader] 이미 로드 중입니다.");
            return;
        }

        // MonoBehaviour 없이 코루틴을 실행하기 위한 Runner 활용
        SceneLoaderRunner.Run(sceneName);
    }

    internal static void SetLoading(bool value) => IsLoading = value;
}

/// <summary>
/// SceneLoader 코루틴 실행용 내부 MonoBehaviour
/// 씬마다 자동 생성되며 DontDestroyOnLoad로 유지
/// </summary>
internal class SceneLoaderRunner : MonoBehaviour
{
    private static SceneLoaderRunner _instance;

    internal static void Run(string sceneName)
    {
        if (_instance == null)
        {
            var go = new GameObject("[SceneLoaderRunner]");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<SceneLoaderRunner>();
        }
        _instance.StartCoroutine(_instance.LoadAsync(sceneName));
    }

    private IEnumerator LoadAsync(string sceneName)
    {
        SceneLoader.SetLoading(true);

        // 페이드 아웃 (FadeManager가 있으면 활용, 없으면 바로 전환)
        if (FadeManager.Instance != null)
            yield return FadeManager.Instance.FadeOut();

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
            yield return null;

        op.allowSceneActivation = true;
        yield return null;

        if (FadeManager.Instance != null)
            yield return FadeManager.Instance.FadeIn();

        SceneLoader.SetLoading(false);
    }
}

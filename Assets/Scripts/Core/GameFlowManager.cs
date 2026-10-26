
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameFlowManager
{
    private const string MainMenuScene = "MainMenu";
    private const string SharedUIScene = "SharedUI";
    private const string ApiaryScene = "Apiary";
    private const string ObservationScene = "Observation";

    private readonly MonoBehaviour runner;

    private bool isTransitioning;

    private readonly ApiaryCameraState apiaryCameraState =
        new ApiaryCameraState();

    public bool IsTransitioning => isTransitioning;

    public GameFlowManager(MonoBehaviour coroutineRunner)
    {
        runner = coroutineRunner;
    }

    public void OpenMainMenu()
    {
        if (isTransitioning)
            return;

        runner.StartCoroutine(OpenMainMenuRoutine());
    }

    public void OpenApiary()
    {
        if (isTransitioning)
            return;

        runner.StartCoroutine(OpenApiaryRoutine());
    }

    public void OpenObservation(int gridX, int gridY)
    {
        if (isTransitioning)
            return;

        runner.StartCoroutine(
            OpenObservationRoutine(gridX, gridY));
    }

    // 메인 메뉴
    private IEnumerator OpenMainMenuRoutine()
    {
        isTransitioning = true;

        Debug.Log("[GameFlow] MainMenu 로드 시작");

        // Single 로드로 기존 일반 씬 교체
        yield return SceneManager.LoadSceneAsync(
            MainMenuScene,
            LoadSceneMode.Single);

        Debug.Log("[GameFlow] MainMenu 로드 완료");

        isTransitioning = false;
    }

    // 양봉장
    private IEnumerator OpenApiaryRoutine()
    {
        isTransitioning = true;

        if (GameBootstrap.Instance == null ||
            GameBootstrap.Instance.GameWorld == null)
        {
            Debug.LogError(
                "[GameFlow] GameWorld가 없습니다.");

            isTransitioning = false;
            yield break;
        }

        // 공통 UI 로드
        if (!IsSceneLoaded(SharedUIScene))
        {
            yield return SceneManager.LoadSceneAsync(
                SharedUIScene,
                LoadSceneMode.Additive);
        }

        // 기존 메인 메뉴 제거
        Debug.Log("[GameFlow] MainMenu 언로드 시작");

        yield return UnloadIfLoaded(MainMenuScene);

        Debug.Log("[GameFlow] MainMenu 언로드 완료");

        // 양봉장 로드
        if (!IsSceneLoaded(ApiaryScene))
        {
            yield return SceneManager.LoadSceneAsync(
                ApiaryScene,
                LoadSceneMode.Additive);
        }

        Scene apiaryScene =
            SceneManager.GetSceneByName(ApiaryScene);

        if (apiaryScene.IsValid() && apiaryScene.isLoaded)
        {
            SceneManager.SetActiveScene(apiaryScene);

            // 이전 양봉장 카메라 상태 복원
            if (apiaryCameraState.HasSavedState)
            {
                Camera camera = FindApiaryCamera();

                if (camera != null)
                {
                    apiaryCameraState.Restore(camera);

                    Debug.Log(
                        "[GameFlow] 양봉장 카메라 상태 복원 완료");
                }
                else
                {
                    Debug.LogWarning(
                        "[GameFlow] 복원할 양봉장 카메라를 찾지 못했습니다.");
                }
            }
        }

        // 관찰 씬 제거
        yield return UnloadIfLoaded(ObservationScene);

        isTransitioning = false;
    }

    // 관찰 씬
    private IEnumerator OpenObservationRoutine(
        int gridX,
        int gridY)
    {
        isTransitioning = true;

        // 관찰 대상 저장
        ObservationContext.GridX = gridX;
        ObservationContext.GridY = gridY;

        // 양봉장 카메라 상태 저장
        Camera camera = FindApiaryCamera();

        if (camera != null)
        {
            apiaryCameraState.Save(camera);

            Debug.Log(
                "[GameFlow] 양봉장 카메라 상태 저장 완료");
        }
        else
        {
            Debug.LogWarning(
                "[GameFlow] 양봉장 카메라를 찾지 못했습니다.");
        }

        // 양봉장 씬 언로드
        yield return UnloadIfLoaded(ApiaryScene);

        // 관찰 씬 로드
        if (!IsSceneLoaded(ObservationScene))
        {
            yield return SceneManager.LoadSceneAsync(
                ObservationScene,
                LoadSceneMode.Additive);
        }

        Scene observationScene =
            SceneManager.GetSceneByName(ObservationScene);

        if (observationScene.IsValid() &&
            observationScene.isLoaded)
        {
            SceneManager.SetActiveScene(observationScene);
        }

        isTransitioning = false;
    }

    // Apiary 씬의 카메라 검색
    private static Camera FindApiaryCamera()
    {
        Scene scene =
            SceneManager.GetSceneByName(ApiaryScene);

        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Camera camera =
                root.GetComponentInChildren<Camera>(true);

            if (camera != null)
                return camera;
        }

        return null;
    }

    // 씬 로드 상태 확인
    private static bool IsSceneLoaded(string sceneName)
    {
        Scene scene =
            SceneManager.GetSceneByName(sceneName);

        return scene.IsValid() && scene.isLoaded;
    }

    // 로드된 씬만 언로드
    private static IEnumerator UnloadIfLoaded(
        string sceneName)
    {
        Scene scene =
            SceneManager.GetSceneByName(sceneName);

        if (scene.IsValid() && scene.isLoaded)
        {
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }

    // 새 게임 시작 시 카메라 상태 초기화
    public void ResetApiaryCamera()
    {
        apiaryCameraState.Clear();
    }
}
